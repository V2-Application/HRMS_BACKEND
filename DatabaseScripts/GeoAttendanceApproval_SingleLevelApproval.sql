-- ============================================================================
-- GEO ATTENDANCE SINGLE-LEVEL APPROVAL MIGRATION
-- Changed from 2-level (Manager + Master) to single-level (Manager only)
-- Manager approval/rejection is now FINAL.
-- ============================================================================
USE HRMS;
GO

-- 1. Update stored procedure: usp_ApproveGeoAttendance (single-level approval)
CREATE OR ALTER PROCEDURE dbo.usp_ApproveGeoAttendance
    @ManagerId      BIGINT,
    @Role           NVARCHAR(50),
    @EmployeeId     BIGINT,
    @PunchDate      DATE,
    @StatusId       INT,            -- 1=Approve, 2=Reject
    @Remarks        NVARCHAR(500),
    @TimeZoneId     NVARCHAR(64),
    @LastUpdatedBy  NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Now DATETIME2 = SYSUTCDATETIME();
    DECLARE @RoleLower NVARCHAR(50) = LOWER(LTRIM(RTRIM(@Role)));
    DECLARE @IsSuperAdmin BIT = CASE WHEN @RoleLower = N'superadmin' THEN 1 ELSE 0 END;
    DECLARE @IsMaster BIT = CASE WHEN @RoleLower = N'master' THEN 1 ELSE 0 END;
    DECLARE @IsManager BIT = 0;

    -- Check if caller is the reporting manager
    DECLARE @ManagerEcode NVARCHAR(50);
    SELECT @ManagerEcode = Ecode FROM tblEmployee WHERE EmployeeId = @ManagerId;

    DECLARE @EmpReportHead NVARCHAR(50);
    SELECT @EmpReportHead = ReportheadEcode FROM tblEmployee WHERE EmployeeId = @EmployeeId;

    IF @ManagerEcode IS NOT NULL AND @EmpReportHead = @ManagerEcode
        SET @IsManager = 1;

    -- Ensure approval record exists
    IF NOT EXISTS (
        SELECT 1 FROM GeoAttendanceApproval
        WHERE EmployeeId = @EmployeeId AND PunchDate = @PunchDate
    )
    BEGIN
        INSERT INTO GeoAttendanceApproval (EmployeeId, PunchDate, ManagerApprovalStatusId, MasterApprovalStatusId, FinalStatusId, CreatedOn)
        VALUES (@EmployeeId, @PunchDate, 4, 4, 4, @Now);
    END

    DECLARE @CurrentFinalStatus INT;
    SELECT @CurrentFinalStatus = FinalStatusId
    FROM GeoAttendanceApproval
    WHERE EmployeeId = @EmployeeId AND PunchDate = @PunchDate;

    -- ===================== SUPERADMIN: approve/reject directly =====================
    IF @IsSuperAdmin = 1
    BEGIN
        UPDATE GeoAttendanceApproval
        SET ManagerApprovalStatusId = @StatusId,
            ManagerApproverId       = @LastUpdatedBy,
            ManagerApprovalOn       = @Now,
            ManagerRemarks          = @Remarks,
            MasterApprovalStatusId  = @StatusId,
            MasterApproverId        = @LastUpdatedBy,
            MasterApprovalOn        = @Now,
            MasterRemarks           = @Remarks,
            FinalStatusId           = @StatusId,
            LastUpdatedOn           = @Now
        WHERE EmployeeId = @EmployeeId AND PunchDate = @PunchDate;

        UPDATE AttendanceRecord
        SET StatusId       = @StatusId,
            Remarks        = @Remarks,
            LastUpdatedBy  = @LastUpdatedBy,
            LastUpdatedOn  = @Now
        WHERE EmployeeId = @EmployeeId
          AND CONVERT(DATE, PunchTimeUtc) = @PunchDate;
    END

    -- ===================== MANAGER APPROVAL (Single Level - Final) =====================
    ELSE IF @IsManager = 1
    BEGIN
        IF @CurrentFinalStatus <> 4
        BEGIN
            RAISERROR('This request is no longer pending approval.', 16, 1);
            RETURN;
        END

        IF @StatusId = 2  -- Manager REJECTS -> Final Rejected
        BEGIN
            UPDATE GeoAttendanceApproval
            SET ManagerApprovalStatusId = 2,
                ManagerApproverId       = @LastUpdatedBy,
                ManagerApprovalOn       = @Now,
                ManagerRemarks          = @Remarks,
                MasterApprovalStatusId  = 2,
                MasterApproverId        = @LastUpdatedBy,
                MasterApprovalOn        = @Now,
                MasterRemarks           = @Remarks,
                FinalStatusId           = 2,
                LastUpdatedOn           = @Now
            WHERE EmployeeId = @EmployeeId AND PunchDate = @PunchDate;

            UPDATE AttendanceRecord
            SET StatusId       = 2,
                Remarks        = @Remarks,
                LastUpdatedBy  = @LastUpdatedBy,
                LastUpdatedOn  = @Now
            WHERE EmployeeId = @EmployeeId
              AND CONVERT(DATE, PunchTimeUtc) = @PunchDate;
        END
        ELSE IF @StatusId = 1  -- Manager APPROVES -> Final Approved (single level)
        BEGIN
            UPDATE GeoAttendanceApproval
            SET ManagerApprovalStatusId = 1,
                ManagerApproverId       = @LastUpdatedBy,
                ManagerApprovalOn       = @Now,
                ManagerRemarks          = @Remarks,
                MasterApprovalStatusId  = 1,
                MasterApproverId        = @LastUpdatedBy,
                MasterApprovalOn        = @Now,
                MasterRemarks           = @Remarks,
                FinalStatusId           = 1,
                LastUpdatedOn           = @Now
            WHERE EmployeeId = @EmployeeId AND PunchDate = @PunchDate;

            UPDATE AttendanceRecord
            SET StatusId       = 1,
                Remarks        = @Remarks,
                LastUpdatedBy  = @LastUpdatedBy,
                LastUpdatedOn  = @Now
            WHERE EmployeeId = @EmployeeId
              AND CONVERT(DATE, PunchTimeUtc) = @PunchDate;
        END
    END

    -- ===================== MASTER: also allowed to approve/reject directly =====================
    ELSE IF @IsMaster = 1
    BEGIN
        IF @CurrentFinalStatus <> 4
        BEGIN
            RAISERROR('This request is no longer pending approval.', 16, 1);
            RETURN;
        END

        UPDATE GeoAttendanceApproval
        SET ManagerApprovalStatusId = @StatusId,
            ManagerApproverId       = @LastUpdatedBy,
            ManagerApprovalOn       = @Now,
            ManagerRemarks          = @Remarks,
            MasterApprovalStatusId  = @StatusId,
            MasterApproverId        = @LastUpdatedBy,
            MasterApprovalOn        = @Now,
            MasterRemarks           = @Remarks,
            FinalStatusId           = @StatusId,
            LastUpdatedOn           = @Now
        WHERE EmployeeId = @EmployeeId AND PunchDate = @PunchDate;

        UPDATE AttendanceRecord
        SET StatusId       = @StatusId,
            Remarks        = @Remarks,
            LastUpdatedBy  = @LastUpdatedBy,
            LastUpdatedOn  = @Now
        WHERE EmployeeId = @EmployeeId
          AND CONVERT(DATE, PunchTimeUtc) = @PunchDate;
    END

    ELSE
    BEGIN
        RAISERROR('You are not authorized to approve/reject this geofence request.', 16, 1);
        RETURN;
    END

    -- Return result
    SELECT
        @@ROWCOUNT AS RowsUpdated,
        @EmployeeId AS EmployeeId,
        @PunchDate AS PunchDate,
        ga.FinalStatusId AS StatusIdApplied,
        s.StatusName AS StatusNameApplied
    FROM GeoAttendanceApproval ga
    LEFT JOIN tblStatus s ON s.StatusId = ga.FinalStatusId
    WHERE ga.EmployeeId = @EmployeeId AND ga.PunchDate = @PunchDate;
END
GO

-- 2. Migrate any existing records stuck in status 16 (ManagerApproved/awaiting master)
--    Move them back to Pending so managers can re-approve under the new single-level flow
UPDATE GeoAttendanceApproval
SET FinalStatusId = 4,
    MasterApprovalStatusId = 4,
    MasterApproverId = NULL,
    MasterApprovalOn = NULL,
    MasterRemarks = NULL,
    LastUpdatedOn = SYSUTCDATETIME()
WHERE FinalStatusId = 16;

UPDATE ar
SET ar.StatusId = 4,
    ar.LastUpdatedOn = SYSUTCDATETIME()
FROM AttendanceRecord ar
INNER JOIN GeoAttendanceApproval ga
    ON ga.EmployeeId = ar.EmployeeId
    AND ga.PunchDate = CONVERT(DATE, ar.PunchTimeUtc)
WHERE ga.FinalStatusId = 4
  AND ar.StatusId = 16;
GO

PRINT '=== Migration complete. Single-Level GeoFence Approval (Manager Only) is ready. ===';
GO
