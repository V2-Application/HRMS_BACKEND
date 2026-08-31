/*
    usp_RefreshAttendanceByEcodeList_Range_20260825.sql
    PROD: 192.168.151.28\hrms, database HRMS        (NEW object - nothing existing is altered)

    WHAT IT REPLACES
    ----------------
    The "Refresh Attendance by Range" modal posts to
    /api/EmpAttendance/refreshattendanceemployeebasedonecodelist, which loops the
    selected ecodes ONE AT A TIME (EmpAttendanceController.cs:1221):

      Machine radio -> per employee: TRUNCATE TempEmployeePunches
                                     EXEC prc_Daily_Attendance_range @From,@To,@Ecode
                                     SqlBulkCopy -> TempEmployeePunches
                                     MERGE INTO tblEmployeeMultiPunches
      Table   radio -> per employee: EXEC usp_MergeMonthlyPunchesRange_Optimized

    prc_Daily_Attendance_range pulls EVERY employee from six linked servers, builds
    the whole pivot, then discards all but one employee:

        ... FROM PivotTable WHERE @Ecode IS NULL OR UserID = @Ecode

    So a 624-employee store repeats the six-server pull 624 times (~3,700 remote
    queries) and truncates one shared staging table 624 times, inside a single
    HTTP request. Org-wide through the UI is not realistically possible.

    WHAT THIS DOES
    --------------
    One pass, both radio buttons, whole organization by default.

      @Mode = 'machine'  raw punch pull  -> dbo.tblEmployeeMultiPunches
      @Mode = 'table'    attendance grid -> dbo.tbl_fn_GetMonthlyPunchesRange_productionnewnick_test
                                            (via usp_MergeMonthlyPunchesRange_Optimized)
      @Mode = 'both'     machine first, then table   <- the correct end-to-end order

    ORG-WIDE
    --------
    Leave @Ecodes and @StCode NULL and it covers every employee:
      * machine mode pulls the six sources once for the range and merges everyone
      * table mode calls usp_MergeMonthlyPunchesRange_Optimized with @ECode = NULL,
        which its own code treats as "all employees" (WHERE @ECode IS NULL OR
        e.ECode = @ECode) - one pass, not one per employee

    ORG-WIDE DEFAULT = ALL ACTIVE EMPLOYEES (@ActiveOnly = 1, 15,944 today).
    Machine mode merges punches only for those ecodes, so codes belonging to
    left staff or unknown cards are not written back.

    Note on @Ecodes in TABLE mode: the underlying proc accepts a SINGLE ecode
    (NVARCHAR(50)), not a list. So table mode uses your list only when it contains
    exactly one ecode; with several it processes ALL employees for the range and
    says so in the summary. Machine mode honours the list exactly.

    Behaviour preserved from the existing pipeline (machine mode):
      * identical six-source UNION ALL and collations
      * identical 300-second de-duplication rule
      * identical Punch1..Punch12 pivot ordered by time
      * NoOfPunches counts pivot columns that actually have a punch
      * TotalHours formatted "HH.MM" as prc_Daily_Attendance_range does
      * MERGE on (UserID, PunchDate) with the same insert/update column list

    Differences, on purpose:
      * uses #temp tables - dbo.TempEmployeePunches is never truncated or written,
        so concurrent runs and the background service cannot wipe each other
      * remote date filters are sargable (col >= @From AND col < @To+1) instead of
        CAST(col AS DATE) BETWEEN ..., so the remote servers can seek
      * missing punches written as '00:00:00', matching current stored data
      * nothing is deleted or truncated anywhere

    HOW TO RUN (SSMS)
    -----------------
    -- dry run first, small range, whole org:
    EXEC dbo.usp_RefreshAttendanceByEcodeList_Range
         @FromDate='2026-08-24', @ToDate='2026-08-25', @Mode='machine', @WhatIf=1;

    -- whole organization, raw punches, for real:
    EXEC dbo.usp_RefreshAttendanceByEcodeList_Range
         @FromDate='2026-08-01', @ToDate='2026-08-25', @Mode='machine';

    -- whole organization, rebuild the attendance grid:
    EXEC dbo.usp_RefreshAttendanceByEcodeList_Range
         @FromDate='2026-08-01', @ToDate='2026-08-25', @Mode='table';

    -- end to end (punches, then grid):
    EXEC dbo.usp_RefreshAttendanceByEcodeList_Range
         @FromDate='2026-08-01', @ToDate='2026-08-25', @Mode='both';

    -- still possible to scope it:
    EXEC dbo.usp_RefreshAttendanceByEcodeList_Range
         @FromDate='2026-08-01', @ToDate='2026-08-25', @StCode='RH01', @Mode='machine';

    Run it in a quiet window the first time: org-wide table mode rewrites a large
    slice of a 27.9M-row table, and org-wide machine mode holds the whole range's
    punches in tempdb.
*/

CREATE OR ALTER PROCEDURE dbo.usp_RefreshAttendanceByEcodeList_Range
(
    @FromDate   DATE,
    @ToDate     DATE,
    @Mode       VARCHAR(10)    = 'machine',  -- 'machine' | 'table' | 'both'
    @Ecodes     NVARCHAR(MAX)  = NULL,       -- comma separated. NULL/'' = WHOLE ORGANISATION
    @StCode     NVARCHAR(50)   = NULL,       -- optional: all active employees of one store
    @ActiveOnly BIT            = 1,          -- 1 = only IsActive=1, IsDeleted=0 employees (default)
    @UpdatedBy  NVARCHAR(100)  = N'usp_RefreshAttendanceByEcodeList_Range',
    @WhatIf     BIT            = 0,          -- 1 = report only, nothing written
    @MaxDays    INT            = 62          -- runaway guard on the date range
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    ---------------------------------------------------------------------------
    -- 1. validate
    ---------------------------------------------------------------------------
    IF @FromDate IS NULL OR @ToDate IS NULL
    BEGIN
        RAISERROR('FromDate and ToDate are required.', 16, 1);
        RETURN;
    END

    IF @FromDate > @ToDate
    BEGIN
        RAISERROR('FromDate cannot be greater than ToDate.', 16, 1);
        RETURN;
    END

    SET @Mode = LOWER(LTRIM(RTRIM(ISNULL(@Mode, 'machine'))));

    IF @Mode NOT IN ('machine', 'table', 'both')
    BEGIN
        RAISERROR('Mode must be ''machine'', ''table'' or ''both''.', 16, 1);
        RETURN;
    END

    DECLARE @DayCount INT = DATEDIFF(DAY, @FromDate, @ToDate) + 1;

    IF @DayCount > @MaxDays
    BEGIN
        RAISERROR('Range is %d days, limit is %d. Narrow the range or raise @MaxDays deliberately.',
                  16, 1, @DayCount, @MaxDays);
        RETURN;
    END

    DECLARE @StartedAt   DATETIME2 = SYSDATETIME();
    DECLARE @RawPunches  INT = 0,
            @DayRows     INT = 0,
            @Inserted    INT = 0,
            @Updated     INT = 0;
    DECLARE @TableScope  NVARCHAR(200) = N'(not run)';
    DECLARE @MachineDone NVARCHAR(200) = N'(not run)';

    ---------------------------------------------------------------------------
    -- 2. resolve the employee set (empty = whole organisation)
    ---------------------------------------------------------------------------
    CREATE TABLE #Ecodes (Ecode NVARCHAR(100) NOT NULL PRIMARY KEY);

    IF @Ecodes IS NOT NULL AND LTRIM(RTRIM(@Ecodes)) <> N''
        INSERT INTO #Ecodes (Ecode)
        SELECT DISTINCT LTRIM(RTRIM(value))
        FROM STRING_SPLIT(@Ecodes, ',')
        WHERE LTRIM(RTRIM(value)) <> N'';

    IF @StCode IS NOT NULL AND LTRIM(RTRIM(@StCode)) <> N''
        INSERT INTO #Ecodes (Ecode)
        SELECT DISTINCT e.Ecode
        FROM dbo.tblEmployee e WITH (NOLOCK)
        JOIN dbo.tblLocation l WITH (NOLOCK) ON l.LocationId = e.LocationId
        WHERE UPPER(LTRIM(RTRIM(l.STCode))) = UPPER(LTRIM(RTRIM(@StCode)))
          AND e.IsActive = 1
          AND ISNULL(e.IsDeleted, 0) = 0
          AND e.Ecode IS NOT NULL
          AND NOT EXISTS (SELECT 1 FROM #Ecodes x WHERE x.Ecode = e.Ecode);

    /* No list and no store = whole organisation. With @ActiveOnly = 1 (default)
       that means every ACTIVE employee in tblEmployee, so machine punches for
       people who have left are not merged back in. Set @ActiveOnly = 0 to take
       whatever ecodes the machines return, including unknown/left staff. */
    DECLARE @ExplicitList BIT = CASE WHEN EXISTS (SELECT 1 FROM #Ecodes) THEN 1 ELSE 0 END;

    IF @ExplicitList = 0 AND @ActiveOnly = 1
        INSERT INTO #Ecodes (Ecode)
        SELECT DISTINCT e.Ecode
        FROM dbo.tblEmployee e WITH (NOLOCK)
        WHERE e.IsActive = 1
          AND ISNULL(e.IsDeleted, 0) = 0
          AND e.Ecode IS NOT NULL
          AND LTRIM(RTRIM(e.Ecode)) <> N'';

    /* An explicit list/store still gets trimmed to active staff when asked. */
    DECLARE @DroppedInactive INT = 0;

    IF @ExplicitList = 1 AND @ActiveOnly = 1
    BEGIN
        DELETE x
        FROM #Ecodes x
        WHERE NOT EXISTS (
            SELECT 1 FROM dbo.tblEmployee e WITH (NOLOCK)
            WHERE e.Ecode = x.Ecode AND e.IsActive = 1 AND ISNULL(e.IsDeleted, 0) = 0);
        SET @DroppedInactive = @@ROWCOUNT;
    END

    DECLARE @EcodeCount INT = (SELECT COUNT(*) FROM #Ecodes);
    DECLARE @Scope NVARCHAR(200) =
        CASE
            WHEN @EcodeCount = 0 AND @ActiveOnly = 0 THEN N'WHOLE ORGANISATION (whatever the machines return)'
            WHEN @ExplicitList = 0 THEN N'ALL ACTIVE EMPLOYEES (' + CAST(@EcodeCount AS NVARCHAR(20)) + N')'
            ELSE CAST(@EcodeCount AS NVARCHAR(20)) + N' employee(s)'
                 + CASE WHEN @DroppedInactive > 0
                        THEN N', ' + CAST(@DroppedInactive AS NVARCHAR(20)) + N' inactive dropped'
                        ELSE N'' END
        END;

    ---------------------------------------------------------------------------
    -- 3. MACHINE MODE : one pull from the six sources, one pivot, one MERGE
    ---------------------------------------------------------------------------
    IF @Mode IN ('machine', 'both')
    BEGIN
        CREATE TABLE #Raw
        (
            Machine_Company_Name NVARCHAR(200) NULL,
            E_code               NVARCHAR(100) NULL,
            Attendance_Punch     DATETIME      NULL
        );

        INSERT INTO #Raw (Machine_Company_Name, E_code, Attendance_Punch)
        SELECT 'Saviour', cardno, officepunch
        FROM [192.168.151.31\MSSQLSERVER1].[savior].[dbo].Machinerawpunch
        WHERE officepunch >= @FromDate AND officepunch < DATEADD(DAY, 1, @ToDate)
        UNION ALL
        SELECT 'Saviour', EmpCode, PunchDateTime
        FROM [192.168.151.31\MSSQLSERVER1].[savior].[dbo].Attendance
        WHERE PunchDateTime >= @FromDate AND PunchDateTime < DATEADD(DAY, 1, @ToDate)
        UNION ALL
        SELECT [Machine_Company_Name], [E_code], [Attendance_Punch]
        FROM [192.168.151.31\MSSQLSERVER1].[savior].[dbo].[vw_raw_attendance_data]
        WHERE [Attendance_Punch] >= @FromDate AND [Attendance_Punch] < DATEADD(DAY, 1, @ToDate)
        UNION ALL
        SELECT 'BIOMEX', Employeecode COLLATE SQL_Latin1_General_CP1_CI_AS, Logdatetime
        FROM [192.168.151.25].[SmartOfficedb].[dbo].ATTLOG
        WHERE Logdatetime >= @FromDate AND Logdatetime < DATEADD(DAY, 1, @ToDate)
        UNION ALL
        SELECT [Machine_Company_Name], [E_code] COLLATE SQL_Latin1_General_CP1_CI_AS, [Attendance_Punch]
        FROM [192.168.149.182].[matrix].[dbo].[vw_raw_attendance_data]
        WHERE [Attendance_Punch] >= @FromDate AND [Attendance_Punch] < DATEADD(DAY, 1, @ToDate)
        UNION ALL
        SELECT [Machine_Company_Name], [E_code], [Attendance_Punch]
        FROM [192.168.149.182].[etimetracklite1].[dbo].[vw_raw_attendance_data]
        WHERE [Attendance_Punch] >= @FromDate AND [Attendance_Punch] < DATEADD(DAY, 1, @ToDate);

        DELETE FROM #Raw WHERE E_code IS NULL OR Attendance_Punch IS NULL;

        IF @EcodeCount > 0
            DELETE r FROM #Raw r
            WHERE NOT EXISTS (SELECT 1 FROM #Ecodes x WHERE x.Ecode = r.E_code);

        CREATE CLUSTERED INDEX IX_Raw ON #Raw (E_code, Attendance_Punch);

        SET @RawPunches = (SELECT COUNT(*) FROM #Raw);

        CREATE TABLE #Pivoted
        (
            UserID      NVARCHAR(100) NOT NULL,
            PunchDate   DATE          NOT NULL,
            Punch1 NVARCHAR(16) NULL, Punch2 NVARCHAR(16) NULL, Punch3 NVARCHAR(16) NULL,
            Punch4 NVARCHAR(16) NULL, Punch5 NVARCHAR(16) NULL, Punch6 NVARCHAR(16) NULL,
            Punch7 NVARCHAR(16) NULL, Punch8 NVARCHAR(16) NULL, Punch9 NVARCHAR(16) NULL,
            Punch10 NVARCHAR(16) NULL, Punch11 NVARCHAR(16) NULL, Punch12 NVARCHAR(16) NULL,
            NoOfPunches INT NULL,
            TotalHours  VARCHAR(50) NULL,
            PRIMARY KEY (UserID, PunchDate)
        );

        ;WITH OrderedPunches AS
        (
            SELECT E_code, Attendance_Punch,
                   ROW_NUMBER() OVER (PARTITION BY E_code, CAST(Attendance_Punch AS DATE)
                                      ORDER BY Attendance_Punch) AS rn
            FROM #Raw
        ),
        FilteredPunches AS
        (
            SELECT p1.*
            FROM OrderedPunches p1
            WHERE NOT EXISTS (
                SELECT 1 FROM OrderedPunches p2
                WHERE p2.E_code = p1.E_code
                  AND CAST(p2.Attendance_Punch AS DATE) = CAST(p1.Attendance_Punch AS DATE)
                  AND p2.rn < p1.rn
                  AND DATEDIFF(SECOND, p2.Attendance_Punch, p1.Attendance_Punch) < 300
            )
        ),
        PunchCTE AS
        (
            SELECT E_code AS UserID,
                   CAST(Attendance_Punch AS DATE) AS PDate,
                   CAST(Attendance_Punch AS TIME) AS InTime,
                   'Punch' + CAST(ROW_NUMBER() OVER (PARTITION BY CAST(Attendance_Punch AS DATE), E_code
                                                     ORDER BY CAST(Attendance_Punch AS TIME)) AS VARCHAR) AS PunchNo
            FROM FilteredPunches
        ),
        P AS
        (
            SELECT * FROM PunchCTE
            PIVOT (MAX(InTime) FOR PunchNo IN
                  ([Punch1],[Punch2],[Punch3],[Punch4],[Punch5],[Punch6],
                   [Punch7],[Punch8],[Punch9],[Punch10],[Punch11],[Punch12])) AS PivotTable
        )
        INSERT INTO #Pivoted
            (UserID, PunchDate, Punch1, Punch2, Punch3, Punch4, Punch5, Punch6,
             Punch7, Punch8, Punch9, Punch10, Punch11, Punch12, NoOfPunches, TotalHours)
        SELECT
            UserID,
            PDate,
            ISNULL(CONVERT(VARCHAR(8), [Punch1],  108), '00:00:00'),
            ISNULL(CONVERT(VARCHAR(8), [Punch2],  108), '00:00:00'),
            ISNULL(CONVERT(VARCHAR(8), [Punch3],  108), '00:00:00'),
            ISNULL(CONVERT(VARCHAR(8), [Punch4],  108), '00:00:00'),
            ISNULL(CONVERT(VARCHAR(8), [Punch5],  108), '00:00:00'),
            ISNULL(CONVERT(VARCHAR(8), [Punch6],  108), '00:00:00'),
            ISNULL(CONVERT(VARCHAR(8), [Punch7],  108), '00:00:00'),
            ISNULL(CONVERT(VARCHAR(8), [Punch8],  108), '00:00:00'),
            ISNULL(CONVERT(VARCHAR(8), [Punch9],  108), '00:00:00'),
            ISNULL(CONVERT(VARCHAR(8), [Punch10], 108), '00:00:00'),
            ISNULL(CONVERT(VARCHAR(8), [Punch11], 108), '00:00:00'),
            ISNULL(CONVERT(VARCHAR(8), [Punch12], 108), '00:00:00'),
            (CASE WHEN [Punch1]  IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN [Punch2]  IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN [Punch3]  IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN [Punch4]  IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN [Punch5]  IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN [Punch6]  IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN [Punch7]  IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN [Punch8]  IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN [Punch9]  IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN [Punch10] IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN [Punch11] IS NOT NULL THEN 1 ELSE 0 END +
             CASE WHEN [Punch12] IS NOT NULL THEN 1 ELSE 0 END),
            RIGHT('0' + CAST((ISNULL(DATEDIFF(MINUTE, [Punch1], [Punch2]), 0) +
                              ISNULL(DATEDIFF(MINUTE, [Punch3], [Punch4]), 0) +
                              ISNULL(DATEDIFF(MINUTE, [Punch5], [Punch6]), 0) +
                              ISNULL(DATEDIFF(MINUTE, [Punch7], [Punch8]), 0) +
                              ISNULL(DATEDIFF(MINUTE, [Punch9], [Punch10]), 0) +
                              ISNULL(DATEDIFF(MINUTE, [Punch11], [Punch12]), 0)) / 60 AS VARCHAR), 2)
            + '.' +
            RIGHT('0' + CAST((ISNULL(DATEDIFF(MINUTE, [Punch1], [Punch2]), 0) +
                              ISNULL(DATEDIFF(MINUTE, [Punch3], [Punch4]), 0) +
                              ISNULL(DATEDIFF(MINUTE, [Punch5], [Punch6]), 0) +
                              ISNULL(DATEDIFF(MINUTE, [Punch7], [Punch8]), 0) +
                              ISNULL(DATEDIFF(MINUTE, [Punch9], [Punch10]), 0) +
                              ISNULL(DATEDIFF(MINUTE, [Punch11], [Punch12]), 0)) % 60 AS VARCHAR), 2)
        FROM P;

        SET @DayRows = (SELECT COUNT(*) FROM #Pivoted);

        IF @WhatIf = 1
        BEGIN
            SELECT TOP (1000)
                   p.UserID, p.PunchDate, p.NoOfPunches, p.TotalHours,
                   CASE WHEN t.ID IS NULL THEN 'would INSERT' ELSE 'would UPDATE' END AS Action,
                   t.NoOfPunches AS CurrentNoOfPunches, t.TotalHours AS CurrentTotalHours
            FROM #Pivoted p
            LEFT JOIN dbo.tblEmployeeMultiPunches t
                   ON t.UserID = p.UserID AND t.PunchDate = p.PunchDate
            ORDER BY p.UserID, p.PunchDate;

            SET @MachineDone = N'WHATIF - nothing written';
        END
        ELSE
        BEGIN
            DECLARE @Actions TABLE (Act NVARCHAR(10));

            MERGE INTO dbo.tblEmployeeMultiPunches AS target
            USING #Pivoted AS source
                  ON target.UserID = source.UserID AND target.PunchDate = source.PunchDate
            WHEN MATCHED THEN
                UPDATE SET
                    Punch1 = source.Punch1, Punch2 = source.Punch2, Punch3 = source.Punch3,
                    Punch4 = source.Punch4, Punch5 = source.Punch5, Punch6 = source.Punch6,
                    Punch7 = source.Punch7, Punch8 = source.Punch8, Punch9 = source.Punch9,
                    Punch10 = source.Punch10, Punch11 = source.Punch11, Punch12 = source.Punch12,
                    NoOfPunches   = source.NoOfPunches,
                    TotalHours    = source.TotalHours,
                    LastUpdatedBy = @UpdatedBy,
                    CreatedOn     = SYSDATETIMEOFFSET()
            WHEN NOT MATCHED THEN
                INSERT (UserID, PunchDate, Punch1, Punch2, Punch3, Punch4, Punch5, Punch6,
                        Punch7, Punch8, Punch9, Punch10, Punch11, Punch12,
                        NoOfPunches, TotalHours, CreatedBy, CreatedOn, LastUpdatedBy)
                VALUES (source.UserID, source.PunchDate, source.Punch1, source.Punch2, source.Punch3,
                        source.Punch4, source.Punch5, source.Punch6, source.Punch7, source.Punch8,
                        source.Punch9, source.Punch10, source.Punch11, source.Punch12,
                        source.NoOfPunches, source.TotalHours, @UpdatedBy, SYSDATETIMEOFFSET(), @UpdatedBy)
            OUTPUT $action INTO @Actions;

            SELECT @Inserted = SUM(CASE WHEN Act = 'INSERT' THEN 1 ELSE 0 END),
                   @Updated  = SUM(CASE WHEN Act = 'UPDATE' THEN 1 ELSE 0 END)
            FROM @Actions;

            SET @MachineDone = N'merged into tblEmployeeMultiPunches';
        END

        DROP TABLE #Pivoted;
        DROP TABLE #Raw;
    END

    ---------------------------------------------------------------------------
    -- 4. TABLE MODE : rebuild the attendance grid in one call
    ---------------------------------------------------------------------------
    IF @Mode IN ('table', 'both')
    BEGIN
        DECLARE @SingleEcode NVARCHAR(50) = NULL;

        IF @EcodeCount = 1
            SELECT @SingleEcode = Ecode FROM #Ecodes;

        /* usp_GetMonthlyPunchesRange_Optimized_new_1 selects employees with
           WHERE (@ECode IS NULL OR e.ECode = @ECode) - there is NO IsActive
           filter in it. So a list/@ActiveOnly cannot narrow table mode: with
           anything other than exactly one ecode it rebuilds the grid for every
           employee row, active or not. Machine mode is unaffected. */
        SET @TableScope = CASE
                WHEN @SingleEcode IS NOT NULL THEN N'single ecode ' + @SingleEcode
                WHEN @EcodeCount > 1 THEN N'ALL employees incl. inactive (inner proc takes one ecode and has no IsActive filter)'
                ELSE N'ALL employees incl. inactive (inner proc has no IsActive filter)' END;

        IF @WhatIf = 1
            SET @TableScope = @TableScope + N' - WHATIF, not executed';
        ELSE
            EXEC dbo.usp_MergeMonthlyPunchesRange_Optimized
                 @FromDate = @FromDate,
                 @ToDate   = @ToDate,
                 @ECode    = @SingleEcode;   -- NULL = every employee, single pass
    END

    ---------------------------------------------------------------------------
    -- 5. summary
    ---------------------------------------------------------------------------
    SELECT
        @Mode                                       AS Mode,
        @Scope                                      AS Scope,
        @FromDate                                   AS FromDate,
        @ToDate                                     AS ToDate,
        @DayCount                                   AS Days,
        @RawPunches                                 AS RawPunchesPulled,
        @DayRows                                    AS EmployeeDayRows,
        ISNULL(@Inserted, 0)                        AS RowsInserted,
        ISNULL(@Updated, 0)                         AS RowsUpdated,
        @MachineDone                                AS MachineResult,
        @TableScope                                 AS TableResult,
        DATEDIFF(SECOND, @StartedAt, SYSDATETIME()) AS ElapsedSeconds;

    DROP TABLE #Ecodes;
END
