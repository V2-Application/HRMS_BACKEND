-- ============================================================================
-- GEO ATTENDANCE 2-LEVEL APPROVAL MIGRATION
-- Level 1: Manager Approval  →  Level 2: Master Final Approval
-- ============================================================================
-- IMPORTANT: Make sure you are connected to the correct database!
USE HRMS;
GO

-- Verify tables exist before proceeding
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'tblStatus')
BEGIN
    RAISERROR('Table tblStatus not found. Are you on the correct database?', 16, 1);
    RETURN;
END
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'tblEmployee')
BEGIN
    RAISERROR('Table tblEmployee not found. Are you on the correct database?', 16, 1);
    RETURN;
END
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AttendanceRecord')
BEGIN
    RAISERROR('Table AttendanceRecord not found. Are you on the correct database?', 16, 1);
    RETURN;
END
GO

-- 1. Add new status "ManagerApproved" (StatusId = 5) if not exists
IF NOT EXISTS (SELECT 1 FROM tblStatus WHERE StatusId = 5)
BEGIN
    -- Try with IDENTITY_INSERT first; if StatusId is not identity, just insert directly
    BEGIN TRY
        SET IDENTITY_INSERT tblStatus ON;
        INSERT INTO tblStatus (StatusId, StatusName, CreatedOn, CreatedBy)
        VALUES (5, N'ManagerApproved', GETDATE(), N'System');
        SET IDENTITY_INSERT tblStatus OFF;
    END TRY
    BEGIN CATCH
        -- StatusId is not an identity column, insert directly
        INSERT INTO tblStatus (StatusId, StatusName, CreatedOn, CreatedBy)
        VALUES (5, N'ManagerApproved', GETDATE(), N'System');
    END CATCH
    PRINT 'Status 5 (ManagerApproved) inserted successfully.';
END
ELSE
BEGIN
    PRINT 'Status 5 already exists, skipping.';
END
GO

-- 2. Create GeoAttendanceApproval table for tracking 2-level approval workflow
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'GeoAttendanceApproval')
BEGIN
    CREATE TABLE dbo.GeoAttendanceApproval
    (
        Id                      INT IDENTITY(1,1) PRIMARY KEY,
        EmployeeId              BIGINT          NOT NULL,
        PunchDate               DATE            NOT NULL,

        -- Level 1: Manager Approval
        ManagerApprovalStatusId INT             NOT NULL DEFAULT 4,   -- 4=Pending
        ManagerApproverId       NVARCHAR(100)   NULL,
        ManagerApprovalOn       DATETIME2       NULL,
        ManagerRemarks          NVARCHAR(500)   NULL,

        -- Level 2: Master Approval
        MasterApprovalStatusId  INT             NOT NULL DEFAULT 4,   -- 4=Pending
        MasterApproverId        NVARCHAR(100)   NULL,
        MasterApprovalOn        DATETIME2       NULL,
        MasterRemarks           NVARCHAR(500)   NULL,

        -- Final computed status: 4=Pending, 5=ManagerApproved, 1=Approved, 2=Rejected
        FinalStatusId           INT             NOT NULL DEFAULT 4,

        CreatedOn               DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
        LastUpdatedOn           DATETIME2       NULL,

        -- Unique constraint: one approval record per employee per day
        CONSTRAINT UQ_GeoAttendanceApproval_Employee_Date UNIQUE (EmployeeId, PunchDate),

        -- Foreign keys
        CONSTRAINT FK_GeoAttendanceApproval_Employee FOREIGN KEY (EmployeeId) REFERENCES tblEmployee(EmployeeId),
        CONSTRAINT FK_GeoAttendanceApproval_FinalStatus FOREIGN KEY (FinalStatusId) REFERENCES tblStatus(StatusId),
        CONSTRAINT FK_GeoAttendanceApproval_ManagerStatus FOREIGN KEY (ManagerApprovalStatusId) REFERENCES tblStatus(StatusId),
        CONSTRAINT FK_GeoAttendanceApproval_MasterStatus FOREIGN KEY (MasterApprovalStatusId) REFERENCES tblStatus(StatusId)
    );

    CREATE NONCLUSTERED INDEX IX_GeoAttendanceApproval_FinalStatus
        ON dbo.GeoAttendanceApproval (FinalStatusId)
        INCLUDE (EmployeeId, PunchDate);

    CREATE NONCLUSTERED INDEX IX_GeoAttendanceApproval_Employee
        ON dbo.GeoAttendanceApproval (EmployeeId, PunchDate)
        INCLUDE (FinalStatusId, ManagerApprovalStatusId, MasterApprovalStatusId);

    PRINT 'GeoAttendanceApproval table created successfully.';
END
ELSE
BEGIN
    PRINT 'GeoAttendanceApproval table already exists, skipping.';
END
GO

-- 3. Seed GeoAttendanceApproval rows from existing AttendanceRecord data
INSERT INTO GeoAttendanceApproval (EmployeeId, PunchDate, ManagerApprovalStatusId, MasterApprovalStatusId, FinalStatusId, CreatedOn)
SELECT
    sub.EmployeeId,
    sub.PunchDate,
    CASE sub.StatusId WHEN 1 THEN 1 WHEN 2 THEN 2 ELSE 4 END,
    CASE sub.StatusId WHEN 1 THEN 1 WHEN 2 THEN 2 ELSE 4 END,
    sub.StatusId,
    SYSUTCDATETIME()
FROM (
    SELECT
        ar.EmployeeId,
        CONVERT(DATE, ar.PunchTimeUtc) AS PunchDate,
        MIN(ar.StatusId) AS StatusId
    FROM AttendanceRecord ar
    GROUP BY ar.EmployeeId, CONVERT(DATE, ar.PunchTimeUtc)
) sub
WHERE NOT EXISTS (
    SELECT 1 FROM GeoAttendanceApproval ga
    WHERE ga.EmployeeId = sub.EmployeeId AND ga.PunchDate = sub.PunchDate
);
PRINT 'Seeded existing data into GeoAttendanceApproval.';
GO

-- 4. Update stored procedure: usp_ApproveGeoAttendance (2-level approval)
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

    -- ===================== SUPERADMIN: approve/reject both levels at once =====================
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

    -- ===================== MANAGER APPROVAL (Level 1) =====================
    ELSE IF @IsManager = 1
    BEGIN
        IF @CurrentFinalStatus <> 4
        BEGIN
            RAISERROR('This request is no longer pending manager approval.', 16, 1);
            RETURN;
        END

        IF @StatusId = 2  -- Manager REJECTS → Final Rejected
        BEGIN
            UPDATE GeoAttendanceApproval
            SET ManagerApprovalStatusId = 2,
                ManagerApproverId       = @LastUpdatedBy,
                ManagerApprovalOn       = @Now,
                ManagerRemarks          = @Remarks,
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
        ELSE IF @StatusId = 1  -- Manager APPROVES → Status 5 (ManagerApproved, awaiting Master)
        BEGIN
            UPDATE GeoAttendanceApproval
            SET ManagerApprovalStatusId = 1,
                ManagerApproverId       = @LastUpdatedBy,
                ManagerApprovalOn       = @Now,
                ManagerRemarks          = @Remarks,
                FinalStatusId           = 16,
                LastUpdatedOn           = @Now
            WHERE EmployeeId = @EmployeeId AND PunchDate = @PunchDate;

            UPDATE AttendanceRecord
            SET StatusId       = 16,
                Remarks        = @Remarks,
                LastUpdatedBy  = @LastUpdatedBy,
                LastUpdatedOn  = @Now
            WHERE EmployeeId = @EmployeeId
              AND CONVERT(DATE, PunchTimeUtc) = @PunchDate;
        END
    END

    -- ===================== MASTER APPROVAL (Level 2) =====================
    ELSE IF @IsMaster = 1
    BEGIN
        IF @CurrentFinalStatus <> 16
        BEGIN
            RAISERROR('This request must be approved by a manager first before master can act.', 16, 1);
            RETURN;
        END

        IF @StatusId = 2  -- Master REJECTS → Final Rejected
        BEGIN
            UPDATE GeoAttendanceApproval
            SET MasterApprovalStatusId = 2,
                MasterApproverId       = @LastUpdatedBy,
                MasterApprovalOn       = @Now,
                MasterRemarks          = @Remarks,
                FinalStatusId          = 2,
                LastUpdatedOn          = @Now
            WHERE EmployeeId = @EmployeeId AND PunchDate = @PunchDate;

            UPDATE AttendanceRecord
            SET StatusId       = 2,
                Remarks        = @Remarks,
                LastUpdatedBy  = @LastUpdatedBy,
                LastUpdatedOn  = @Now
            WHERE EmployeeId = @EmployeeId
              AND CONVERT(DATE, PunchTimeUtc) = @PunchDate;
        END
        ELSE IF @StatusId = 1  -- Master APPROVES → Final Approved
        BEGIN
            UPDATE GeoAttendanceApproval
            SET MasterApprovalStatusId = 1,
                MasterApproverId       = @LastUpdatedBy,
                MasterApprovalOn       = @Now,
                MasterRemarks          = @Remarks,
                FinalStatusId          = 1,
                LastUpdatedOn          = @Now
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

-- 5. Update stored procedure: usp_GetDailyAttendanceSummaryGeo (add approval info)
CREATE OR ALTER PROCEDURE dbo.usp_GetDailyAttendanceSummaryGeo
    @ManagerId   BIGINT,
    @Role        NVARCHAR(50),
    @StatusId    INT = 0,
    @PageNumber  INT = 1,
    @PageSize    INT = 10,
    @SearchTerm  NVARCHAR(100) = NULL,
    @TimeZoneId  NVARCHAR(64) = N'UTC'
AS
BEGIN
    SET NOCOUNT ON;

    IF (@PageNumber < 1) SET @PageNumber = 1;
    IF (@PageSize   < 1) SET @PageSize   = 10;

    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;
    DECLARE @RoleLower NVARCHAR(50) = LOWER(LTRIM(RTRIM(@Role)));

    DECLARE @ManagerEcode NVARCHAR(50);
    SELECT @ManagerEcode = e.Ecode
    FROM tblEmployee e
    WHERE e.EmployeeId = @ManagerId;

    DECLARE @Term NVARCHAR(100) = NULL;
    IF (@SearchTerm IS NOT NULL AND LTRIM(RTRIM(@SearchTerm)) <> N'')
        SET @Term = N'%' + LOWER(LTRIM(RTRIM(@SearchTerm))) + N'%';

    ;WITH Base AS
    (
        SELECT
            ar.EmployeeId,
            ar.PunchType,
            ar.PunchTimeUtc,
            ar.Latitude,
            ar.Longitude,
            ar.WithinGeofence,
            ar.DeviceInfo,
            ar.ClientIp,
            ar.StatusId,
            ar.Remarks,
            ar.Address,
            ar.ProofPath,
            e.Ecode,
            COALESCE(
                e.[FULL NAME],
                NULLIF(
                    CONCAT(
                        NULLIF(e.FirstName, N''),
                        CASE WHEN e.FirstName IS NOT NULL AND e.LastName IS NOT NULL THEN N' ' ELSE N'' END,
                        NULLIF(e.LastName,  N'')
                    ),
                    N''
                ),
                N'Unknown'
            ) AS EmployeeName,
            CASE
                WHEN @TimeZoneId = N'UTC'
                    THEN CONVERT(date, ar.PunchTimeUtc)
                ELSE CONVERT(date, (ar.PunchTimeUtc AT TIME ZONE N'UTC') AT TIME ZONE @TimeZoneId)
            END AS PunchDate
        FROM AttendanceRecord ar
        INNER JOIN tblEmployee e ON e.EmployeeId = ar.EmployeeId
        WHERE
            (
                @RoleLower = N'superadmin'
                OR ( @ManagerEcode IS NOT NULL AND e.ReportheadEcode = @ManagerEcode )
                OR ( @RoleLower = N'master' )
            )
            AND (@StatusId = 0 OR ar.StatusId = @StatusId)
    ),
    Grouped AS
    (
        SELECT
            b.EmployeeId,
            b.Ecode,
            b.EmployeeName,
            b.PunchDate,
            COUNT(*)                                          AS PunchCount,
            SUM(CASE WHEN b.PunchType = 1 THEN 1 ELSE 0 END)  AS PunchInCount,
            SUM(CASE WHEN b.PunchType = 2 THEN 1 ELSE 0 END)  AS PunchOutCount,
            MIN(b.PunchTimeUtc)                                AS FirstPunchUtc,
            MAX(b.PunchTimeUtc)                                AS LastPunchUtc,
            (SELECT TOP 1 b2.Remarks FROM Base b2
             WHERE b2.EmployeeId = b.EmployeeId AND b2.PunchDate = b.PunchDate
             AND b2.Remarks IS NOT NULL ORDER BY b2.PunchTimeUtc) AS Remarks,
            (SELECT TOP 1 b2.Address FROM Base b2
             WHERE b2.EmployeeId = b.EmployeeId AND b2.PunchDate = b.PunchDate
             AND b2.Address IS NOT NULL ORDER BY b2.PunchTimeUtc) AS Address
        FROM Base b
        GROUP BY b.EmployeeId, b.Ecode, b.EmployeeName, b.PunchDate
    ),
    StatusAgg AS
    (
        SELECT
            b.EmployeeId,
            b.PunchDate,
            b.StatusId,
            COUNT(*) AS Cnt
        FROM Base b
        GROUP BY b.EmployeeId, b.PunchDate, b.StatusId
    ),
    ModeStatus AS
    (
        SELECT
            sa.EmployeeId,
            sa.PunchDate,
            sa.StatusId AS SummaryStatusId,
            ROW_NUMBER() OVER (
                PARTITION BY sa.EmployeeId, sa.PunchDate
                ORDER BY sa.Cnt DESC, sa.StatusId ASC
            ) AS rn
        FROM StatusAgg sa
    ),
    GroupedWithStatus AS
    (
        SELECT
            g.EmployeeId,
            g.Ecode,
            g.EmployeeName,
            g.PunchDate,
            g.PunchCount,
            g.PunchInCount,
            g.PunchOutCount,
            g.FirstPunchUtc,
            g.LastPunchUtc,
            g.Remarks,
            g.Address,
            ms.SummaryStatusId
        FROM Grouped g
        LEFT JOIN ModeStatus ms
          ON ms.EmployeeId = g.EmployeeId
         AND ms.PunchDate  = g.PunchDate
         AND ms.rn = 1
    ),
    FinalWithApproval AS
    (
        SELECT
            gws.*,
            s.StatusName AS SummaryStatusName,
            ga.ManagerApprovalStatusId,
            ms2.StatusName AS ManagerApprovalStatusName,
            ga.ManagerApproverId,
            ga.ManagerApprovalOn,
            ga.ManagerRemarks,
            ga.MasterApprovalStatusId,
            as2.StatusName AS MasterApprovalStatusName,
            ga.MasterApproverId,
            ga.MasterApprovalOn,
            ga.MasterRemarks,
            COALESCE(ga.FinalStatusId, gws.SummaryStatusId) AS ApprovalFinalStatusId
        FROM GroupedWithStatus gws
        LEFT JOIN tblStatus s ON s.StatusId = gws.SummaryStatusId
        LEFT JOIN GeoAttendanceApproval ga
          ON ga.EmployeeId = gws.EmployeeId AND ga.PunchDate = gws.PunchDate
        LEFT JOIN tblStatus ms2 ON ms2.StatusId = ga.ManagerApprovalStatusId
        LEFT JOIN tblStatus as2 ON as2.StatusId = ga.MasterApprovalStatusId
    ),
    Filtered AS
    (
        SELECT *
        FROM FinalWithApproval f
        WHERE
            @Term IS NULL
            OR LOWER(f.EmployeeName) LIKE @Term
            OR LOWER(f.Ecode)        LIKE @Term
            OR LOWER(COALESCE(f.SummaryStatusName,N'')) LIKE @Term
            OR CONVERT(NVARCHAR(30), f.PunchDate, 126) LIKE @Term
    )
    SELECT * INTO #Filtered
    FROM Filtered;

    DECLARE @TotalRecords INT = (SELECT COUNT(*) FROM #Filtered);

    SELECT *
    INTO #Page
    FROM #Filtered
    ORDER BY PunchDate DESC, EmployeeId
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

    ----------------------------------------------------------------------
    -- Result set #1: paged daily summaries with approval info
    ----------------------------------------------------------------------
    SELECT
        p.EmployeeId,
        p.Ecode,
        p.EmployeeName,
        p.Remarks,
        p.PunchDate,
        p.PunchCount,
        p.PunchInCount,
        p.PunchOutCount,
        p.FirstPunchUtc,
        p.LastPunchUtc,
        p.SummaryStatusId,
        p.SummaryStatusName AS StatusName,
        p.Address,
        p.ManagerApprovalStatusId,
        p.ManagerApprovalStatusName,
        p.ManagerApproverId,
        p.ManagerApprovalOn,
        p.ManagerRemarks,
        p.MasterApprovalStatusId,
        p.MasterApprovalStatusName,
        p.MasterApproverId,
        p.MasterApprovalOn,
        p.MasterRemarks,
        p.ApprovalFinalStatusId,
        @TotalRecords AS TotalRecords
    FROM #Page p
    ORDER BY p.PunchDate DESC, p.EmployeeId;

    ----------------------------------------------------------------------
    -- Result set #2: raw punches for the current page
    ----------------------------------------------------------------------
    ;WITH DetailsBase AS
    (
        SELECT
            ar.EmployeeId,
            CASE
                WHEN @TimeZoneId = N'UTC'
                    THEN CONVERT(date, ar.PunchTimeUtc)
                ELSE CONVERT(date, (ar.PunchTimeUtc AT TIME ZONE N'UTC') AT TIME ZONE @TimeZoneId)
            END AS PunchDate,
            ar.PunchTimeUtc,
            ar.PunchType,
            ar.Latitude,
            ar.Longitude,
            ar.WithinGeofence,
            ar.DeviceInfo,
            ar.ClientIp,
            ar.StatusId,
            ar.Remarks,
            ar.Address,
            ar.ProofPath
        FROM AttendanceRecord ar
        INNER JOIN tblEmployee e ON e.EmployeeId = ar.EmployeeId
        WHERE
            (
                @RoleLower = N'superadmin'
                OR ( @ManagerEcode IS NOT NULL AND e.ReportheadEcode = @ManagerEcode )
                OR ( @RoleLower = N'master' )
            )
            AND (@StatusId = 0 OR ar.StatusId = @StatusId)
    )
    SELECT
        d.EmployeeId,
        d.PunchDate,
        d.PunchTimeUtc,
        d.PunchType,
        d.Latitude,
        d.Longitude,
        d.WithinGeofence,
        d.DeviceInfo,
        d.ClientIp,
        d.StatusId,
        d.Remarks,
        d.Address,
        d.ProofPath
    FROM DetailsBase d
    INNER JOIN #Page p
        ON  p.EmployeeId = d.EmployeeId
        AND p.PunchDate  = d.PunchDate
    ORDER BY d.PunchDate DESC, d.EmployeeId, d.PunchTimeUtc;

    DROP TABLE IF EXISTS #Filtered;
    DROP TABLE IF EXISTS #Page;
END
GO

PRINT '=== Migration complete. 2-Level GeoFence Approval is ready. ===';
GO
