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

    DECLARE @ManagerEcode NVARCHAR(50);
    SELECT @ManagerEcode = Ecode FROM tblEmployee WHERE EmployeeId = @ManagerId;

    DECLARE @EmpReportHead NVARCHAR(50);
    SELECT @EmpReportHead = ReportheadEcode FROM tblEmployee WHERE EmployeeId = @EmployeeId;

    IF @ManagerEcode IS NOT NULL AND @EmpReportHead = @ManagerEcode
        SET @IsManager = 1;

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

    -- ===================== MANAGER (single level - final). Allowed to act on
    -- pending OR already-decided records (so an approved request can be rejected). =====================
    ELSE IF @IsManager = 1
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

    -- ===================== MASTER: approve/reject directly (any current status) =====================
    ELSE IF @IsMaster = 1
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

    ELSE
    BEGIN
        RAISERROR('You are not authorized to approve/reject this geofence request.', 16, 1);
        RETURN;
    END

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
