-- Snapshot of dbo.usp_GetDailyAttendanceSummaryGeo on PROD before the 20260526_0932 ALTER.
-- To rollback: replace CREATE PROCEDURE with CREATE OR ALTER and run.

CREATE   PROCEDURE dbo.usp_GetDailyAttendanceSummaryGeo
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

    -- Resolve manager's Ecode (self-join model)
    DECLARE @ManagerEcode NVARCHAR(50);
    SELECT @ManagerEcode = e.Ecode
    FROM dbo.tblEmployee e
    WHERE e.EmployeeId = @ManagerId;

    -- Caller's EmployeeId as a string, to match AttendanceRecord.LastUpdatedBy /
    -- GeoAttendanceApproval.ManagerApproverId (both store the approver id as text).
    DECLARE @ManagerIdText NVARCHAR(50) = CONVERT(NVARCHAR(50), @ManagerId);

    DECLARE @RoleLower NVARCHAR(50) = LOWER(LTRIM(RTRIM(@Role)));
    DECLARE @IsSuperAdmin BIT =
        CASE WHEN @RoleLower IN (N'superadmin', N'it superadmin', N'master')
             THEN 1 ELSE 0 END;

    -- Pending tab cycle: 26th of previous month â†’ today (inclusive). Applies to all roles.
    DECLARE @Today               DATE     = CAST(GETDATE() AS DATE);
    DECLARE @CycleFrom           DATE     = DATEFROMPARTS(YEAR(DATEADD(MONTH, -1, @Today)),
                                                         MONTH(DATEADD(MONTH, -1, @Today)),
                                                         26);
    DECLARE @CycleToExclusive    DATE     = DATEADD(DAY, 1, @Today);

    -- Simple, case-insensitive search term
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
            ar.LastUpdatedBy,
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
        FROM dbo.AttendanceRecord ar
        INNER JOIN dbo.tblEmployee e ON e.EmployeeId = ar.EmployeeId
        LEFT JOIN dbo.GeoAttendanceApproval ga
            ON ga.EmployeeId = ar.EmployeeId
           AND ga.PunchDate = CAST(ar.PunchTimeUtc AS DATE)
        WHERE
            (
                @IsSuperAdmin = 1
                OR ( @ManagerEcode IS NOT NULL AND e.ReportheadEcode = @ManagerEcode )
            )
            AND (@StatusId = 0 OR ar.StatusId = @StatusId)
            -- Pending tab: restrict to cycle 26th prev â†’ today
            AND (
                @StatusId <> 4
                OR CAST(ar.PunchTimeUtc AS DATE) >= @CycleFrom
                    AND CAST(ar.PunchTimeUtc AS DATE) < @CycleToExclusive
            )
            -- Approved / Rejected tabs (non-SuperAdmin): only rows the caller acted on
            AND (
                @IsSuperAdmin = 1
                OR @StatusId NOT IN (1, 2)
                OR ar.LastUpdatedBy = @ManagerIdText
                OR ga.ManagerApproverId = @ManagerIdText
            )
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
    FinalWithStatusName AS
    (
        SELECT
            gws.*,
            s.StatusName AS SummaryStatusName
        FROM GroupedWithStatus gws
        LEFT JOIN dbo.tblStatus s
          ON s.StatusId = gws.SummaryStatusId
    ),
    Filtered AS
    (
        SELECT *
        FROM FinalWithStatusName f
        WHERE
            @Term IS NULL
            OR LOWER(f.EmployeeName) LIKE @Term
            OR LOWER(f.Ecode)        LIKE @Term
            OR LOWER(COALESCE(f.SummaryStatusName,N'')) LIKE @Term
            OR CONVERT(NVARCHAR(30), f.PunchDate, 126) LIKE @Term
    )
    SELECT * INTO #Filtered FROM Filtered;

    DECLARE @TotalRecords INT = (SELECT COUNT(*) FROM #Filtered);

    SELECT
        EmployeeId,
        Ecode,
        EmployeeName,
        PunchDate,
        PunchCount,
        PunchInCount,
        PunchOutCount,
        FirstPunchUtc,
        LastPunchUtc,
        SummaryStatusId,
        SummaryStatusName,
        Remarks,
        Address
    INTO #Page
    FROM #Filtered
    ORDER BY PunchDate DESC, EmployeeId
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

    ----------------------------------------------------------------------
    -- Result set #1: paged daily summaries (+ TotalRecords constant)
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
        FROM dbo.AttendanceRecord ar
        INNER JOIN dbo.tblEmployee e ON e.EmployeeId = ar.EmployeeId
        WHERE
            (
                @IsSuperAdmin = 1
                OR ( @ManagerEcode IS NOT NULL AND e.ReportheadEcode = @ManagerEcode )
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


