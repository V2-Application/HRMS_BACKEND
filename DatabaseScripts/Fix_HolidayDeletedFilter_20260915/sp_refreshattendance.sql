ALTER PROCEDURE [dbo].[sp_refreshattendance]
AS
BEGIN
    SET NOCOUNT ON;

    ------------------------------------------------------------
    -- Week math uses Monday as start of week
    ------------------------------------------------------------
    SET DATEFIRST 1;  -- 1 = Monday

    ------------------------------------------------------------
    -- Params & policy
    ------------------------------------------------------------
    DECLARE
        @FromDate DATE,
        @ToDate   DATE,
        @WorkDate DATE,
        @ECode    NVARCHAR(50) = NULL,
        @LatePolicyStart DATE = '2025-09-15';

    -- Window: 1st of previous month .. today
    SET @ToDate   = CAST(GETDATE() AS DATE);
    SET @FromDate = DATEFROMPARTS(
                        YEAR(DATEADD(MONTH, -1, @ToDate)),
                        MONTH(DATEADD(MONTH, -1, @ToDate)),
                        1
                    );

    SET @WorkDate = @FromDate;

    ------------------------------------------------------------
    -- Staging reset
    ------------------------------------------------------------
    TRUNCATE TABLE [HRMS].[dbo].[EmployeeDateGrid_temp];

    ------------------------------------------------------------
    -- Constants / policy
    ------------------------------------------------------------
    DECLARE
        @LateAfter    TIME(0) = '08:00:00',
        @GraceTill    TIME(0) = '23:59:00',
        @WeeklyGrace  INT     = 2,
        @FullDayMin   INT     = 510,  -- 8h30m
        @HalfDayMin   INT     = 240;  -- 4h

    ------------------------------------------------------------
    -- Build Holiday base for entire window (STCode + HolidayDate)
    ------------------------------------------------------------
    IF OBJECT_ID('tempdb..#HolidayBase') IS NOT NULL
        DROP TABLE #HolidayBase;

    ;WITH HolidayRaw AS
    (
        SELECT hm.HolidayDate, hm.HolidayName, tl.STCode
        FROM dbo.HolidayMaster hm                WITH (NOLOCK)
        INNER JOIN dbo.LocationTypeMaster ltm    WITH (NOLOCK) ON ltm.Id = hm.LocationType
        INNER JOIN dbo.GroupMaster gm            WITH (NOLOCK) ON gm.Id  = hm.LocationValue
        INNER JOIN dbo.GroupWiseStoreCodeMapping g WITH (NOLOCK) ON g.GroupId = gm.Id
        INNER JOIN dbo.tblLocation tl            WITH (NOLOCK) ON tl.STCode = g.ST_CD
        WHERE hm.HolidayDate BETWEEN @FromDate AND @ToDate
  AND ISNULL(hm.IsDeleted,0) = 0 AND ISNULL(hm.IsActive,1) = 1   -- deleted/inactive holidays must not apply
    ),
    HolidayBase AS
    (
        SELECT r.STCode, r.HolidayDate, MIN(r.HolidayName) AS HolidayName
        FROM HolidayRaw r
        GROUP BY r.STCode, r.HolidayDate
    )
    SELECT hb.STCode, hb.HolidayDate, hb.HolidayName
    INTO #HolidayBase
    FROM HolidayBase hb;

    CREATE NONCLUSTERED INDEX IX_#HolidayBase ON #HolidayBase (STCode, HolidayDate);

    ------------------------------------------------------------
    -- Day-by-day loop
    ------------------------------------------------------------
    WHILE @WorkDate <= @ToDate
    BEGIN
        -- Week anchor: Monday-start but never earlier than the month's 1st
        DECLARE @MondayStart DATE = DATEADD(DAY, 1 - DATEPART(WEEKDAY, @WorkDate), @WorkDate);
        DECLARE @MonthStart  DATE = DATEFROMPARTS(YEAR(@WorkDate), MONTH(@WorkDate), 1);
        DECLARE @WeekStart   DATE = CASE WHEN @MondayStart < @MonthStart THEN @MonthStart ELSE @MondayStart END;

        ;WITH Emp AS
        (
            SELECT  e.EmployeeId,
                    e.ECode,
                    EmployeeName = COALESCE(
                        NULLIF(LTRIM(ISNULL(e.FirstName,'') + ' ' + ISNULL(e.LastName,'')), ''),
                        e.[FULL NAME],
                        'NA'
                    ),
                    dsg.DesignationName,
                    loc.LocationName,
                    loc.STCode,
                    dept.DepartmentName
            FROM dbo.tblEmployee   e   WITH (NOLOCK)
            LEFT JOIN dbo.tblDesignation dsg WITH (NOLOCK) ON dsg.DesignationId = e.DesignationId
            LEFT JOIN dbo.tblLocation    loc WITH (NOLOCK) ON loc.LocationId    = e.LocationId
            LEFT JOIN dbo.tblDepartment  dept WITH (NOLOCK) ON dept.DepartmentId = e.DepartmentId
            WHERE (@ECode IS NULL OR e.ECode = @ECode)
        ),
        Grid AS
        (
            SELECT  e.*,
                    @WorkDate AS PunchDate,
                    CASE WHEN DATEPART(WEEKDAY, @WorkDate) IN (6,7) THEN 1 ELSE 0 END AS IsWeekend, -- Sat/Sun with DATEFIRST=1
                    CASE WHEN @WorkDate > CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END AS IsFuture
            FROM Emp e
        ),
        P AS
        (
            SELECT  e.EmployeeId, e.ECode, p.PunchDate,
                    p.Punch1,p.Punch2,p.Punch3,p.Punch4,p.Punch5,p.Punch6,
                    p.Punch7,p.Punch8,p.Punch9,p.Punch10,p.Punch11,p.Punch12,
                    p.RegularizePunchIn, p.RegularizePuncOut, p.IsRegularize,
                    p.Machine_Type
            FROM  Emp e
            LEFT JOIN dbo.tblEmployeeMultiPunches p WITH (NOLOCK)
                   ON p.UserID   = e.ECode
                  AND p.PunchDate = @WorkDate
        ),
        AggBase AS
        (
            SELECT  EmployeeId, ECode, PunchDate,
                    STRING_AGG(Machine_Type, ',') AS Machine_Type,
                    MAX(CAST(IsRegularize AS TINYINT)) AS IsRegularize,
                    MAX(RegularizePunchIn) AS RegularizePunchIn,
                    MAX(RegularizePuncOut) AS RegularizePuncOut,
                    MAX(Punch1)  AS Punch1,  MAX(Punch2)  AS Punch2,
                    MAX(Punch3)  AS Punch3,  MAX(Punch4)  AS Punch4,
                    MAX(Punch5)  AS Punch5,  MAX(Punch6)  AS Punch6,
                    MAX(Punch7)  AS Punch7,  MAX(Punch8)  AS Punch8,
                    MAX(Punch9)  AS Punch9,  MAX(Punch10) AS Punch10,
                    MAX(Punch11) AS Punch11, MAX(Punch12) AS Punch12
            FROM P
            GROUP BY EmployeeId, ECode, PunchDate
        ),
        Agg AS
        (
            SELECT  ab.*,
                    v.PunchCnt,
                    COALESCE(NULLIF(ab.RegularizePunchIn,'00:00:00'), v.FirstIn,  '00:00:00') AS PunchIn,
                    COALESCE(NULLIF(ab.RegularizePuncOut,'00:00:00'), v.LastOut, '00:00:00') AS PunchOut
            FROM AggBase ab
            CROSS APPLY
            (
                SELECT 
                    SUM(CASE WHEN t.p IS NOT NULL AND t.p <> '00:00:00' THEN 1 ELSE 0 END) AS PunchCnt,
                    MIN(CASE WHEN t.p IS NOT NULL AND t.p <> '00:00:00' THEN t.p END)      AS FirstIn,
                    MAX(CASE WHEN t.p IS NOT NULL AND t.p <> '00:00:00' THEN t.p END)      AS LastOut
                FROM (VALUES
                    (ab.Punch1),(ab.Punch2),(ab.Punch3),(ab.Punch4),(ab.Punch5),(ab.Punch6),
                    (ab.Punch7),(ab.Punch8),(ab.Punch9),(ab.Punch10),(ab.Punch11),(ab.Punch12)
                ) AS v(p)
                CROSS APPLY (SELECT v.p) AS t(p)
            ) v
        ),
        DayMin AS
        (
            SELECT  a.EmployeeId, a.PunchDate, a.PunchCnt,
                    WorkMin =
                    CASE
                        WHEN a.IsRegularize = 1
                             AND a.RegularizePunchIn  <> '00:00:00'
                             AND a.RegularizePuncOut  <> '00:00:00'
                        THEN DATEDIFF(MINUTE, a.RegularizePunchIn, a.RegularizePuncOut)
                        WHEN a.PunchCnt % 2 = 0 THEN
                              COALESCE(DATEDIFF(MINUTE, a.Punch1,  a.Punch2),  0)
                            + COALESCE(DATEDIFF(MINUTE, a.Punch3,  a.Punch4),  0)
                            + COALESCE(DATEDIFF(MINUTE, a.Punch5,  a.Punch6),  0)
                            + COALESCE(DATEDIFF(MINUTE, a.Punch7,  a.Punch8),  0)
                            + COALESCE(DATEDIFF(MINUTE, a.Punch9,  a.Punch10), 0)
                            + COALESCE(DATEDIFF(MINUTE, a.Punch11, a.Punch12), 0)
                        ELSE 0
                    END
            FROM Agg a
        ),
        Holi AS
        (
            SELECT g.EmployeeId, g.PunchDate, 1 AS IsHoliday, hb.HolidayName
            FROM Grid g
            INNER JOIN #HolidayBase hb
                ON hb.STCode = g.STCode
               AND hb.HolidayDate = g.PunchDate
        ),
        LateRaw AS
        (
            SELECT  g.EmployeeId,
                    g.PunchDate,
                    @WeekStart AS WeekStart,
                    dm.WorkMin,
                    g.IsFuture,
                    g.IsWeekend,
                    ISNULL(h.IsHoliday,0) AS IsHoliday,
                    CAST(CASE 
                            WHEN a.IsRegularize = 1 AND a.RegularizePunchIn <> '00:00:00'
                            THEN a.RegularizePunchIn 
                            ELSE ISNULL(a.PunchIn, '00:00:00') 
                         END AS TIME(0)) AS InTime,
                    -- LATE only on eligible full-day, non-holiday, after policy start
                    CASE 
                      WHEN g.PunchDate > @LatePolicyStart
                           AND ISNULL(h.IsHoliday,0) = 0
                           AND (
                                (a.IsRegularize = 1 AND a.RegularizePunchIn <> '00:00:00' AND a.RegularizePuncOut <> '00:00:00')
                                OR dm.WorkMin >= @FullDayMin
                               )
                           AND CAST(
                                CASE WHEN a.IsRegularize = 1 AND a.RegularizePunchIn <> '00:00:00' 
                                     THEN a.RegularizePunchIn
                                     ELSE ISNULL(a.PunchIn,'00:00:00') END
                               AS TIME(0)
                           ) > @LateAfter
                      THEN 1 ELSE 0
                    END AS IsLate,
                    CASE 
                      WHEN g.PunchDate > @LatePolicyStart
                           AND ISNULL(h.IsHoliday,0) = 0
                           AND (
                                (a.IsRegularize = 1 AND a.RegularizePunchIn <> '00:00:00' AND a.RegularizePuncOut <> '00:00:00')
                                OR dm.WorkMin >= @FullDayMin
                               )
                           AND CAST(
                                CASE WHEN a.IsRegularize = 1 AND a.RegularizePunchIn <> '00:00:00' 
                                     THEN a.RegularizePunchIn
                                     ELSE ISNULL(a.PunchIn,'00:00:00') END
                               AS TIME(0)
                           ) > @GraceTill
                      THEN 1 ELSE 0
                    END AS IsBeyondGrace
            FROM  Grid g
            LEFT JOIN Agg    a  ON a.EmployeeId = g.EmployeeId AND a.PunchDate = g.PunchDate
            LEFT JOIN DayMin dm ON dm.EmployeeId = g.EmployeeId AND dm.PunchDate = g.PunchDate
            LEFT JOIN Holi   h  ON h.EmployeeId = g.EmployeeId AND h.PunchDate = g.PunchDate
        )
        INSERT INTO [HRMS].[dbo].[EmployeeDateGrid_temp] (
            EmpAttendanceId, EmployeeId, EmployeeName, ECode, AttendanceDate, Machine_Type,
            DesignationName, LocationName, STCode, DepartmentName,
            Punch1, Punch2, Punch3, Punch4, Punch5, Punch6,
            Punch7, Punch8, Punch9, Punch10, Punch11, Punch12,
            PunchIn, PunchOut, ValidPunchCount,
            RegularizePunchIn, RegularizePuncOut, IsRegularize,
            IsOnLeave, TotalWorkingMinutes, LateMinutes, EarlyMinutes,
            Status, TotalWorkingDays, TotalMonthlyWorkingHours
        )
        SELECT
            0 AS EmpAttendanceId,
            g.EmployeeId, g.EmployeeName, g.ECode,
            g.PunchDate AS AttendanceDate,
            ISNULL(a.Machine_Type,'') AS Machine_Type,
            g.DesignationName, g.LocationName, g.STCode, g.DepartmentName,
            a.Punch1,a.Punch2,a.Punch3,a.Punch4,a.Punch5,a.Punch6,
            a.Punch7,a.Punch8,a.Punch9,a.Punch10,a.Punch11,a.Punch12,
            ISNULL(a.PunchIn,'00:00:00')  AS PunchIn,
            ISNULL(a.PunchOut,'00:00:00') AS PunchOut,
            ISNULL(a.PunchCnt,0)          AS ValidPunchCount,
            ISNULL(a.RegularizePunchIn,'00:00:00')  AS RegularizePunchIn,
            ISNULL(a.RegularizePuncOut,'00:00:00')  AS RegularizePuncOut,
            CAST(ISNULL(a.IsRegularize,0) AS BIT)   AS IsRegularize,

            -- On Leave (for the day)
            CASE WHEN EXISTS (SELECT 1
                              FROM dbo.tblLeaveRequest lr WITH (NOLOCK)
                              WHERE lr.EmployeeId = g.EmployeeId
                                AND lr.IsRevoked = 0
                                AND lr.StatusId  = 1
                                AND g.PunchDate BETWEEN lr.StartDate AND lr.EndDate)
                 THEN 1 ELSE 0 END AS IsOnLeave,

            -- TotalWorkingMinutes (zero for holiday/off/invalid)
            CASE
              WHEN lrw.IsHoliday = 1 THEN '0 hours and 00 minutes'
              WHEN g.IsFuture = 1 THEN '0 hours and 00 minutes'
              WHEN g.IsWeekend = 1 AND a.EmployeeId IS NULL THEN '0 hours and 00 minutes'
              WHEN dm.WorkMin IS NULL THEN '0 hours and 00 minutes'
              ELSE CAST(dm.WorkMin/60 AS VARCHAR(10)) + ' hours and ' +
                   RIGHT('0' + CAST(dm.WorkMin%60 AS VARCHAR(2)), 2) + ' minutes'
            END AS TotalWorkingMinutes,

            -- Late / Early minutes: zero on holiday or ineligible (not full day/regularized)
            CASE
              WHEN lrw.IsHoliday = 1 THEN 0
              WHEN g.PunchDate <= @LatePolicyStart THEN 0
              WHEN a.EmployeeId IS NULL THEN 0
              WHEN ( (a.IsRegularize = 1 AND a.RegularizePunchIn <> '00:00:00' AND a.RegularizePuncOut <> '00:00:00')
                     OR dm.WorkMin >= @FullDayMin )
                   AND a.IsRegularize = 1 AND a.RegularizePunchIn > @LateAfter
                   THEN DATEDIFF(MINUTE, @LateAfter, a.RegularizePunchIn)
              WHEN ( (a.IsRegularize = 1 AND a.RegularizePunchIn <> '00:00:00' AND a.RegularizePuncOut <> '00:00:00')
                     OR dm.WorkMin >= @FullDayMin )
                   AND a.IsRegularize = 0 AND a.PunchIn > @LateAfter
                   THEN DATEDIFF(MINUTE, @LateAfter, a.PunchIn)
              ELSE 0
            END AS LateMinutes,

            CASE
              WHEN lrw.IsHoliday = 1 THEN 0
              WHEN g.PunchDate <= @LatePolicyStart THEN 0
              WHEN a.EmployeeId IS NULL THEN 0
              WHEN ( (a.IsRegularize = 1 AND a.RegularizePunchIn <> '00:00:00' AND a.RegularizePuncOut <> '00:00:00')
                     OR dm.WorkMin >= @FullDayMin )
                   AND a.IsRegularize = 1 AND a.RegularizePunchIn < @LateAfter
                   THEN DATEDIFF(MINUTE, a.RegularizePunchIn, @LateAfter)
              WHEN ( (a.IsRegularize = 1 AND a.RegularizePunchIn <> '00:00:00' AND a.RegularizePuncOut <> '00:00:00')
                     OR dm.WorkMin >= @FullDayMin )
                   AND a.IsRegularize = 0 AND a.PunchIn < @LateAfter
                   THEN DATEDIFF(MINUTE, a.PunchIn, @LateAfter)
              ELSE 0
            END AS EarlyMinutes,

            -- Status (Holiday precedence; weekly grace respects exclusions)
            CASE
              WHEN lrw.IsHoliday = 1 THEN 'Holiday'
              WHEN g.IsFuture = 1 THEN ''

              WHEN EXISTS (SELECT 1 FROM dbo.tblLeaveRequest lr WITH (NOLOCK)
                           WHERE lr.EmployeeId = g.EmployeeId
                             AND lr.IsRevoked = 0
                             AND lr.StatusId  = 1
                             AND g.PunchDate BETWEEN lr.StartDate AND lr.EndDate)
                   THEN 'On Leave'

              WHEN g.IsWeekend = 1 AND a.EmployeeId IS NULL THEN 'Weekly Off'

              WHEN a.IsRegularize = 1
                   AND a.RegularizePunchIn <> '00:00:00'
                   AND a.RegularizePuncOut <> '00:00:00'
                   THEN 'Manual Present'

              WHEN a.PunchCnt % 2 = 1 THEN 'Missed Punch'

              -- Weekly grace override (only after policy start, and ignoring Holiday/Absent/Half Day)
              WHEN g.PunchDate > @LatePolicyStart
                   AND (
                       -- Force half-day if beyond grace on the day (only if eligible; IsBeyondGrace already respects that)
                       CASE WHEN lrw.IsBeyondGrace = 1 THEN 1 ELSE 0 END
                       -- Or late and past grace count this week (prior eligible lates + today)
                       + CASE 
                           WHEN lrw.IsLate = 1 AND (
                                (
                                  SELECT COUNT(*)
                                  FROM [HRMS].[dbo].[EmployeeDateGrid_temp] x
                                  WHERE x.ECode = g.ECode
                                    AND x.AttendanceDate >= @WeekStart
                                    AND x.AttendanceDate <  g.PunchDate   -- strictly before today
                                    AND x.AttendanceDate >  @LatePolicyStart
                                    -- exclude Holiday/Absent/Half Day from late counting
                                    AND x.Status NOT IN ('Holiday','Absent','Half Day Absent')
                                    -- eligible full-day
                                    AND (
                                         (x.IsRegularize = 1 AND x.RegularizePunchIn <> '00:00:00' AND x.RegularizePuncOut <> '00:00:00')
                                         OR (
                                             CASE WHEN x.ValidPunchCount % 2 = 0 THEN
                                                 COALESCE(DATEDIFF(MINUTE, x.Punch1,  x.Punch2),  0) +
                                                 COALESCE(DATEDIFF(MINUTE, x.Punch3,  x.Punch4),  0) +
                                                 COALESCE(DATEDIFF(MINUTE, x.Punch5,  x.Punch6),  0) +
                                                 COALESCE(DATEDIFF(MINUTE, x.Punch7,  x.Punch8),  0) +
                                                 COALESCE(DATEDIFF(MINUTE, x.Punch9,  x.Punch10), 0) +
                                                 COALESCE(DATEDIFF(MINUTE, x.Punch11, x.Punch12), 0)
                                             ELSE 0 END
                                         ) >= @FullDayMin
                                    )
                                    -- reconstruct 'late' check
                                    AND (
                                         (x.IsRegularize = 1 AND x.RegularizePunchIn > @LateAfter) OR
                                         (x.IsRegularize = 0 AND x.PunchIn          > @LateAfter)
                                    )
                                )
                                + 1  -- include today (lrw.IsLate = 1)
                              ) > @WeeklyGrace
                         THEN 1 ELSE 0 END
                     ) = 1
                   THEN CASE WHEN dm.WorkMin >= @HalfDayMin THEN 'Half Day Absent' ELSE 'Absent' END

              -- Normal thresholds
              WHEN dm.WorkMin >= @FullDayMin THEN 'Present'
              WHEN dm.WorkMin >= @HalfDayMin THEN 'Half Day Absent'
              ELSE 'Absent'
            END AS Status,

            -- Filled after loop via UPDATE (placeholders for now)
            CAST(NULL AS FLOAT)       AS TotalWorkingDays,
            CAST(NULL AS VARCHAR(50)) AS TotalMonthlyWorkingHours
        FROM  Grid g
        LEFT JOIN Agg      a   ON a.EmployeeId = g.EmployeeId AND a.PunchDate = g.PunchDate
        LEFT JOIN DayMin   dm  ON dm.EmployeeId = g.EmployeeId AND dm.PunchDate = g.PunchDate
        LEFT JOIN LateRaw  lrw ON lrw.EmployeeId = g.EmployeeId AND lrw.PunchDate = g.PunchDate;

        -- next day
        SET @WorkDate = DATEADD(DAY, 1, @WorkDate);
    END

    ------------------------------------------------------------
    -- Monthly aggregates (TotalMonthlyWorkingHours / TotalWorkingDays)
    ------------------------------------------------------------
    ;WITH WorkMinPerDay AS
    (
        SELECT 
            ECode,
            AttendanceDate,
            WorkMin = 
                CASE
                    WHEN Status = 'Holiday' THEN 0
                    WHEN IsRegularize = 1
                         AND RegularizePunchIn <> '00:00:00'
                         AND RegularizePuncOut <> '00:00:00'
                    THEN DATEDIFF(MINUTE, RegularizePunchIn, RegularizePuncOut)
                    WHEN ValidPunchCount % 2 = 0 THEN
                          COALESCE(DATEDIFF(MINUTE, Punch1,  Punch2),  0)
                        + COALESCE(DATEDIFF(MINUTE, Punch3,  Punch4),  0)
                        + COALESCE(DATEDIFF(MINUTE, Punch5,  Punch6),  0)
                        + COALESCE(DATEDIFF(MINUTE, Punch7,  Punch8),  0)
                        + COALESCE(DATEDIFF(MINUTE, Punch9,  Punch10), 0)
                        + COALESCE(DATEDIFF(MINUTE, Punch11, Punch12), 0)
                    ELSE 0
                END
        FROM [HRMS].[dbo].[EmployeeDateGrid_temp]
    ),
    SumByEmp AS
    (
        SELECT ECode, SUM(WorkMin) AS TotalMin
        FROM WorkMinPerDay
        GROUP BY ECode
    ),
    DayCredit AS
    (
        SELECT
            t.ECode,
            t.AttendanceDate,
            WorkDayValue =
            CASE
                WHEN t.AttendanceDate > CAST(GETDATE() AS DATE) THEN 0.0
                WHEN t.Status = 'Holiday' THEN 0.0
                WHEN t.Status = 'On Leave' THEN 0.0
                WHEN t.Status = 'Weekly Off' AND t.ValidPunchCount = 0 THEN 0.0
                WHEN t.Status = 'Manual Present' THEN 1.0
                WHEN t.ValidPunchCount % 2 = 1 THEN 0.0
                WHEN t.Status = 'Absent' THEN 0.0
                WHEN t.Status = 'Half Day Absent' THEN 0.5
                WHEN t.Status = 'Present' THEN 1.0
                ELSE 0.0
            END
        FROM [HRMS].[dbo].[EmployeeDateGrid_temp] t
    ),
    SumDayCredit AS
    (
        SELECT ECode, SUM(WorkDayValue) AS TotalWorkingDays
        FROM DayCredit
        GROUP BY ECode
    )
    UPDATE t
      SET t.TotalMonthlyWorkingHours = 
            CAST(s.TotalMin/60 AS VARCHAR(10)) + ' hours and ' +
            RIGHT('0' + CAST(s.TotalMin%60 AS VARCHAR(2)), 2) + ' minutes',
          t.TotalWorkingDays = sdc.TotalWorkingDays
    FROM [HRMS].[dbo].[EmployeeDateGrid_temp] t
    LEFT JOIN SumByEmp     s   ON s.ECode  = t.ECode
    LEFT JOIN SumDayCredit sdc ON sdc.ECode = t.ECode;

    ------------------------------------------------------------
    -- Helpful index for the merge (if not already there)
    ------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM sys.indexes 
                   WHERE object_id = OBJECT_ID('[HRMS].[dbo].[EmployeeDateGrid_temp]')
                     AND name = 'IX_EmployeeDateGrid_temp_EC_AttDate')
    BEGIN
        CREATE NONCLUSTERED INDEX IX_EmployeeDateGrid_temp_EC_AttDate
            ON [HRMS].[dbo].[EmployeeDateGrid_temp] (ECode, AttendanceDate);
    END

    ------------------------------------------------------------
    -- Upsert into final table
    ------------------------------------------------------------
    MERGE [HRMS].[dbo].[EmployeeDateGrid] WITH (HOLDLOCK) AS TARGET
    USING [HRMS].[dbo].[EmployeeDateGrid_temp] AS SOURCE
      ON TARGET.ECode = SOURCE.ECode
     AND TARGET.AttendanceDate = SOURCE.AttendanceDate
    WHEN MATCHED THEN
        UPDATE SET
            TARGET.EmployeeId               = SOURCE.EmployeeId,
            TARGET.EmployeeName             = SOURCE.EmployeeName,
            TARGET.Machine_Type             = SOURCE.Machine_Type,
            TARGET.DesignationName          = SOURCE.DesignationName,
            TARGET.LocationName             = SOURCE.LocationName,
            TARGET.STCode                   = SOURCE.STCode,
            TARGET.DepartmentName           = SOURCE.DepartmentName,
            TARGET.Punch1                   = SOURCE.Punch1,
            TARGET.Punch2                   = SOURCE.Punch2,
            TARGET.Punch3                   = SOURCE.Punch3,
            TARGET.Punch4                   = SOURCE.Punch4,
            TARGET.Punch5                   = SOURCE.Punch5,
            TARGET.Punch6                   = SOURCE.Punch6,
            TARGET.Punch7                   = SOURCE.Punch7,
            TARGET.Punch8                   = SOURCE.Punch8,
            TARGET.Punch9                   = SOURCE.Punch9,
            TARGET.Punch10                  = SOURCE.Punch10,
            TARGET.Punch11                  = SOURCE.Punch11,
            TARGET.Punch12                  = SOURCE.Punch12,
            TARGET.PunchIn                  = SOURCE.PunchIn,
            TARGET.PunchOut                 = SOURCE.PunchOut,
            TARGET.ValidPunchCount          = SOURCE.ValidPunchCount,
            TARGET.RegularizePunchIn        = SOURCE.RegularizePunchIn,
            TARGET.RegularizePuncOut        = SOURCE.RegularizePuncOut,
            TARGET.IsRegularize             = SOURCE.IsRegularize,
            TARGET.IsOnLeave                = SOURCE.IsOnLeave,
            TARGET.TotalWorkingMinutes      = SOURCE.TotalWorkingMinutes,
            TARGET.LateMinutes              = SOURCE.LateMinutes,
            TARGET.EarlyMinutes             = SOURCE.EarlyMinutes,
            TARGET.Status                   = SOURCE.Status,
            TARGET.TotalWorkingDays         = SOURCE.TotalWorkingDays,
            TARGET.TotalMonthlyWorkingHours = SOURCE.TotalMonthlyWorkingHours
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (
            EmpAttendanceId, EmployeeId, EmployeeName, ECode, AttendanceDate, Machine_Type,
            DesignationName, LocationName, STCode, DepartmentName,
            Punch1, Punch2, Punch3, Punch4, Punch5, Punch6,
            Punch7, Punch8, Punch9, Punch10, Punch11, Punch12,
            PunchIn, PunchOut, ValidPunchCount,
            RegularizePunchIn, RegularizePuncOut, IsRegularize,
            IsOnLeave, TotalWorkingMinutes, LateMinutes, EarlyMinutes,
            Status, TotalWorkingDays, TotalMonthlyWorkingHours
        )
        VALUES (
            SOURCE.EmpAttendanceId, SOURCE.EmployeeId, SOURCE.EmployeeName, SOURCE.ECode, SOURCE.AttendanceDate, SOURCE.Machine_Type,
            SOURCE.DesignationName, SOURCE.LocationName, SOURCE.STCode, SOURCE.DepartmentName,
            SOURCE.Punch1, SOURCE.Punch2, SOURCE.Punch3, SOURCE.Punch4, SOURCE.Punch5, SOURCE.Punch6,
            SOURCE.Punch7, SOURCE.Punch8, SOURCE.Punch9, SOURCE.Punch10, SOURCE.Punch11, SOURCE.Punch12,
            SOURCE.PunchIn, SOURCE.PunchOut, SOURCE.ValidPunchCount,
            SOURCE.RegularizePunchIn, SOURCE.RegularizePuncOut, SOURCE.IsRegularize,
            SOURCE.IsOnLeave, SOURCE.TotalWorkingMinutes, SOURCE.LateMinutes, SOURCE.EarlyMinutes,
            SOURCE.Status, SOURCE.TotalWorkingDays, SOURCE.TotalMonthlyWorkingHours
        )
    ;
END
