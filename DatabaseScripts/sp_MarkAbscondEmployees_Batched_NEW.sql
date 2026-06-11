CREATE OR ALTER PROCEDURE dbo.sp_MarkAbscondEmployees_Batched
    @BatchSize INT = 8000
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    -- Per-store absconding window:
    --   STCode 'RH01' -> 7 calendar days, all other stores -> 6 calendar days.
    -- Saturdays/Sundays/weekly-offs/holidays COUNT as absent days. An employee is absconding only
    -- if there is NO attendance evidence (Present / Half-day / Manual / Mispunch / GF / valid punch /
    -- regularized) on ANY day in their trailing window, AND they are not on approved/pending leave
    -- during it, AND they were active for the whole window.
    DECLARE @Now      DATETIME = GETDATE();
    DECLARE @ToDate   DATE     = CAST(@Now AS DATE);
    DECLARE @SeenFastFrom DATE = DATEADD(DAY, -5, @ToDate);  -- 6-day min span for the fast pre-filter
    DECLARE @By       NVARCHAR(50) = N'System';

    /* 0) Active employees (+ store code for per-store window) */
    IF OBJECT_ID('tempdb..#ActiveEmp') IS NOT NULL DROP TABLE #ActiveEmp;
    CREATE TABLE #ActiveEmp(
        EmployeeId   BIGINT       NOT NULL PRIMARY KEY,
        ECode        NVARCHAR(50) NOT NULL,
        EmployeeName NVARCHAR(255) NULL,
        STCode       NVARCHAR(50) NULL,
        DOJ          DATE NULL,
        WindowDays   INT NOT NULL,
        Processed    BIT NOT NULL DEFAULT(0)
    );

    INSERT INTO #ActiveEmp(EmployeeId, ECode, EmployeeName, STCode, DOJ, WindowDays)
    SELECT e.EmployeeId, e.ECode, e.[FULL NAME], l.STCode, CAST(e.DOJ AS DATE),
           CASE WHEN l.STCode = 'RH01' THEN 7 ELSE 6 END
    FROM dbo.tblEmployee e WITH (READPAST)
    LEFT JOIN dbo.tblLocation l WITH (NOLOCK) ON l.LocationId = e.LocationId
    WHERE e.IsActive = 1 AND e.ECode IS NOT NULL;

    /* 1) Fast "seen" pre-filter over the minimum 6-day span (safe: only skips clearly-present staff) */
    IF OBJECT_ID('tempdb..#SeenFast') IS NOT NULL DROP TABLE #SeenFast;
    CREATE TABLE #SeenFast(EmployeeId BIGINT NOT NULL PRIMARY KEY);

    INSERT INTO #SeenFast(EmployeeId)
    SELECT DISTINCT e.EmployeeId
    FROM #ActiveEmp e
    JOIN dbo.tblEmployeeMultiPunches p WITH (READPAST)
      ON p.UserID = e.ECode AND p.PunchDate BETWEEN @SeenFastFrom AND @ToDate;

    INSERT INTO #SeenFast(EmployeeId)
    SELECT DISTINCT e.EmployeeId
    FROM #ActiveEmp e
    JOIN dbo.AttendanceRecord ar WITH (READPAST)
      ON ar.EmployeeId = e.EmployeeId AND ar.StatusId = 1
     AND CONVERT(date, ar.PunchTimeUtc) BETWEEN @SeenFastFrom AND @ToDate
    WHERE e.EmployeeId NOT IN (SELECT EmployeeId FROM #SeenFast);

    /* output staging */
    IF OBJECT_ID('tempdb..#AbscondedOut') IS NOT NULL DROP TABLE #AbscondedOut;
    CREATE TABLE #AbscondedOut(
        EmployeeId BIGINT NOT NULL PRIMARY KEY, ECode NVARCHAR(50) NOT NULL,
        EmployeeName NVARCHAR(255) NULL, AbscondedOn DATETIME NOT NULL, Remarks NVARCHAR(255) NOT NULL
    );

    WHILE EXISTS (SELECT 1 FROM #ActiveEmp a WHERE a.Processed=0 AND a.EmployeeId NOT IN (SELECT EmployeeId FROM #SeenFast))
    BEGIN
        IF OBJECT_ID('tempdb..#Batch') IS NOT NULL DROP TABLE #Batch;
        CREATE TABLE #Batch(
            EmployeeId BIGINT NOT NULL PRIMARY KEY, ECode NVARCHAR(50) NOT NULL,
            EmployeeName NVARCHAR(255) NULL, DOJ DATE NULL, WindowDays INT NOT NULL, WindowStart DATE NOT NULL
        );

        INSERT INTO #Batch(EmployeeId,ECode,EmployeeName,DOJ,WindowDays,WindowStart)
        SELECT TOP(@BatchSize) a.EmployeeId, a.ECode, a.EmployeeName, a.DOJ, a.WindowDays,
               DATEADD(DAY, -(a.WindowDays-1), @ToDate)
        FROM #ActiveEmp a
        WHERE a.Processed=0 AND a.EmployeeId NOT IN (SELECT EmployeeId FROM #SeenFast)
        ORDER BY a.EmployeeId;

        UPDATE a SET Processed=1 FROM #ActiveEmp a JOIN #Batch b ON b.EmployeeId=a.EmployeeId;

        /* facts pulled per-employee for THEIR window only */
        IF OBJECT_ID('tempdb..#Facts') IS NOT NULL DROP TABLE #Facts;
        CREATE TABLE #Facts(
            EmployeeId BIGINT NOT NULL, PunchDate DATE NOT NULL,
            StatusLabel NVARCHAR(50) NULL, IsRegularize BIT NOT NULL, ValidPunches INT NOT NULL,
            PRIMARY KEY (EmployeeId, PunchDate)
        );
        INSERT INTO #Facts(EmployeeId,PunchDate,StatusLabel,IsRegularize,ValidPunches)
        SELECT f.EmployeeId, f.AttendanceDate, f.Status, CAST(f.IsRegularize AS BIT), ISNULL(f.ValidPunchCount,0)
        FROM #Batch b
        CROSS APPLY dbo.fn_GetMonthlyPunchesRange_productionnewnick_live(b.WindowStart, @ToDate, b.ECode) AS f
        OPTION (RECOMPILE);

        /* to-abscond: no attendance evidence anywhere in the window, not on leave, full window coverage, joined before window */
        IF OBJECT_ID('tempdb..#ToAbscond') IS NOT NULL DROP TABLE #ToAbscond;
        CREATE TABLE #ToAbscond(EmployeeId BIGINT NOT NULL PRIMARY KEY, ECode NVARCHAR(50) NOT NULL, EmployeeName NVARCHAR(255) NULL, WindowDays INT NOT NULL);

        INSERT INTO #ToAbscond(EmployeeId,ECode,EmployeeName,WindowDays)
        SELECT b.EmployeeId, b.ECode, b.EmployeeName, b.WindowDays
        FROM #Batch b
        WHERE
          NOT EXISTS (
             SELECT 1 FROM dbo.tblLeaveRequest l WITH (READPAST)
             WHERE l.EmployeeId = b.EmployeeId AND l.StatusId IN (1,2) AND l.IsRevoked = 0
               AND l.StartDate <= @ToDate AND l.EndDate >= b.WindowStart
          )
          AND NOT EXISTS (
             SELECT 1 FROM #Facts f
             WHERE f.EmployeeId = b.EmployeeId
               AND ( f.StatusLabel IN (N'Present',N'Manual Present',N'Manual Present Half Day',N'Half Day Absent',N'Mispunch',N'GF')
                     OR f.ValidPunches > 0 OR f.IsRegularize = 1 )
          )
          AND (SELECT COUNT(DISTINCT f2.PunchDate) FROM #Facts f2 WHERE f2.EmployeeId = b.EmployeeId) = b.WindowDays
          AND (b.DOJ IS NULL OR b.DOJ <= b.WindowStart);

        IF EXISTS (SELECT 1 FROM #ToAbscond)
        BEGIN
            BEGIN TRY
                BEGIN TRAN;

                UPDATE es WITH (ROWLOCK, UPDLOCK)
                SET es.AbscondRemarks = N'Abscond by system: no attendance for ' + CAST(t.WindowDays AS NVARCHAR(12)) + N' day(s)',
                    es.AbscondedBy = @By, es.IsApprovedByManager = 0, es.ManagerRemarks = NULL,
                    es.IsApprovedByHR = 0, es.HRRemarks = NULL, es.ResignationDate = @Now
                FROM dbo.tblEmployeeSepration es JOIN #ToAbscond t ON t.EmployeeId = es.EmployeeId
                WHERE es.ResignationTypeId = 10;

                INSERT INTO dbo.tblEmployeeSepration
                    (EmployeeId, ResignationTypeId, AbscondRemarks, AbscondedBy, ResignationDate, IsApprovedByManager, ManagerRemarks, IsApprovedByHR, HRRemarks)
                SELECT t.EmployeeId, 10, N'Abscond by system: no attendance for ' + CAST(t.WindowDays AS NVARCHAR(12)) + N' day(s)', @By, @Now, 0, NULL, 0, NULL
                FROM #ToAbscond t
                WHERE NOT EXISTS (SELECT 1 FROM dbo.tblEmployeeSepration es WITH (READPAST) WHERE es.EmployeeId = t.EmployeeId AND es.ResignationTypeId = 10);

                UPDATE e WITH (ROWLOCK, UPDLOCK) SET e.IsActive = 0
                FROM dbo.tblEmployee e JOIN #ToAbscond t ON t.EmployeeId = e.EmployeeId WHERE e.IsActive <> 0;

                INSERT INTO dbo.AbscondHistory (EmployeeId, ECode, EmployeeName, AbscondDate, Remarks, CreatedBy)
                SELECT t.EmployeeId, t.ECode, t.EmployeeName, @Now, N'Abscond by system: no attendance for ' + CAST(t.WindowDays AS NVARCHAR(12)) + N' day(s)', @By
                FROM #ToAbscond t
                WHERE NOT EXISTS (SELECT 1 FROM dbo.AbscondHistory ah WITH (READPAST) WHERE ah.EmployeeId = t.EmployeeId AND CAST(ah.AbscondDate AS DATE) = @ToDate);

                COMMIT TRAN;
            END TRY
            BEGIN CATCH
                IF XACT_STATE() <> 0 ROLLBACK TRAN;
            END CATCH;

            INSERT INTO #AbscondedOut(EmployeeId,ECode,EmployeeName,AbscondedOn,Remarks)
            SELECT t.EmployeeId, t.ECode, t.EmployeeName, @Now, N'Abscond by system: no attendance for ' + CAST(t.WindowDays AS NVARCHAR(12)) + N' day(s)'
            FROM #ToAbscond t WHERE NOT EXISTS (SELECT 1 FROM #AbscondedOut o WHERE o.EmployeeId = t.EmployeeId);
        END
    END

    SELECT EmployeeId, ECode, EmployeeName, AbscondedOn, Remarks FROM #AbscondedOut ORDER BY EmployeeName;
    SET NOCOUNT OFF;
END;

