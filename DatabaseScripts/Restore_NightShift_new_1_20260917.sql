ALTER PROCEDURE [dbo].[usp_GetMonthlyPunchesRange_Optimized_new_1] --  '2026-02-26','2026-03-25','v41797'        
(        
    @FromDate DATE,        
    @ToDate   DATE,        
    @ECode    NVARCHAR(50) = NULL        
)        
AS        
BEGIN        
    SET NOCOUNT ON;        
        
    IF @FromDate IS NULL OR @ToDate IS NULL OR @FromDate > @ToDate        
    BEGIN        
        RAISERROR('Invalid date range', 16, 1);        
        RETURN;        
    END;        
        
    DECLARE @Today DATE = CAST(GETDATE() AS DATE);        
        
    /* NEW: extend range by 1 day to support cross-midnight + BOTH merge window */        
    DECLARE @ToDateEx DATE = DATEADD(DAY, 1, @ToDate);        
        
    /* ===== Day/Half-day thresholds (keep minutes logic consistent everywhere) ===== */        
    DECLARE @FullDayMinutes INT = 510;        
    DECLARE @HalfDayMinutes INT = 240;    -- olv value 240    
        
    /*==========================================================        
      1) Employees        
    ==========================================================*/        
    IF OBJECT_ID('tempdb..#Emp') IS NOT NULL DROP TABLE #Emp;        
        
    SELECT        
        e.EmployeeId,        
        e.ECode,        
        EmployeeName = CASE        
            WHEN e.FirstName IS NULL AND e.[FULL NAME] IS NULL THEN 'NA'        
            WHEN e.FirstName IS NULL THEN ISNULL(e.[FULL NAME], 'NA')        
            ELSE LTRIM(ISNULL(e.FirstName,'') + CASE WHEN e.LastName IS NOT NULL THEN ' ' + e.LastName ELSE '' END)        
        END,        
        e.LocationId,        
        e.ShiftID AS CurrentShiftID,        
        dsg.DesignationName,        
        loc.LocationName,        
        loc.STCode,        
        dept.DepartmentName        
    INTO #Emp        
    FROM dbo.tblEmployee e WITH (NOLOCK)        
    LEFT JOIN dbo.tblDesignation dsg WITH (NOLOCK) ON e.DesignationId = dsg.DesignationId        
    LEFT JOIN dbo.tblLocation   loc WITH (NOLOCK) ON e.LocationId   = loc.LocationId        
    LEFT JOIN dbo.tblDepartment dept WITH (NOLOCK) ON e.DepartmentId = dept.DepartmentId        
    WHERE (@ECode IS NULL OR e.ECode = @ECode);        
        
    CREATE UNIQUE CLUSTERED INDEX IX_#Emp ON #Emp(EmployeeId);        
    CREATE INDEX IX_#Emp_ECode ON #Emp(ECode);        
        
    /*==========================================================        
      2) Dates        
    ==========================================================*/        
    IF OBJECT_ID('tempdb..#Dates') IS NOT NULL DROP TABLE #Dates;        
        
    ;WITH n AS        
    (        
        SELECT TOP (DATEDIFF(DAY, @FromDate, @ToDate) + 1)        
               ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS n        
        FROM sys.all_objects        
    )        
    SELECT DATEADD(DAY, n.n, @FromDate) AS PunchDate        
    INTO #Dates        
    FROM n;        
        
    CREATE UNIQUE CLUSTERED INDEX IX_#Dates ON #Dates(PunchDate);        
        
    /*==========================================================        
      3) Holidays        
    ==========================================================*/        
    IF OBJECT_ID('tempdb..#Holiday') IS NOT NULL DROP TABLE #Holiday;        
        
    SELECT        
        tl.STCode,        
        hm.HolidayDate,        
        HolidayName = MIN(hm.HolidayName)        
    INTO #Holiday        
    FROM dbo.HolidayMaster hm WITH (NOLOCK)        
    INNER JOIN dbo.LocationTypeMaster ltm WITH (NOLOCK) ON ltm.Id = hm.LocationType        
    INNER JOIN dbo.GroupMaster gm WITH (NOLOCK) ON gm.Id = hm.LocationValue        
    INNER JOIN dbo.GroupWiseStoreCodeMapping g WITH (NOLOCK) ON g.GroupId = gm.Id        
    INNER JOIN dbo.tblLocation tl WITH (NOLOCK) ON tl.STCode = g.ST_CD        
    WHERE hm.HolidayDate >= @FromDate AND hm.HolidayDate <= @ToDate
      AND ISNULL(hm.IsDeleted,0) = 0 AND ISNULL(hm.IsActive,1) = 1   -- deleted/inactive holidays must not apply        
    GROUP BY tl.STCode, hm.HolidayDate;        
        
    CREATE CLUSTERED INDEX IX_#Holiday ON #Holiday(STCode, HolidayDate);        
        
 /*==========================================================        
      4) Leave Days        
    ==========================================================*/        
    IF OBJECT_ID('tempdb..#LeaveDays') IS NOT NULL DROP TABLE #LeaveDays;        
        
    SELECT DISTINCT        
        l.EmployeeId,        
        d.PunchDate        
    INTO #LeaveDays        
    FROM dbo.tblLeaveRequest l WITH (NOLOCK)        
   JOIN #Emp e   ON e.EmployeeId = l.EmployeeId        
    JOIN #Dates d ON d.PunchDate BETWEEN l.StartDate AND l.EndDate        
    WHERE l.IsRevoked = 0        
      AND l.StatusId IN (1)        
      AND l.StartDate <= @ToDate        
      AND l.EndDate   >= @FromDate;        
        
    CREATE CLUSTERED INDEX IX_#LeaveDays ON #LeaveDays(EmployeeId, PunchDate);        
        
    /*==========================================================        
      5) Shift History sliced to range        
    ==========================================================*/        
    IF OBJECT_ID('tempdb..#ShiftHist') IS NOT NULL DROP TABLE #ShiftHist;        
        
    SELECT        
        h.EmployeeId,        
        h.ShiftId,        
        h.EffectiveFrom,        
        EffectiveTo = ISNULL(h.EffectiveTo, CONVERT(date,'9999-12-31')),        
        h.HistoryId        
    INTO #ShiftHist        
    FROM dbo.EmployeeShiftHistory h WITH (NOLOCK)        
    JOIN #Emp e ON e.EmployeeId = h.EmployeeId        
    WHERE h.EffectiveFrom <= @ToDate        
      AND ISNULL(h.EffectiveTo, CONVERT(date,'9999-12-31')) >= @FromDate;        
        
    CREATE INDEX IX_#ShiftHist_EmpFrom        
      ON #ShiftHist(EmployeeId, EffectiveFrom DESC, HistoryId DESC)        
      INCLUDE (EffectiveTo, ShiftId);        
        
    /*==========================================================        
      6) Employee-Date Grid        
    ==========================================================*/        
    IF OBJECT_ID('tempdb..#EDG') IS NOT NULL DROP TABLE #EDG;        
        
    SELECT        
        e.EmployeeId, e.ECode, e.EmployeeName,        
        e.DesignationName, e.LocationName, e.STCode, e.DepartmentName, e.LocationId,        
        d.PunchDate,        
        
        ShiftID = COALESCE(sh.ShiftId, e.CurrentShiftID),        
        sm.ShiftName,        
        ShiftStartTime = COALESCE(CAST(sm.StartTime AS TIME(0)), CAST('08:00:00' AS TIME(0))),        
        ShiftEndTime   = CAST(sm.EndTime AS TIME(0)),        
        
        HasShift = CASE WHEN COALESCE(sh.ShiftId, e.CurrentShiftID) IS NULL THEN 0 ELSE 1 END,        
        IsFlexibleShift = CASE WHEN COALESCE(sh.ShiftId, e.CurrentShiftID) = 19 OR sm.ShiftName = 'Flexible Shift' THEN 1 ELSE 0 END,        
        IsCrossMidnight = CASE        
            WHEN COALESCE(sh.ShiftId, e.CurrentShiftID) IS NOT NULL        
             AND CAST(sm.StartTime AS TIME(0)) > CAST(sm.EndTime AS TIME(0)) THEN 1 ELSE 0 END,        
        
        IsWeekend    = CASE WHEN DATEPART(WEEKDAY, d.PunchDate) IN (1,7) THEN 1 ELSE 0 END,        
        IsFutureDate = CASE WHEN d.PunchDate > @Today THEN 1 ELSE 0 END,        
        
        IsHoliday    = CASE WHEN h.HolidayDate IS NULL THEN 0 ELSE 1 END,        
        HolidayName  = ISNULL(h.HolidayName,'')        
    INTO #EDG        
    FROM #Emp e        
    CROSS JOIN #Dates d        
    OUTER APPLY        
    (        
        SELECT TOP 1 s.ShiftId        
        FROM #ShiftHist s        
        WHERE s.EmployeeId = e.EmployeeId        
          AND d.PunchDate >= s.EffectiveFrom        
          AND d.PunchDate <= s.EffectiveTo        
        ORDER BY s.EffectiveFrom DESC, s.HistoryId DESC        
    ) sh        
    LEFT JOIN dbo.tblShiftMaster sm WITH (NOLOCK)        
      ON sm.ShiftID = COALESCE(sh.ShiftId, e.CurrentShiftID)        
    LEFT JOIN #Holiday h        
      ON h.STCode = e.STCode AND h.HolidayDate = d.PunchDate;        
        
    CREATE CLUSTERED INDEX IX_#EDG ON #EDG(EmployeeId, PunchDate);        
        
    /*==========================================================        
      7) PunchAgg  (EXTENDED to @ToDateEx for cross-midnight support)        
    ==========================================================*/        
    IF OBJECT_ID('tempdb..#PunchAgg') IS NOT NULL DROP TABLE #PunchAgg;        
        
    SELECT        
        e.EmployeeId,        
        p.UserID AS ECode,        
     p.PunchDate,        
        Machine_Type = STRING_AGG(ISNULL(p.Machine_Type,''), ','),        
        
        Punch1  = MAX(p.Punch1),  Punch2  = MAX(p.Punch2),        
        Punch3  = MAX(p.Punch3),  Punch4  = MAX(p.Punch4),        
        Punch5  = MAX(p.Punch5),  Punch6  = MAX(p.Punch6),        
        Punch7  = MAX(p.Punch7),  Punch8  = MAX(p.Punch8),        
        Punch9  = MAX(p.Punch9),  Punch10 = MAX(p.Punch10),        
        Punch11 = MAX(p.Punch11), Punch12 = MAX(p.Punch12),        
        
        RegularizePunchIn = MAX(p.RegularizePunchIn),        
        RegularizePuncOut = MAX(p.RegularizePuncOut),        
        IsRegularize      = MAX(CAST(p.IsRegularize AS INT))        
    INTO #PunchAgg        
    FROM dbo.tblEmployeeMultiPunches p WITH (NOLOCK)        
    JOIN #Emp e ON e.ECode = p.UserID        
    WHERE p.PunchDate >= @FromDate AND p.PunchDate <= @ToDateEx        
    GROUP BY e.EmployeeId, p.UserID, p.PunchDate;        
        
    CREATE CLUSTERED INDEX IX_#PunchAgg ON #PunchAgg(EmployeeId, PunchDate);        
        
    /*==========================================================        
      8) PunchCalc  (Night shift helpers included)        
    ==========================================================*/        
    IF OBJECT_ID('tempdb..#PunchCalc') IS NOT NULL DROP TABLE #PunchCalc;        
        
    SELECT        
        pa.*,        
        calc.PunchCount,        
        
        HasRegularizePair =        
            CASE WHEN pa.IsRegularize = 1        
                      AND NULLIF(pa.RegularizePunchIn,'00:00:00') IS NOT NULL        
                      AND NULLIF(pa.RegularizePuncOut,'00:00:00') IS NOT NULL        
                      AND DATEDIFF(MINUTE, pa.RegularizePunchIn, pa.RegularizePuncOut) > 0        
                 THEN 1 ELSE 0 END,        
        
        EffectivePunchCount =        
            CASE WHEN pa.IsRegularize = 1        
                      AND NULLIF(pa.RegularizePunchIn,'00:00:00') IS NOT NULL        
                      AND NULLIF(pa.RegularizePuncOut,'00:00:00') IS NOT NULL        
                      AND DATEDIFF(MINUTE, pa.RegularizePunchIn, pa.RegularizePuncOut) > 0        
                 THEN 2        
                 ELSE calc.PunchCount        
            END,        
        
        PunchIn = CASE        
            WHEN pa.IsRegularize = 1 AND NULLIF(pa.RegularizePunchIn,'00:00:00') IS NOT NULL        
            THEN pa.RegularizePunchIn        
            ELSE ISNULL(calc.MinPunch, '00:00:00')        
        END,        
        PunchOut = CASE        
            WHEN pa.IsRegularize = 1 AND NULLIF(pa.RegularizePuncOut,'00:00:00') IS NOT NULL        
            THEN pa.RegularizePuncOut        
            ELSE ISNULL(calc.MaxPunch, '00:00:00')        
        END,        
        
        /* Night Shift helpers */        
        EveningFirstPunch = ISNULL(calc.EveningMinPunch, calc.MinPunch),        
        MorningFirstPunch = calc.MorningMinPunch,        
        MorningLastPunch  = calc.MorningMaxPunch        
    INTO #PunchCalc        
    FROM #PunchAgg pa        
    CROSS APPLY        
    (        
        SELECT        
            PunchCount = SUM(CASE WHEN v.p IS NOT NULL AND v.p <> '00:00:00' THEN 1 ELSE 0 END),        
            MinPunch   = MIN(CASE WHEN v.p IS NOT NULL AND v.p <> '00:00:00' THEN v.p END),        
            MaxPunch   = MAX(CASE WHEN v.p IS NOT NULL AND v.p <> '00:00:00' THEN v.p END),        
        
            EveningMinPunch = MIN(CASE        
                                    WHEN v.p IS NOT NULL AND v.p <> '00:00:00'        
    AND CAST(v.p AS TIME(0)) >= CAST('14:00:00' AS TIME(0))        
                                    THEN v.p        
                                  END),        
        
            MorningMinPunch = MIN(CASE        
                                    WHEN v.p IS NOT NULL AND v.p <> '00:00:00'        
                                     AND CAST(v.p AS TIME(0)) <= CAST('14:00:00' AS TIME(0))        
                                    THEN v.p        
                                  END),        
            MorningMaxPunch = MAX(CASE        
                                    WHEN v.p IS NOT NULL AND v.p <> '00:00:00'        
                                     AND CAST(v.p AS TIME(0)) <= CAST('14:00:00' AS TIME(0))        
                                    THEN v.p        
                                  END)        
        FROM (VALUES        
            (pa.Punch1),(pa.Punch2),(pa.Punch3),(pa.Punch4),(pa.Punch5),(pa.Punch6),        
            (pa.Punch7),(pa.Punch8),(pa.Punch9),(pa.Punch10),(pa.Punch11),(pa.Punch12)        
        ) v(p)        
    ) calc;        
        
    CREATE CLUSTERED INDEX IX_#PunchCalc ON #PunchCalc(EmployeeId, PunchDate);        
        
    /*==========================================================        
      9) Machine Minutes        
    ==========================================================*/        
    IF OBJECT_ID('tempdb..#DwmMachine') IS NOT NULL DROP TABLE #DwmMachine;        
        
    SELECT        
        pc.EmployeeId,        
        pc.PunchDate,        
        TotalDailyWorkingMinutes =        
            CASE        
                WHEN pc.IsRegularize = 1        
                     AND NULLIF(pc.RegularizePunchIn,'00:00:00') IS NOT NULL        
   AND NULLIF(pc.RegularizePuncOut,'00:00:00') IS NOT NULL        
                     AND DATEDIFF(MINUTE, pc.RegularizePunchIn, pc.RegularizePuncOut) > 0        
                THEN DATEDIFF(MINUTE, pc.RegularizePunchIn, pc.RegularizePuncOut)        
                ELSE        
                    (CASE WHEN NULLIF(pc.Punch1,'00:00:00')  IS NOT NULL AND NULLIF(pc.Punch2,'00:00:00')  IS NOT NULL AND pc.Punch2  > pc.Punch1  THEN DATEDIFF(MINUTE, pc.Punch1,  pc.Punch2)  ELSE 0 END) +        
                    (CASE WHEN NULLIF(pc.Punch3,'00:00:00')  IS NOT NULL AND NULLIF(pc.Punch4,'00:00:00')  IS NOT NULL AND pc.Punch4  > pc.Punch3  THEN DATEDIFF(MINUTE, pc.Punch3,  pc.Punch4)  ELSE 0 END) +        
                    (CASE WHEN NULLIF(pc.Punch5,'00:00:00')  IS NOT NULL AND NULLIF(pc.Punch6,'00:00:00')  IS NOT NULL AND pc.Punch6  > pc.Punch5  THEN DATEDIFF(MINUTE, pc.Punch5,  pc.Punch6)  ELSE 0 END) +        
                    (CASE WHEN NULLIF(pc.Punch7,'00:00:00')  IS NOT NULL AND NULLIF(pc.Punch8,'00:00:00')  IS NOT NULL AND pc.Punch8  > pc.Punch7  THEN DATEDIFF(MINUTE, pc.Punch7,  pc.Punch8)  ELSE 0 END) +        
                    (CASE WHEN NULLIF(pc.Punch9,'00:00:00')  IS NOT NULL AND NULLIF(pc.Punch10,'00:00:00') IS NOT NULL AND pc.Punch10 > pc.Punch9  THEN DATEDIFF(MINUTE, pc.Punch9,  pc.Punch10) ELSE 0 END) +        
                    (CASE WHEN NULLIF(pc.Punch11,'00:00:00') IS NOT NULL AND NULLIF(pc.Punch12,'00:00:00') IS NOT NULL AND pc.Punch12 > pc.Punch11 THEN DATEDIFF(MINUTE, pc.Punch11, pc.Punch12) ELSE 0 END)        
            END,        
        HasEffectivePair =        
            CASE        
                WHEN pc.IsRegularize = 1        
                     AND NULLIF(pc.RegularizePunchIn,'00:00:00') IS NOT NULL        
                     AND NULLIF(pc.RegularizePuncOut,'00:00:00') IS NOT NULL        
                     AND DATEDIFF(MINUTE, pc.RegularizePunchIn, pc.RegularizePuncOut) > 0        
                THEN 1        
                WHEN        
                    (NULLIF(pc.Punch1,'00:00:00')  IS NOT NULL AND NULLIF(pc.Punch2,'00:00:00')  IS NOT NULL AND pc.Punch2  > pc.Punch1) OR        
                    (NULLIF(pc.Punch3,'00:00:00')  IS NOT NULL AND NULLIF(pc.Punch4,'00:00:00')  IS NOT NULL AND pc.Punch4  > pc.Punch3) OR        
                    (NULLIF(pc.Punch5,'00:00:00')  IS NOT NULL AND NULLIF(pc.Punch6,'00:00:00')  IS NOT NULL AND pc.Punch6  > pc.Punch5) OR        
                    (NULLIF(pc.Punch7,'00:00:00')  IS NOT NULL AND NULLIF(pc.Punch8,'00:00:00')  IS NOT NULL AND pc.Punch8  > pc.Punch7) OR        
                    (NULLIF(pc.Punch9,'00:00:00')  IS NOT NULL AND NULLIF(pc.Punch10,'00:00:00') IS NOT NULL AND pc.Punch10 > pc.Punch9) OR        
                    (NULLIF(pc.Punch11,'00:00:00') IS NOT NULL AND NULLIF(pc.Punch12,'00:00:00') IS NOT NULL AND pc.Punch12 > pc.Punch11)     
                THEN 1 ELSE 0        
            END        
    INTO #DwmMachine        
    FROM #PunchCalc pc;        
        
    CREATE CLUSTERED INDEX IX_#DwmMachine ON #DwmMachine(EmployeeId, PunchDate);        
        
    /*==========================================================        
      10) Geo Approved + GeoDaily  (EXTENDED to @ToDateEx window)        
    ==========================================================*/        
    IF OBJECT_ID('tempdb..#GeoApproved') IS NOT NULL DROP TABLE #GeoApproved;        
        
    SELECT        
        ar.EmployeeId,        
        PunchDate = CONVERT(date, ar.PunchTimeUtc),        
        ar.PunchTimeUtc,        
        ar.PunchType,        
        ar.Address,        
        rn = ROW_NUMBER() OVER (PARTITION BY ar.EmployeeId, CONVERT(date, ar.PunchTimeUtc) ORDER BY ar.PunchTimeUtc)        
    INTO #GeoApproved        
    FROM dbo.AttendanceRecord ar WITH (NOLOCK)        
    JOIN #Emp e ON e.EmployeeId = ar.EmployeeId        
    WHERE ar.StatusId = 1        
      AND ar.PunchTimeUtc >= @FromDate        
      AND ar.PunchTimeUtc <  DATEADD(DAY, 2, @ToDateEx);  -- safe buffer for cross-midnight window        
        
    CREATE CLUSTERED INDEX IX_#GeoApproved ON #GeoApproved(EmployeeId, PunchDate, rn);        
        
    IF OBJECT_ID('tempdb..#GeoDaily') IS NOT NULL DROP TABLE #GeoDaily;        
        
    ;WITH PunchCounts AS        
    (        
        SELECT        
            EmployeeId,        
            PunchDate,        
            TotalPunches  = COUNT(*),        
            GeoFirstInUtc = MIN(PunchTimeUtc),        
            GeoLastOutUtc = MAX(PunchTimeUtc),        
            Address       = MIN(Address)        
        FROM #GeoApproved        
        GROUP BY EmployeeId, PunchDate        
    ),        
    Paired AS        
    (        
        SELECT        
            a.EmployeeId,        
            a.PunchDate,        
            WorkedMinutes = CASE        
                              WHEN b.PunchTimeUtc > a.PunchTimeUtc        
                              THEN DATEDIFF(MINUTE, a.PunchTimeUtc, b.PunchTimeUtc)        
                              ELSE 0        
                            END        
        FROM #GeoApproved a        
        JOIN #GeoApproved b        
          ON b.EmployeeId = a.EmployeeId        
         AND b.PunchDate  = a.PunchDate        
         AND b.rn = a.rn + 1        
        WHERE a.rn % 2 = 1        
    ),        
    PairAgg AS        
    (        
        SELECT EmployeeId, PunchDate, GeoWorkedMinutes = SUM(WorkedMinutes)        
        FROM Paired        
        GROUP BY EmployeeId, PunchDate        
    )        
    SELECT        
        pc.EmployeeId,        
        pc.PunchDate,        
        pc.GeoFirstInUtc,        
        pc.GeoLastOutUtc,        
        GeoWorkedMinutes = ISNULL(pa.GeoWorkedMinutes, 0),        
        GeoTotalPunches  = pc.TotalPunches,        
        GeoStatus = CASE WHEN pc.TotalPunches % 2 <> 0 THEN 'MISS' ELSE 'GF' END,        
        pc.Address        
    INTO #GeoDaily        
    FROM PunchCounts pc        
    LEFT JOIN PairAgg pa        
      ON pa.EmployeeId = pc.EmployeeId        
     AND pa.PunchDate  = pc.PunchDate;        
        
    CREATE CLUSTERED INDEX IX_#GeoDaily ON #GeoDaily(EmployeeId, PunchDate);        
        
    /*==========================================================        
      10.5) BOTH MERGE: build combined punch stream (Machine + Geo),        
           sorted by time, then compute minutes by pairing.        
           (Shift-aware + cross-midnight window)        
    ==========================================================*/        
    IF OBJECT_ID('tempdb..#WorkWin') IS NOT NULL DROP TABLE #WorkWin;        
        
    SELECT        
        EmployeeId,        
        WorkDate = PunchDate,        
        IsCrossMidnight,        
        WinStartDT = DATEADD(MINUTE, CASE WHEN IsCrossMidnight=1 THEN 14*60 ELSE 0 END, CAST(PunchDate AS DATETIME)),        
        WinEndDT   = DATEADD(DAY, 1, DATEADD(MINUTE, CASE WHEN IsCrossMidnight=1 THEN 14*60 ELSE 0 END, CAST(PunchDate AS DATETIME)))        
    INTO #WorkWin        
    FROM #EDG;        
        
    CREATE CLUSTERED INDEX IX_#WorkWin ON #WorkWin(EmployeeId, WorkDate);        
        
    /* Machine punches as rows (work-date aware for cross-midnight) */        
    IF OBJECT_ID('tempdb..#MachinePunchRows') IS NOT NULL DROP TABLE #MachinePunchRows;        
        
    ;WITH MP AS        
    (        
        SELECT        
            pa.EmployeeId,        
            pa.PunchDate,        
            PunchTime = CAST(v.p AS TIME(0))        
        FROM #PunchAgg pa        
        CROSS APPLY (VALUES        
            (pa.Punch1),(pa.Punch2),(pa.Punch3),(pa.Punch4),(pa.Punch5),(pa.Punch6),        
            (pa.Punch7),(pa.Punch8),(pa.Punch9),(pa.Punch10),(pa.Punch11),(pa.Punch12)        
        ) v(p)        
        WHERE v.p IS NOT NULL AND v.p <> '00:00:00'        
    )        
    SELECT        
        w.EmployeeId,        
        w.WorkDate,        
        PunchDT = DATEADD(SECOND, DATEDIFF(SECOND,'00:00:00', mp.PunchTime),        
                  CASE        
                     WHEN w.IsCrossMidnight=1 AND mp.PunchDate = DATEADD(DAY,1,w.WorkDate)        
                     THEN DATEADD(DAY,1, CAST(w.WorkDate AS DATETIME))        
                     ELSE CAST(w.WorkDate AS DATETIME)        
                  END),        
        Src = 'M'        
    INTO #MachinePunchRows        
    FROM #WorkWin w        
    JOIN MP mp        
      ON mp.EmployeeId = w.EmployeeId        
     AND (        
          (w.IsCrossMidnight = 0 AND mp.PunchDate = w.WorkDate)        
       OR (w.IsCrossMidnight = 1 AND        
            (        
              (mp.PunchDate = w.WorkDate AND mp.PunchTime >= '14:00:00')        
              OR        
              (mp.PunchDate = DATEADD(DAY,1,w.WorkDate) AND mp.PunchTime <= '14:00:00')        
            )        
          )        
     );        
      /*==========================================================  
  10.5.M) MACHINE WORK-DATE VIEW  
  Purpose:  
    - build machine-only punches by WorkDate  
    - for cross-midnight shifts, next-day morning punch belongs  
      to previous workday only  
    - do NOT disturb existing calendar-date logic  
==========================================================*/  
IF OBJECT_ID('tempdb..#MachinePunchOrdered') IS NOT NULL DROP TABLE #MachinePunchOrdered;  
  
SELECT  
    EmployeeId,  
    WorkDate,  
    PunchDT,  
    rn = ROW_NUMBER() OVER  
         (  
            PARTITION BY EmployeeId, WorkDate  
            ORDER BY PunchDT  
         )  
INTO #MachinePunchOrdered  
FROM #MachinePunchRows;  
  
CREATE CLUSTERED INDEX IX_#MachinePunchOrdered  
ON #MachinePunchOrdered(EmployeeId, WorkDate, rn);  
  
IF OBJECT_ID('tempdb..#MachineDaily') IS NOT NULL DROP TABLE #MachineDaily;  
  
;WITH Pairing AS  
(  
    SELECT  
        a.EmployeeId,  
        a.WorkDate,  
        WorkedMinutes =  
            CASE  
                WHEN b.PunchDT > a.PunchDT  
                THEN DATEDIFF(MINUTE, a.PunchDT, b.PunchDT)  
                ELSE 0  
            END  
    FROM #MachinePunchOrdered a  
    JOIN #MachinePunchOrdered b  
      ON b.EmployeeId = a.EmployeeId  
     AND b.WorkDate   = a.WorkDate  
     AND b.rn         = a.rn + 1  
    WHERE a.rn % 2 = 1  
),  
Agg AS  
(  
    SELECT  
        EmployeeId,  
        WorkDate,  
        MachineTotalPunches = COUNT(*),  
        MachineFirstInDT    = MIN(PunchDT),  
        MachineLastOutDT    = MAX(PunchDT)  
    FROM #MachinePunchOrdered  
    GROUP BY EmployeeId, WorkDate  
),  
PairAgg AS  
(  
    SELECT  
        EmployeeId,  
        WorkDate,  
        MachineWorkedMinutes = SUM(WorkedMinutes)  
    FROM Pairing  
    GROUP BY EmployeeId, WorkDate  
)  
SELECT  
    a.EmployeeId,  
    PunchDate = a.WorkDate,  
    a.MachineTotalPunches,  
    a.MachineFirstInDT,  
    a.MachineLastOutDT,  
    MachineWorkedMinutes = ISNULL(p.MachineWorkedMinutes, 0),  
    MachineStatus =  
        CASE  
            WHEN a.MachineTotalPunches % 2 = 0 THEN 'GF'  
            ELSE 'MISS'  
        END  
INTO #MachineDaily  
FROM Agg a  
LEFT JOIN PairAgg p  
  ON p.EmployeeId = a.EmployeeId  
 AND p.WorkDate   = a.WorkDate;  
  
CREATE CLUSTERED INDEX IX_#MachineDaily  
ON #MachineDaily(EmployeeId, PunchDate);  
  
IF OBJECT_ID('tempdb..#MachinePunchCols') IS NOT NULL DROP TABLE #MachinePunchCols;  
  
SELECT  
    EmployeeId,  
    WorkDate,  
    Punch1  = MAX(CASE WHEN rn = 1  THEN CAST(PunchDT AS TIME(0)) END),  
    Punch2  = MAX(CASE WHEN rn = 2  THEN CAST(PunchDT AS TIME(0)) END),  
    Punch3  = MAX(CASE WHEN rn = 3  THEN CAST(PunchDT AS TIME(0)) END),  
    Punch4  = MAX(CASE WHEN rn = 4  THEN CAST(PunchDT AS TIME(0)) END),  
    Punch5  = MAX(CASE WHEN rn = 5  THEN CAST(PunchDT AS TIME(0)) END),  
    Punch6  = MAX(CASE WHEN rn = 6  THEN CAST(PunchDT AS TIME(0)) END),  
    Punch7  = MAX(CASE WHEN rn = 7  THEN CAST(PunchDT AS TIME(0)) END),  
    Punch8  = MAX(CASE WHEN rn = 8  THEN CAST(PunchDT AS TIME(0)) END),  
    Punch9  = MAX(CASE WHEN rn = 9  THEN CAST(PunchDT AS TIME(0)) END),  
    Punch10 = MAX(CASE WHEN rn = 10 THEN CAST(PunchDT AS TIME(0)) END),  
    Punch11 = MAX(CASE WHEN rn = 11 THEN CAST(PunchDT AS TIME(0)) END),  
    Punch12 = MAX(CASE WHEN rn = 12 THEN CAST(PunchDT AS TIME(0)) END)  
INTO #MachinePunchCols  
FROM #MachinePunchOrdered  
WHERE rn <= 12  
GROUP BY EmployeeId, WorkDate;  
  
CREATE CLUSTERED INDEX IX_#MachinePunchCols  
ON #MachinePunchCols(EmployeeId, WorkDate);  
    CREATE CLUSTERED INDEX IX_#MachinePunchRows ON #MachinePunchRows(EmployeeId, WorkDate, PunchDT);        
        
   /* Geo punches as rows (window based) */        
    IF OBJECT_ID('tempdb..#GeoPunchRows') IS NOT NULL DROP TABLE #GeoPunchRows;        
        
    SELECT        
        w.EmployeeId,        
        w.WorkDate,        
        PunchDT = ga.PunchTimeUtc,        
        Src = 'G'        
    INTO #GeoPunchRows        
    FROM #WorkWin w        
    JOIN #GeoApproved ga        
      ON ga.EmployeeId = w.EmployeeId        
     AND ga.PunchTimeUtc >= w.WinStartDT        
     AND ga.PunchTimeUtc <  w.WinEndDT;        
        
    CREATE CLUSTERED INDEX IX_#GeoPunchRows ON #GeoPunchRows(EmployeeId, WorkDate, PunchDT);        
        
    /* Union + order */        
    IF OBJECT_ID('tempdb..#AllPunchRows') IS NOT NULL DROP TABLE #AllPunchRows;        
        
    SELECT EmployeeId, WorkDate, PunchDT, Src        
    INTO #AllPunchRows        
    FROM #MachinePunchRows        
    UNION ALL        
    SELECT EmployeeId, WorkDate, PunchDT, Src        
    FROM #GeoPunchRows;        
        
    CREATE CLUSTERED INDEX IX_#AllPunchRows ON #AllPunchRows(EmployeeId, WorkDate, PunchDT, Src);        
        
    --IF OBJECT_ID('tempdb..#AllPunchOrdered') IS NOT NULL DROP TABLE #AllPunchOrdered;        
        
    --SELECT        
    --    EmployeeId,        
    --    WorkDate,        
    --    PunchDT,        
    --    Src,        
    --    rn = ROW_NUMBER() OVER        
    --         (PARTITION BY EmployeeId, WorkDate        
    --          ORDER BY PunchDT, CASE WHEN Src='M' THEN 0 ELSE 1 END)        
    --INTO #AllPunchOrdered        
    --FROM #AllPunchRows;        
        
    --CREATE CLUSTERED INDEX IX_#AllPunchOrdered ON #AllPunchOrdered(EmployeeId, WorkDate, rn);        
 /* ==============================        
   10.5.x) De-dup machine+geo punches:        
   If consecutive punches are within 5 minutes, treat as same punch.        
   Keep 1 punch per group (prefer Machine).        
================================ */        
        
IF OBJECT_ID('tempdb..#AllPunchGrouped') IS NOT NULL DROP TABLE #AllPunchGrouped;        
        
;WITH S AS        
(        
    SELECT        
        a.EmployeeId,        
        a.WorkDate,        
        a.PunchDT,        
        a.Src,        
        edg.STCode,            -- <-- location key (use LocationId if you prefer)        
 PrevPunchDT = LAG(a.PunchDT) OVER (PARTITION BY a.EmployeeId, a.WorkDate ORDER BY a.PunchDT)        
    FROM #AllPunchRows a        
    JOIN #EDG edg        
      ON edg.EmployeeId = a.EmployeeId        
     AND edg.PunchDate  = a.WorkDate        
),        
G AS        
(        
    SELECT        
        EmployeeId,        
        WorkDate,        
        PunchDT,        
        Src,        
        STCode,        
        grp =        
            SUM(        
                CASE        
                    WHEN PrevPunchDT IS NULL THEN 1        
        
                    -- RH01 ONLY: merge punches if next punch is within 10 minutes        
                    WHEN STCode = 'RH02'        
                         AND DATEDIFF(MINUTE, PrevPunchDT, PunchDT) < 10        
                    THEN 0        
                    WHEN STCode = 'RH02'        
                         AND DATEDIFF(MINUTE, PrevPunchDT, PunchDT) >= 10        
                    THEN 1        
        
                    -- All other locations: NO gap-merge; only collapse exact duplicates        
                    WHEN STCode <> 'RH02'        
                         AND PunchDT = PrevPunchDT        
                    THEN 0        
                    ELSE 1        
                END        
            ) OVER (PARTITION BY EmployeeId, WorkDate ORDER BY PunchDT ROWS UNBOUNDED PRECEDING)        
    FROM S        
)        
SELECT EmployeeId, WorkDate, PunchDT, Src, grp        
INTO #AllPunchGrouped        
FROM G;        
        
CREATE CLUSTERED INDEX IX_#AllPunchGrouped ON #AllPunchGrouped(EmployeeId, WorkDate, grp, PunchDT);        
/* Pick 1 punch per 5-min group:        
   Machine wins if present, else earliest */        
IF OBJECT_ID('tempdb..#AllPunchDedup') IS NOT NULL DROP TABLE #AllPunchDedup;        
        
;WITH R AS        
(        
    SELECT        
        EmployeeId,        
        WorkDate,        
        grp,        
        PunchDT,        
        Src,        
        rnPick = ROW_NUMBER() OVER        
                 (        
                   PARTITION BY EmployeeId, WorkDate, grp        
                   ORDER BY CASE WHEN Src='M' THEN 0 ELSE 1 END, PunchDT        
                 )        
    FROM #AllPunchGrouped        
)        
SELECT        
    EmployeeId,        
    WorkDate,        
    PunchDT,        
    Src        
INTO #AllPunchDedup        
FROM R        
WHERE rnPick = 1;        
        
CREATE CLUSTERED INDEX IX_#AllPunchDedup ON #AllPunchDedup(EmployeeId, WorkDate, PunchDT, Src);        
        
/* Now order punches for pairing (use deduped stream) */        
IF OBJECT_ID('tempdb..#AllPunchOrdered') IS NOT NULL DROP TABLE #AllPunchOrdered;        
        
SELECT        
    EmployeeId,        
    WorkDate,        
    PunchDT,        
    Src,        
    rn = ROW_NUMBER() OVER        
         (PARTITION BY EmployeeId, WorkDate        
          ORDER BY PunchDT, CASE WHEN Src='M' THEN 0 ELSE 1 END)        
INTO #AllPunchOrdered        
FROM #AllPunchDedup;        
        
CREATE CLUSTERED INDEX IX_#AllPunchOrdered ON #AllPunchOrdered(EmployeeId, WorkDate, rn);        
        
    IF OBJECT_ID('tempdb..#BothDaily') IS NOT NULL DROP TABLE #BothDaily;        
        
    ;WITH Pairing AS        
    (        
        SELECT        
            a.EmployeeId,        
            a.WorkDate,        
            WorkedMinutes =        
              CASE WHEN b.PunchDT > a.PunchDT        
                   THEN DATEDIFF(MINUTE, a.PunchDT, b.PunchDT)        
                   ELSE 0        
              END        
        FROM #AllPunchOrdered a        
        JOIN #AllPunchOrdered b        
          ON b.EmployeeId = a.EmployeeId        
         AND b.WorkDate   = a.WorkDate        
         AND b.rn = a.rn + 1        
        WHERE a.rn % 2 = 1        
    ),        
    Agg AS        
    (        
        SELECT        
            EmployeeId,        
            WorkDate,        
            BothTotalPunches = COUNT(*),        
            BothFirstInDT    = MIN(PunchDT),        
            BothLastOutDT    = MAX(PunchDT)        
        FROM #AllPunchOrdered        
        GROUP BY EmployeeId, WorkDate        
    ),        
    PairAgg AS        
    (        
        SELECT EmployeeId, WorkDate, BothWorkedMinutes = SUM(WorkedMinutes)        
        FROM Pairing        
        GROUP BY EmployeeId, WorkDate        
    )        
    SELECT        
        a.EmployeeId,        
        PunchDate = a.WorkDate,        
        a.BothTotalPunches,        
        a.BothFirstInDT,        
        a.BothLastOutDT,        
        BothWorkedMinutes = ISNULL(p.BothWorkedMinutes,0),        
        BothStatus = CASE WHEN a.BothTotalPunches % 2 = 0 THEN 'GF' ELSE 'MISS' END        
    INTO #BothDaily        
    FROM Agg a        
    LEFT JOIN PairAgg p        
      ON p.EmployeeId = a.EmployeeId        
     AND p.WorkDate   = a.WorkDate;        
        
    CREATE CLUSTERED INDEX IX_#BothDaily ON #BothDaily(EmployeeId, PunchDate);        
        
    /* Optional: merged Punch1..Punch12 for output */        
    IF OBJECT_ID('tempdb..#BothPunchCols') IS NOT NULL DROP TABLE #BothPunchCols;        
        
    SELECT        
        EmployeeId,        
        WorkDate,        
        Punch1  = MAX(CASE WHEN rn=1  THEN CAST(PunchDT AS TIME(0)) END),        
        Punch2  = MAX(CASE WHEN rn=2  THEN CAST(PunchDT AS TIME(0)) END),        
        Punch3  = MAX(CASE WHEN rn=3  THEN CAST(PunchDT AS TIME(0)) END),        
        Punch4  = MAX(CASE WHEN rn=4  THEN CAST(PunchDT AS TIME(0)) END),        
        Punch5  = MAX(CASE WHEN rn=5  THEN CAST(PunchDT AS TIME(0)) END),        
        Punch6  = MAX(CASE WHEN rn=6  THEN CAST(PunchDT AS TIME(0)) END),        
        Punch7  = MAX(CASE WHEN rn=7  THEN CAST(PunchDT AS TIME(0)) END),        
        Punch8  = MAX(CASE WHEN rn=8  THEN CAST(PunchDT AS TIME(0)) END),        
        Punch9  = MAX(CASE WHEN rn=9  THEN CAST(PunchDT AS TIME(0)) END),        
        Punch10 = MAX(CASE WHEN rn=10 THEN CAST(PunchDT AS TIME(0)) END),        
        Punch11 = MAX(CASE WHEN rn=11 THEN CAST(PunchDT AS TIME(0)) END),        
        Punch12 = MAX(CASE WHEN rn=12 THEN CAST(PunchDT AS TIME(0)) END)        
    INTO #BothPunchCols        
    FROM #AllPunchOrdered        
    WHERE rn <= 12        
    GROUP BY EmployeeId, WorkDate;        
        
    CREATE CLUSTERED INDEX IX_#BothPunchCols ON #BothPunchCols(EmployeeId, WorkDate);        
        
    /*==========================================================        
      11) Effective In/Out        
          (Night shift logic preserved)        
    ==========================================================*/        
    IF OBJECT_ID('tempdb..#Eff') IS NOT NULL DROP TABLE #Eff;        
        
    SELECT        
        edg.EmployeeId,        
        edg.PunchDate,        
        edg.LocationId,        
        edg.IsFlexibleShift,        
        edg.IsCrossMidnight,        
        
        ShiftStartDT = DATEADD(SECOND, DATEDIFF(SECOND,'00:00:00', edg.ShiftStartTime), CAST(edg.PunchDate AS DATETIME)),        
        
        EffectiveInDT =        
            CASE        
                WHEN pc.PunchIn IS NULL OR pc.PunchIn='00:00:00' THEN NULL        
                WHEN edg.IsCrossMidnight = 1        
                     AND pc.IsRegularize = 0        
                     AND NULLIF(pc.EveningFirstPunch,'00:00:00') IS NOT NULL        
                THEN DATEADD(SECOND, DATEDIFF(SECOND,'00:00:00', CAST(pc.EveningFirstPunch AS TIME)), CAST(edg.PunchDate AS DATETIME))        
                ELSE DATEADD(SECOND, DATEDIFF(SECOND,'00:00:00', CAST(pc.PunchIn AS TIME)), CAST(edg.PunchDate AS DATETIME))        
            END,        
        
        EffectiveOutDT =        
            CASE        
                WHEN edg.IsCrossMidnight = 1 THEN        
                    CASE        
                        WHEN (edgN.IsHoliday = 1 OR edgN.IsWeekend = 1)        
                             AND pcN.MorningFirstPunch IS NOT NULL AND pcN.MorningFirstPunch <> '00:00:00'        
                        THEN DATEADD(SECOND, DATEDIFF(SECOND,'00:00:00', CAST(pcN.MorningFirstPunch AS TIME)),        
                                     DATEADD(DAY,1,CAST(edg.PunchDate AS DATETIME)))        
        
                        WHEN pcN.MorningLastPunch IS NOT NULL AND pcN.MorningLastPunch <> '00:00:00'        
                        THEN DATEADD(SECOND, DATEDIFF(SECOND,'00:00:00', CAST(pcN.MorningLastPunch AS TIME)),        
                                     DATEADD(DAY,1,CAST(edg.PunchDate AS DATETIME)))        
        
                        ELSE        
                            CASE WHEN pc.PunchOut IS NULL OR pc.PunchOut='00:00:00' THEN NULL        
                                 ELSE DATEADD(SECOND, DATEDIFF(SECOND,'00:00:00', CAST(pc.PunchOut AS TIME)), CAST(edg.PunchDate AS DATETIME))        
                            END        
                    END        
                ELSE        
                    CASE WHEN pc.PunchOut IS NULL OR pc.PunchOut='00:00:00' THEN NULL        
                         ELSE DATEADD(SECOND, DATEDIFF(SECOND,'00:00:00', CAST(pc.PunchOut AS TIME)), CAST(edg.PunchDate AS DATETIME))        
                    END        
            END,        
        
        EffectivePunchInTime  =        
            CAST(NULLIF(        
                CASE        
                    WHEN edg.IsCrossMidnight = 1 AND pc.IsRegularize = 0 AND NULLIF(pc.EveningFirstPunch,'00:00:00') IS NOT NULL        
                    THEN pc.EveningFirstPunch        
                    ELSE pc.PunchIn        
                END,'00:00:00') AS TIME(0)),        
        
        EffectivePunchOutTime = CAST(NULLIF(pc.PunchOut,'00:00:00') AS TIME(0))        
    INTO #Eff        
    FROM #EDG edg        
    LEFT JOIN #PunchCalc pc        
      ON pc.EmployeeId = edg.EmployeeId AND pc.PunchDate = edg.PunchDate        
    LEFT JOIN #EDG edgN        
      ON edgN.EmployeeId = edg.EmployeeId AND edgN.PunchDate = DATEADD(DAY,1,edg.PunchDate)        
    LEFT JOIN #PunchCalc pcN        
      ON pcN.EmployeeId = edg.EmployeeId AND pcN.PunchDate = DATEADD(DAY,1,edg.PunchDate);        
        
    CREATE CLUSTERED INDEX IX_#Eff ON #Eff(EmployeeId, PunchDate);        
      /*==========================================================  
  12) DwmFinal  
  (REGULARIZE > BOTH > GEO > MACHINE)  
  UPDATED: machine cross-midnight uses work-date machine stream  
==========================================================*/  
IF OBJECT_ID('tempdb..#DwmFinal') IS NOT NULL DROP TABLE #DwmFinal;  
  
SELECT  
    edg.EmployeeId,  
    edg.PunchDate,  
  
    HasGeo     = CASE WHEN ISNULL(gd.GeoTotalPunches,0) > 0 THEN 1 ELSE 0 END,  
    HasMachine = CASE WHEN ISNULL(pc.PunchCount,0) > 0 THEN 1 ELSE 0 END,  
  
    SourceFlag =  
    CASE  
        WHEN ISNULL(pc.IsRegularize,0) = 1  
             AND NULLIF(pc.RegularizePunchIn,'00:00:00')  IS NOT NULL  
             AND NULLIF(pc.RegularizePuncOut,'00:00:00') IS NOT NULL  
             AND DATEDIFF(MINUTE, pc.RegularizePunchIn, pc.RegularizePuncOut) > 0  
        THEN 'REGULARIZE'  
  
        WHEN ISNULL(gd.GeoTotalPunches,0) > 0 AND ISNULL(pc.PunchCount,0) > 0 THEN 'BOTH'  
        WHEN ISNULL(gd.GeoTotalPunches,0) > 0 THEN 'GEO'  
        WHEN ISNULL(pc.PunchCount,0) > 0 THEN 'MACHINE'  
        ELSE 'NONE'  
    END,  
  
    GeoWorkedMinutes     = ISNULL(gd.GeoWorkedMinutes, 0),  
    MachineWorkedMinutes = ISNULL(dm.TotalDailyWorkingMinutes, 0),  
    BothWorkedMinutes    = ISNULL(bd.BothWorkedMinutes, 0),  
  
    TotalDailyWorkingMinutes =  
    CASE  
        WHEN ISNULL(pc.IsRegularize,0) = 1  
             AND NULLIF(pc.RegularizePunchIn,'00:00:00')  IS NOT NULL  
             AND NULLIF(pc.RegularizePuncOut,'00:00:00') IS NOT NULL  
             AND DATEDIFF(MINUTE, pc.RegularizePunchIn, pc.RegularizePuncOut) > 0  
        THEN DATEDIFF(MINUTE, pc.RegularizePunchIn, pc.RegularizePuncOut)  
  
        WHEN (ISNULL(gd.GeoTotalPunches,0) > 0 AND ISNULL(pc.PunchCount,0) > 0)  
        THEN ISNULL(bd.BothWorkedMinutes,0)  
  
        WHEN ISNULL(gd.GeoTotalPunches,0) > 0  
        THEN ISNULL(gd.GeoWorkedMinutes,0)  
  
        WHEN edg.IsCrossMidnight = 1  
             AND ISNULL(pc.PunchCount,0) > 0  
             AND ISNULL(md.MachineWorkedMinutes,0) > 0  
        THEN md.MachineWorkedMinutes  
  
        WHEN dm.TotalDailyWorkingMinutes IS NOT NULL  
        THEN dm.TotalDailyWorkingMinutes  
  
        WHEN ef.EffectiveInDT IS NOT NULL  
             AND ef.EffectiveOutDT IS NOT NULL  
             AND DATEDIFF(MINUTE, ef.EffectiveInDT, ef.EffectiveOutDT) > 0  
        THEN DATEDIFF(MINUTE, ef.EffectiveInDT, ef.EffectiveOutDT)  
  
        ELSE 0  
    END,  
  
    HasEffectivePair =  
    CASE  
        WHEN (ISNULL(gd.GeoTotalPunches,0) > 0 AND ISNULL(pc.PunchCount,0) > 0)  
             AND ISNULL(bd.BothTotalPunches,0) % 2 = 0  
             AND ISNULL(bd.BothWorkedMinutes,0) > 0  
        THEN 1  
  
        WHEN ISNULL(gd.GeoTotalPunches,0) > 0  
             AND gd.GeoStatus = 'GF'  
        THEN 1  
  
        WHEN edg.IsCrossMidnight = 1  
             AND ISNULL(pc.PunchCount,0) > 0  
             AND ISNULL(md.MachineTotalPunches,0) % 2 = 0  
             AND ISNULL(md.MachineWorkedMinutes,0) > 0  
        THEN 1  
  
        WHEN dm.HasEffectivePair = 1  
        THEN 1  
  
        WHEN ef.EffectiveInDT IS NOT NULL  
             AND ef.EffectiveOutDT IS NOT NULL  
             AND DATEDIFF(MINUTE, ef.EffectiveInDT, ef.EffectiveOutDT) > 0  
        THEN 1  
  
        ELSE 0  
    END,  
  
    GeoStatus =  
    CASE  
        WHEN (ISNULL(gd.GeoTotalPunches,0) > 0 AND ISNULL(pc.PunchCount,0) > 0)  
        THEN ISNULL(bd.BothStatus,'')  
        ELSE ISNULL(gd.GeoStatus,'')  
    END,  
  
    GeoAddress = gd.Address  
INTO #DwmFinal  
FROM #EDG edg  
LEFT JOIN #GeoDaily    gd ON gd.EmployeeId = edg.EmployeeId AND gd.PunchDate = edg.PunchDate  
LEFT JOIN #PunchCalc   pc ON pc.EmployeeId = edg.EmployeeId AND pc.PunchDate = edg.PunchDate  
LEFT JOIN #DwmMachine  dm ON dm.EmployeeId = edg.EmployeeId AND dm.PunchDate = edg.PunchDate  
LEFT JOIN #Eff         ef ON ef.EmployeeId = edg.EmployeeId AND ef.PunchDate = edg.PunchDate  
LEFT JOIN #BothDaily   bd ON bd.EmployeeId = edg.EmployeeId AND bd.PunchDate = edg.PunchDate  
LEFT JOIN #MachineDaily md ON md.EmployeeId = edg.EmployeeId AND md.PunchDate = edg.PunchDate;  
  
CREATE CLUSTERED INDEX IX_#DwmFinal ON #DwmFinal(EmployeeId, PunchDate);  
    /*==========================================================        
      12) DwmFinal (REGULARIZE > BOTH(merged) > GEO > MACHINE > Effective)        
    ==========================================================*/        
--    IF OBJECT_ID('tempdb..#DwmFinal') IS NOT NULL DROP TABLE #DwmFinal;        
        
--    SELECT        
--        edg.EmployeeId,        
--        edg.PunchDate,        
        
--        -- flags        
--        HasGeo     = CASE WHEN ISNULL(gd.GeoTotalPunches,0)  > 0 THEN 1 ELSE 0 END,        
--        HasMachine = CASE WHEN ISNULL(pc.PunchCount,0)       > 0 THEN 1 ELSE 0 END,        
        
--        SourceFlag =        
--        CASE        
--            WHEN ISNULL(pc.IsRegularize,0) = 1        
--                 AND NULLIF(pc.RegularizePunchIn,'00:00:00')  IS NOT NULL        
--                 AND NULLIF(pc.RegularizePuncOut,'00:00:00') IS NOT NULL        
--                 AND DATEDIFF(MINUTE, pc.RegularizePunchIn, pc.RegularizePuncOut) > 0        
--            THEN 'REGULARIZE'        
        
--            WHEN ISNULL(gd.GeoTotalPunches,0) > 0 AND ISNULL(pc.PunchCount,0) > 0 THEN 'BOTH'        
--            WHEN ISNULL(gd.GeoTotalPunches,0) > 0 THEN 'GEO'        
--            WHEN ISNULL(pc.PunchCount,0) > 0 THEN 'MACHINE'        
--            ELSE 'NONE'        
--        END,        
        
--        -- audit minutes        
--        GeoWorkedMinutes     = ISNULL(gd.GeoWorkedMinutes, 0),        
--        MachineWorkedMinutes = ISNULL(dm.TotalDailyWorkingMinutes, 0),        
--        BothWorkedMinutes    = ISNULL(bd.BothWorkedMinutes, 0),        
        
--        -- choose ONE minutes value for attendance (NO double count)        
--        --TotalDailyWorkingMinutes =        
--        --CASE        
--        --    WHEN ISNULL(pc.IsRegularize,0) = 1        
--        --         AND NULLIF(pc.RegularizePunchIn,'00:00:00')  IS NOT NULL        
--        --         AND NULLIF(pc.RegularizePuncOut,'00:00:00') IS NOT NULL        
--        --         AND DATEDIFF(MINUTE, pc.RegularizePunchIn, pc.RegularizePuncOut) > 0        
--        --    THEN DATEDIFF(MINUTE, pc.RegularizePunchIn, pc.RegularizePuncOut)        
        
--        --    WHEN (ISNULL(gd.GeoTotalPunches,0) > 0 AND ISNULL(pc.PunchCount,0) > 0)        
--        --    THEN ISNULL(bd.BothWorkedMinutes,0)        
        
--        --    WHEN ISNULL(gd.GeoTotalPunches,0) > 0 THEN ISNULL(gd.GeoWorkedMinutes,0)        
--        --    WHEN dm.TotalDailyWorkingMinutes IS NOT NULL THEN dm.TotalDailyWorkingMinutes        
--        --    ELSE 0        
--        --END,        
--      TotalDailyWorkingMinutes = --CASE --    WHEN ISNULL(pc.IsRegularize,0) = 1 --         AND NULLIF(pc.RegularizePunchIn,'00:00:00')  IS NOT NULL --         AND NULLIF(pc.RegularizePuncOut,'00:00:00') IS NOT NULL --         AND DATEDIFF(MINUTE, pc.RegularizePunchIn, pc.RegularizePuncOut) > 0 --    THEN DATEDIFF(MINUTE, pc.RegularizePunchIn, pc.RegularizePuncOut)  --    WHEN (ISNULL(gd.GeoTotalPunches,0) > 0 AND ISNULL(pc.PunchCount,0) > 0) --    THEN ISNULL(bd.BothWorkedMinutes,0)  --    WHEN ISNULL(gd.GeoTotalPunches,0) > 0 --    THEN ISNULL(gd.GeoWorkedMinutes,0)  --    WHEN ISNULL(dm.TotalDailyWorkingMinutes,0) > 0 --    THEN dm.TotalDailyWorkingMinutes  --    WHEN ef.EffectiveInDT IS NOT NULL --         AND ef.EffectiveOutDT IS NOT NULL --      AND DATEDIFF(MINUTE, ef.EffectiveInDT, ef.EffectiveOutDT) > 0 --    THEN DATEDIFF(MINUTE, ef.EffectiveInDT, ef.EffectiveOutDT)  --    ELSE 0 --END,  
--        HasEffectivePair =        
--            CASE        
--                WHEN (ISNULL(gd.GeoTotalPunches,0) > 0 AND ISNULL(pc.PunchCount,0) > 0)        
--                     AND ISNULL(bd.BothTotalPunches,0) % 2 = 0        
--                     AND ISNULL(bd.BothWorkedMinutes,0) > 0        
--                THEN 1        
--                WHEN ISNULL(gd.GeoTotalPunches,0) > 0 AND gd.GeoStatus = 'GF' THEN 1        
--                WHEN dm.HasEffectivePair = 1 THEN 1        
--                WHEN ef.EffectiveInDT IS NOT NULL AND ef.EffectiveOutDT IS NOT NULL        
--                     AND DATEDIFF(MINUTE, ef.EffectiveInDT, ef.EffectiveOutDT) > 0 THEN 1        
--                ELSE 0        
--            END,        
        
--        -- IMPORTANT: keep using column name GeoStatus so the rest of your procedure remains unchanged        
--        GeoStatus =        
--            CASE        
--                WHEN (ISNULL(gd.GeoTotalPunches,0) > 0 AND ISNULL(pc.PunchCount,0) > 0) THEN ISNULL(bd.BothStatus,'')        
--                ELSE ISNULL(gd.GeoStatus,'')        
--            END,        
        
--        GeoAddress = gd.Address        
--    INTO #DwmFinal        
--    FROM #EDG edg        
--    LEFT JOIN #GeoDaily   gd ON gd.EmployeeId = edg.EmployeeId AND gd.PunchDate = edg.PunchDate        
--    LEFT JOIN #PunchCalc  pc ON pc.EmployeeId = edg.EmployeeId AND pc.PunchDate = edg.PunchDate        
--    LEFT JOIN #DwmMachine dm ON dm.EmployeeId = edg.EmployeeId AND dm.PunchDate = edg.PunchDate        
--    LEFT JOIN #Eff        ef ON ef.EmployeeId = edg.EmployeeId AND ef.PunchDate = edg.PunchDate        
--    LEFT JOIN #BothDaily  bd ON bd.EmployeeId = edg.EmployeeId AND bd.PunchDate = edg.PunchDate;        
        
--    CREATE CLUSTERED INDEX IX_#DwmFinal ON #DwmFinal(EmployeeId, PunchDate);        
        
    /*==========================================================        
      TVF Extra: EffChosen (UPDATED: BOTH uses merged first/last)        
    ==========================================================*/    
 /*==========================================================  
  TVF Extra: EffChosen  
  UPDATED:  
    - BOTH uses merged first/last  
    - MACHINE + cross-midnight uses machine work-date first/last  
==========================================================*/  
IF OBJECT_ID('tempdb..#EffChosen') IS NOT NULL DROP TABLE #EffChosen;  
  
SELECT  
    ef.EmployeeId,  
    ef.PunchDate,  
    ef.LocationId,  
    ef.IsFlexibleShift,  
    ef.ShiftStartDT,  
    UsedInDT  =  
        CASE  
            WHEN df.SourceFlag = 'BOTH'  
                 AND bd.BothFirstInDT IS NOT NULL  
            THEN bd.BothFirstInDT  
  
            WHEN df.SourceFlag = 'MACHINE'  
                 AND ef.IsCrossMidnight = 1  
                 AND md.MachineFirstInDT IS NOT NULL  
            THEN md.MachineFirstInDT  
  
            WHEN df.GeoStatus = 'GF'  
                 AND gd.GeoFirstInUtc IS NOT NULL  
            THEN gd.GeoFirstInUtc  
  
            ELSE ef.EffectiveInDT  
        END,  
    UsedOutDT =  
        CASE  
            WHEN df.SourceFlag = 'BOTH'  
                 AND bd.BothLastOutDT IS NOT NULL  
            THEN bd.BothLastOutDT  
  
            WHEN df.SourceFlag = 'MACHINE'  
                 AND ef.IsCrossMidnight = 1  
                 AND md.MachineLastOutDT IS NOT NULL  
            THEN md.MachineLastOutDT  
  
            WHEN df.GeoStatus = 'GF'  
                 AND gd.GeoLastOutUtc IS NOT NULL  
            THEN gd.GeoLastOutUtc  
  
            ELSE ef.EffectiveOutDT  
        END  
INTO #EffChosen  
FROM #Eff ef  
LEFT JOIN #DwmFinal    df ON df.EmployeeId = ef.EmployeeId AND df.PunchDate = ef.PunchDate  
LEFT JOIN #GeoDaily    gd ON gd.EmployeeId = ef.EmployeeId AND gd.PunchDate = ef.PunchDate  
LEFT JOIN #BothDaily   bd ON bd.EmployeeId = ef.EmployeeId AND bd.PunchDate = ef.PunchDate  
LEFT JOIN #MachineDaily md ON md.EmployeeId = ef.EmployeeId AND md.PunchDate = ef.PunchDate;  
  
CREATE CLUSTERED INDEX IX_#EffChosen ON #EffChosen(EmployeeId, PunchDate);  
    --IF OBJECT_ID('tempdb..#EffChosen') IS NOT NULL DROP TABLE #EffChosen;        
        
    --SELECT        
    --    ef.EmployeeId,        
    --    ef.PunchDate,        
    --    ef.LocationId,        
    --    ef.IsFlexibleShift,        
    --    ef.ShiftStartDT,        
    --    UsedInDT  =        
    --        CASE        
    --            WHEN df.SourceFlag = 'BOTH' AND bd.BothFirstInDT IS NOT NULL THEN bd.BothFirstInDT        
    --            WHEN df.GeoStatus = 'GF' AND gd.GeoFirstInUtc IS NOT NULL THEN gd.GeoFirstInUtc        
    --            ELSE ef.EffectiveInDT        
    --        END,        
    --    UsedOutDT =        
    --        CASE        
    --            WHEN df.SourceFlag = 'BOTH' AND bd.BothLastOutDT IS NOT NULL THEN bd.BothLastOutDT        
    --            WHEN df.GeoStatus = 'GF' AND gd.GeoLastOutUtc IS NOT NULL THEN gd.GeoLastOutUtc        
    --            ELSE ef.EffectiveOutDT        
    --        END        
    --INTO #EffChosen        
    --FROM #Eff ef        
    --LEFT JOIN #DwmFinal df ON df.EmployeeId = ef.EmployeeId AND df.PunchDate = ef.PunchDate        
    --LEFT JOIN #GeoDaily gd ON gd.EmployeeId = ef.EmployeeId AND gd.PunchDate = ef.PunchDate        
    --LEFT JOIN #BothDaily bd ON bd.EmployeeId = ef.EmployeeId AND bd.PunchDate = ef.PunchDate;        
        
    --CREATE CLUSTERED INDEX IX_#EffChosen ON #EffChosen(EmployeeId, PunchDate);        
        
    /*==========================================================        
      TVF Extra: Grace / ForcedHalfDay        
    ==========================================================*/        
    IF OBJECT_ID('tempdb..#GraceDaysTracking') IS NOT NULL DROP TABLE #GraceDaysTracking;        
        
    SELECT        
        edg.EmployeeId,        
        edg.PunchDate,        
        edg.LocationId,        
        edg.IsFlexibleShift,        
        WeekStartMonday = DATEADD(DAY, -((DATEPART(WEEKDAY, edg.PunchDate) + 5) % 7), edg.PunchDate),        
        ec.ShiftStartDT,        
        ec.UsedInDT,        
        df.TotalDailyWorkingMinutes,        
        LateGraceMinutes = ISNULL(pol.LateGraceMinutes, 4),        
        
        IsLate = CASE        
                   WHEN edg.IsHoliday = 1 THEN 0        
                   WHEN ec.UsedInDT IS NULL THEN 0        
                   WHEN ISNULL(df.TotalDailyWorkingMinutes,0) < @FullDayMinutes THEN 0        
                   WHEN DATEDIFF(MINUTE, ec.ShiftStartDT, ec.UsedInDT) > ISNULL(pol.LateGraceMinutes, 4)        
                   THEN 1 ELSE 0        
                 END        
    INTO #GraceDaysTracking        
    FROM #EDG edg        
    LEFT JOIN #EffChosen ec ON ec.EmployeeId = edg.EmployeeId AND ec.PunchDate = edg.PunchDate        
    LEFT JOIN #DwmFinal df  ON df.EmployeeId = edg.EmployeeId AND df.PunchDate = edg.PunchDate        
    OUTER APPLY        
    (        
        SELECT TOP (1) p.LateGraceMinutes        
        FROM dbo.AttendanceLatePolicy p WITH (NOLOCK)        
        WHERE p.IsActive = 1        
          AND p.EffectiveFromDate <= edg.PunchDate        
          AND (p.LocationId IS NULL OR p.LocationId = edg.LocationId)        
          AND (p.IsFlexibleShift IS NULL OR p.IsFlexibleShift = edg.IsFlexibleShift)        
        ORDER BY        
          CASE WHEN p.LocationId IS NULL THEN 1 ELSE 0 END,        
          CASE WHEN p.IsFlexibleShift IS NULL THEN 1 ELSE 0 END,        
          p.EffectiveFromDate DESC        
    ) pol;        
        
    CREATE CLUSTERED INDEX IX_#GraceDaysTracking ON #GraceDaysTracking(EmployeeId, PunchDate);        
        
    IF OBJECT_ID('tempdb..#GraceCounter') IS NOT NULL DROP TABLE #GraceCounter;        
        
    SELECT        
        g.*,        
        PriorLates =        
        (        
            SELECT COUNT(*)        
            FROM #GraceDaysTracking x        
            WHERE x.EmployeeId = g.EmployeeId        
              AND x.WeekStartMonday = g.WeekStartMonday        
              AND x.PunchDate < g.PunchDate        
              AND x.LocationId = 313        
              AND x.IsFlexibleShift = 0        
              AND x.IsLate = 1        
              AND ISNULL(x.TotalDailyWorkingMinutes,0) >= @FullDayMinutes        
              AND DATEPART(WEEKDAY, x.PunchDate) BETWEEN 2 AND 6        
        )        
    INTO #GraceCounter        
    FROM #GraceDaysTracking g;        
        
    CREATE CLUSTERED INDEX IX_#GraceCounter ON #GraceCounter(EmployeeId, PunchDate);        
        
IF OBJECT_ID('tempdb..#ForcedHalfDay') IS NOT NULL DROP TABLE #ForcedHalfDay;

SELECT
    gc.EmployeeId,
    gc.PunchDate,
    IsForcedHalfDay =
        CASE
          WHEN gc.LocationId = 313
           AND gc.IsFlexibleShift = 0
           AND gc.PunchDate >= '2025-09-16'
           AND DATEPART(WEEKDAY, gc.PunchDate) BETWEEN 2 AND 6
           AND gc.IsLate = 1
           AND gc.PriorLates >= 2
           AND NOT EXISTS
           (
               SELECT 1
               FROM #EDG edg
               WHERE edg.EmployeeId = gc.EmployeeId
                 AND edg.PunchDate  = gc.PunchDate
                 AND edg.IsCrossMidnight = 1
           )
           AND EXISTS
           (
               SELECT 1
               FROM #DwmFinal mt
               WHERE mt.EmployeeId = gc.EmployeeId
                 AND mt.PunchDate  = gc.PunchDate
                 AND ISNULL(mt.TotalDailyWorkingMinutes,0) >= @FullDayMinutes
           )
          THEN 1 ELSE 0
        END
INTO #ForcedHalfDay
FROM #GraceCounter gc;

CREATE CLUSTERED INDEX IX_#ForcedHalfDay ON #ForcedHalfDay(EmployeeId, PunchDate);       
        
    /*==========================================================        
      13) Monthly (MUST exist)        
    ==========================================================*/        
    IF OBJECT_ID('tempdb..#Monthly') IS NOT NULL DROP TABLE #Monthly;        
        
    SELECT        
        EmployeeId,        
        TotalMonthlyWorkingHours =        
            CAST(SUM(TotalDailyWorkingMinutes) / 60 AS VARCHAR(10)) + ' hours and ' +        
            RIGHT('0' + CAST(SUM(TotalDailyWorkingMinutes) % 60 AS VARCHAR(2)), 2) + ' minutes'        
    INTO #Monthly       
    FROM #DwmFinal        
    GROUP BY EmployeeId;        
        
    CREATE UNIQUE CLUSTERED INDEX IX_#Monthly ON #Monthly(EmployeeId);        
        
    /*==========================================================        
      14) Carry-over (MUST exist)        
      UPDATED: include BOTH / GEO-only morning single punch as carryover too        
    ==========================================================*/        
    IF OBJECT_ID('tempdb..#Carry') IS NOT NULL DROP TABLE #Carry;        
        
    SELECT        
      edg.EmployeeId,        
      edg.PunchDate,        
      IsCarryOver =        
        CASE        
          WHEN prev.IsCrossMidnight = 1        
           AND prev.PunchDate IS NOT NULL        
           AND        
           (        
                /* BOTH: exactly 1 merged punch and it is morning (<= 14:00) */        
                (df.SourceFlag = 'BOTH'        
                 AND ISNULL(bd.BothTotalPunches,0) = 1        
                 AND bd.BothFirstInDT IS NOT NULL        
                 AND CAST(bd.BothFirstInDT AS TIME(0)) <= '14:00:00')        
        
                /* GEO-only: exactly 1 geo punch and it is morning (<= 14:00) */        
                OR        
                (df.SourceFlag = 'GEO'        
                 AND ISNULL(gd.GeoTotalPunches,0) = 1        
                 AND gd.GeoFirstInUtc IS NOT NULL        
                 AND CAST(gd.GeoFirstInUtc AS TIME(0)) <= '14:00:00')        
        
                /* MACHINE-only: your existing rule */        
                OR        
                (ISNULL(pc.PunchCount,0) = 1        
                 AND CAST(pc.PunchIn AS TIME(0)) <= '14:00:00')        
           )        
          THEN 1 ELSE 0        
        END        
    INTO #Carry        
    FROM #EDG edg        
    LEFT JOIN #EDG prev        
      ON prev.EmployeeId = edg.EmployeeId        
     AND prev.PunchDate  = DATEADD(DAY,-1, edg.PunchDate)        
    LEFT JOIN #PunchCalc pc        
      ON pc.EmployeeId = edg.EmployeeId        
     AND pc.PunchDate  = edg.PunchDate        
    LEFT JOIN #DwmFinal df        
      ON df.EmployeeId = edg.EmployeeId        
     AND df.PunchDate  = edg.PunchDate        
    LEFT JOIN #GeoDaily gd        
      ON gd.EmployeeId = edg.EmployeeId        
     AND gd.PunchDate  = edg.PunchDate        
    LEFT JOIN #BothDaily bd        
      ON bd.EmployeeId = edg.EmployeeId        
     AND bd.PunchDate  = edg.PunchDate;        
        
    CREATE CLUSTERED INDEX IX_#Carry ON #Carry(EmployeeId, PunchDate);        
        
    /*==========================================================        
      15) WorkDay + Agg (as per your logic)        
      UPDATED: odd punches => decide by minutes (BOTH-aware)        
    ==========================================================*/        
    IF OBJECT_ID('tempdb..#WorkDay') IS NOT NULL DROP TABLE #WorkDay;        
        
    SELECT        
        edg.EmployeeId,        
        edg.PunchDate,        
        WorkDayValue =        
            CASE        
                WHEN edg.IsFutureDate = 1 THEN 0.0        
                WHEN edg.IsHoliday   = 1 THEN 0.0        
        
                WHEN ld.EmployeeId IS NOT NULL        
                 AND ISNULL(df.TotalDailyWorkingMinutes,0) = 0        
                 AND ISNULL(pc.PunchCount,0) = 0        
                 AND ISNULL(gd.GeoTotalPunches,0) = 0        
                THEN 0.0        
        
                WHEN c.IsCarryOver = 1 THEN 0.0        
        
                /* TVF special override */        
                WHEN edg.PunchDate = '2025-10-18'        
                     AND edg.LocationId = 313        
                     AND (edg.ShiftName = 'General Shift' OR edg.ShiftName = 'Flexible Shift')        
                     AND ISNULL(pc.IsRegularize,0) = 0        
                     AND (        
                          (df.GeoStatus='GF'        
                           AND gd.GeoFirstInUtc IS NOT NULL AND CAST(gd.GeoFirstInUtc AS TIME(0)) <  '09:00:00'        
                           AND gd.GeoLastOutUtc IS NOT NULL AND CAST(gd.GeoLastOutUtc AS TIME(0)) >= '15:30:00'        
                           AND ISNULL(df.TotalDailyWorkingMinutes,0) >= 360        
                          )        
                          OR        
                          (df.GeoStatus<>'GF'        
                           AND ef.EffectivePunchInTime  IS NOT NULL AND ef.EffectivePunchInTime  <  '09:00:00'        
                           AND ef.EffectivePunchOutTime IS NOT NULL AND ef.EffectivePunchOutTime >= '15:30:00'        
                           AND ISNULL(df.TotalDailyWorkingMinutes,0) >= 360        
                          )        
                     )        
                THEN 1.0        
        
                WHEN edg.IsWeekend = 1 AND ISNULL(df.TotalDailyWorkingMinutes,0) = 0 THEN 0.0        
        
                /* ForcedHalfDay => 0.75 */        
                WHEN fh.IsForcedHalfDay = 1 THEN 0.75        
        
                /* Regularize full day */        
                WHEN pc.IsRegularize = 1        
                 AND NULLIF(pc.RegularizePunchIn,'00:00:00')  IS NOT NULL        
                 AND NULLIF(pc.RegularizePuncOut,'00:00:00') IS NOT NULL        
                 AND DATEDIFF(MINUTE, pc.RegularizePunchIn, pc.RegularizePuncOut) >= @FullDayMinutes        
                THEN 1.0        
        
                /* UPDATED: Odd punches => decide by minutes (BOTH-aware via df.HasEffectivePair + df.TotalDailyWorkingMinutes) */        
                WHEN ((     
    CASE   WHEN df.SourceFlag='BOTH' THEN ISNULL(bd.BothTotalPunches,0)   WHEN df.SourceFlag='GEO'  THEN ISNULL(gd.GeoTotalPunches,0)   WHEN df.SourceFlag='MACHINE' AND edg.IsCrossMidnight = 1 THEN ISNULL(md.MachineTotalPunches,0)   ELSE ISNULL(pc.EffectivePunchCount, ISNULL(pc.PunchCount,0)) END  
                        --CASE        
                        --  WHEN df.SourceFlag='BOTH' THEN ISNULL(bd.BothTotalPunches,0)        
                        --  WHEN df.SourceFlag='GEO'  THEN ISNULL(gd.GeoTotalPunches,0)        
                        --  ELSE ISNULL(pc.EffectivePunchCount, ISNULL(pc.PunchCount,0))        
                        --END        
                      ) % 2 = 1)        
                  AND (     
      CASE   WHEN df.SourceFlag='BOTH' THEN ISNULL(bd.BothTotalPunches,0)   WHEN df.SourceFlag='GEO'  THEN ISNULL(gd.GeoTotalPunches,0)   WHEN df.SourceFlag='MACHINE' AND edg.IsCrossMidnight = 1 THEN ISNULL(md.MachineTotalPunches,0)   ELSE ISNULL(pc.EffectivePunchCount, ISNULL(pc.PunchCount,0)) END  
                        --CASE        
                        --  WHEN df.SourceFlag='BOTH' THEN ISNULL(bd.BothTotalPunches,0)        
                        --  WHEN df.SourceFlag='GEO'  THEN ISNULL(gd.GeoTotalPunches,0)        
                        --  ELSE ISNULL(pc.EffectivePunchCount, ISNULL(pc.PunchCount,0))        
                        --END        
                      ) > 0        
                  AND ISNULL(df.HasEffectivePair,0) = 0        
 THEN        
                    CASE        
                        WHEN ISNULL(df.TotalDailyWorkingMinutes,0) >= @FullDayMinutes THEN 1.0        
                        WHEN ISNULL(df.TotalDailyWorkingMinutes,0) >= @HalfDayMinutes THEN 0.5        
                        ELSE 0.0     
                    END        
        
                WHEN ISNULL(df.TotalDailyWorkingMinutes,0) >= @FullDayMinutes THEN 1.0        
                WHEN ISNULL(df.TotalDailyWorkingMinutes,0) >= @HalfDayMinutes THEN 0.5        
                ELSE 0.0        
            END        
    INTO #WorkDay        
    FROM #EDG edg        
    LEFT JOIN #LeaveDays ld ON ld.EmployeeId = edg.EmployeeId AND ld.PunchDate = edg.PunchDate        
    LEFT JOIN #DwmFinal df  ON df.EmployeeId = edg.EmployeeId AND df.PunchDate = edg.PunchDate        
    LEFT JOIN #PunchCalc pc ON pc.EmployeeId = edg.EmployeeId AND pc.PunchDate = edg.PunchDate        
    LEFT JOIN #Eff ef       ON ef.EmployeeId = edg.EmployeeId AND ef.PunchDate = edg.PunchDate        
    LEFT JOIN #GeoDaily gd  ON gd.EmployeeId = edg.EmployeeId AND gd.PunchDate = edg.PunchDate        
    LEFT JOIN #Carry c      ON c.EmployeeId = edg.EmployeeId AND c.PunchDate = edg.PunchDate        
    LEFT JOIN #ForcedHalfDay fh ON fh.EmployeeId = edg.EmployeeId AND fh.PunchDate = edg.PunchDate        
    LEFT JOIN #BothDaily bd ON bd.EmployeeId = edg.EmployeeId AND bd.PunchDate = edg.PunchDate  
 LEFT JOIN #MachineDaily md ON md.EmployeeId = edg.EmployeeId AND md.PunchDate = edg.PunchDate  
 ;        
        
    CREATE CLUSTERED INDEX IX_#WorkDay ON #WorkDay(EmployeeId, PunchDate);        
        
    IF OBJECT_ID('tempdb..#WorkDayAgg') IS NOT NULL DROP TABLE #WorkDayAgg;        
        
    SELECT EmployeeId, TotalWorkingDays = SUM(WorkDayValue)        
    INTO #WorkDayAgg        
    FROM #WorkDay        
    GROUP BY EmployeeId;        
        
    CREATE UNIQUE CLUSTERED INDEX IX_#WorkDayAgg ON #WorkDayAgg(EmployeeId);        
       
    /*==========================================================        
      16) FINAL OUTPUT  (ONLY requested columns)        
      UPDATED: BOTH uses merged PunchIn/Out + merged Punch1..12 + merged ValidPunchCount        
    ==========================================================*/        
    SELECT        
        EmpAttendanceId = CAST(0 AS INT),        
        edg.EmployeeId,        
        edg.EmployeeName,        
        edg.ECode,        
        AttendanceDate = edg.PunchDate,        
        
        Machine_Type = CASE WHEN c.IsCarryOver = 1 THEN '' ELSE ISNULL(pc.Machine_Type,'') END,        
        
        DesignationName = ISNULL(edg.DesignationName, 'N/A'),        
        LocationName    = ISNULL(edg.LocationName, 'N/A'),        
        STCode          = ISNULL(edg.STCode, 'N/A'),        
        DepartmentName  = ISNULL(edg.DepartmentName, 'N/A'),        
        
        ShiftName      = ISNULL(edg.ShiftName,''),        
        ShiftStartTime = edg.ShiftStartTime,        
        ShiftEndTime   = edg.ShiftEndTime,        
        
        IsHoliday   = edg.IsHoliday,        
        HolidayName = edg.HolidayName,        
      PunchIn = CASE   WHEN c.IsCarryOver = 1 THEN CAST('00:00:00' AS TIME(0))   WHEN df.SourceFlag = 'BOTH' AND bd.BothFirstInDT IS NOT NULL THEN CAST(bd.BothFirstInDT AS TIME(0))   WHEN df.SourceFlag = 'MACHINE' AND edg.IsCrossMidnight = 1 AND md.MachineFirstInDT IS NOT NULL THEN CAST(md.MachineFirstInDT AS TIME(0))   WHEN df.GeoStatus IN ('GF') AND gd.GeoFirstInUtc IS NOT NULL THEN CAST(gd.GeoFirstInUtc AS TIME(0))   ELSE ISNULL(CAST(ec.UsedInDT AS TIME(0)), CAST('00:00:00' AS TIME(0))) END,  
        --PunchIn =        
        --CASE        
        --  WHEN c.IsCarryOver = 1 THEN CAST('00:00:00' AS TIME(0))        
        --  WHEN df.SourceFlag = 'BOTH' AND bd.BothFirstInDT IS NOT NULL THEN CAST(bd.BothFirstInDT AS TIME(0))        
        --  WHEN df.GeoStatus IN ('GF') AND gd.GeoFirstInUtc IS NOT NULL THEN CAST(gd.GeoFirstInUtc AS TIME(0))        
        --  ELSE ISNULL(CAST(ec.UsedInDT AS TIME(0)), CAST('00:00:00' AS TIME(0)))        
        --END,        
        
        --PunchOut =        
        --CASE        
        --  WHEN c.IsCarryOver = 1 THEN CAST('00:00:00' AS TIME(0))        
        --  WHEN df.SourceFlag = 'BOTH' AND bd.BothLastOutDT IS NOT NULL THEN CAST(bd.BothLastOutDT AS TIME(0))        
        --  WHEN df.GeoStatus IN ('GF') AND gd.GeoFirstInUtc IS NOT NULL THEN CAST(gd.GeoLastOutUtc AS TIME(0))        
        --  ELSE ISNULL(CAST(ec.UsedOutDT AS TIME(0)), CAST('00:00:00' AS TIME(0)))        
        --END,        
      PunchOut =  
CASE  
  WHEN c.IsCarryOver = 1 THEN CAST('00:00:00' AS TIME(0))  
  WHEN df.SourceFlag = 'BOTH' AND bd.BothLastOutDT IS NOT NULL THEN CAST(bd.BothLastOutDT AS TIME(0))  
  WHEN df.SourceFlag = 'MACHINE' AND edg.IsCrossMidnight = 1 AND md.MachineLastOutDT IS NOT NULL THEN CAST(md.MachineLastOutDT AS TIME(0))  
  WHEN df.GeoStatus IN ('GF') AND gd.GeoLastOutUtc IS NOT NULL THEN CAST(gd.GeoLastOutUtc AS TIME(0))  
  ELSE ISNULL(CAST(ec.UsedOutDT AS TIME(0)), CAST('00:00:00' AS TIME(0)))  
END,  
        /* Punch1..Punch12:        
           - CarryOver => 00:00:00        
           - BOTH => merged punches        
           - else => machine punches        
        */  
        Punch1  = CASE WHEN c.IsCarryOver=1 THEN '00:00:00'
               WHEN df.SourceFlag='BOTH' THEN ISNULL(CONVERT(VARCHAR(8), bc.Punch1, 108),'00:00:00')
               WHEN df.SourceFlag='MACHINE' AND edg.IsCrossMidnight = 1 THEN ISNULL(CONVERT(VARCHAR(8), mpc.Punch1, 108),'00:00:00')
               ELSE ISNULL(pc.Punch1,'00:00:00') END,

Punch2  = CASE WHEN c.IsCarryOver=1 THEN '00:00:00'
               WHEN df.SourceFlag='BOTH' THEN ISNULL(CONVERT(VARCHAR(8), bc.Punch2, 108),'00:00:00')
               WHEN df.SourceFlag='MACHINE' AND edg.IsCrossMidnight = 1 THEN ISNULL(CONVERT(VARCHAR(8), mpc.Punch2, 108),'00:00:00')
               ELSE ISNULL(pc.Punch2,'00:00:00') END,

Punch3  = CASE WHEN c.IsCarryOver=1 THEN '00:00:00'
               WHEN df.SourceFlag='BOTH' THEN ISNULL(CONVERT(VARCHAR(8), bc.Punch3, 108),'00:00:00')
               WHEN df.SourceFlag='MACHINE' AND edg.IsCrossMidnight = 1 THEN ISNULL(CONVERT(VARCHAR(8), mpc.Punch3, 108),'00:00:00')
               ELSE ISNULL(pc.Punch3,'00:00:00') END,

Punch4  = CASE WHEN c.IsCarryOver=1 THEN '00:00:00'
               WHEN df.SourceFlag='BOTH' THEN ISNULL(CONVERT(VARCHAR(8), bc.Punch4, 108),'00:00:00')
               WHEN df.SourceFlag='MACHINE' AND edg.IsCrossMidnight = 1 THEN ISNULL(CONVERT(VARCHAR(8), mpc.Punch4, 108),'00:00:00')
               ELSE ISNULL(pc.Punch4,'00:00:00') END,

Punch5  = CASE WHEN c.IsCarryOver=1 THEN '00:00:00'
               WHEN df.SourceFlag='BOTH' THEN ISNULL(CONVERT(VARCHAR(8), bc.Punch5, 108),'00:00:00')
               WHEN df.SourceFlag='MACHINE' AND edg.IsCrossMidnight = 1 THEN ISNULL(CONVERT(VARCHAR(8), mpc.Punch5, 108),'00:00:00')
               ELSE ISNULL(pc.Punch5,'00:00:00') END,

Punch6  = CASE WHEN c.IsCarryOver=1 THEN '00:00:00'
               WHEN df.SourceFlag='BOTH' THEN ISNULL(CONVERT(VARCHAR(8), bc.Punch6, 108),'00:00:00')
               WHEN df.SourceFlag='MACHINE' AND edg.IsCrossMidnight = 1 THEN ISNULL(CONVERT(VARCHAR(8), mpc.Punch6, 108),'00:00:00')
               ELSE ISNULL(pc.Punch6,'00:00:00') END,

Punch7  = CASE WHEN c.IsCarryOver=1 THEN '00:00:00'
               WHEN df.SourceFlag='BOTH' THEN ISNULL(CONVERT(VARCHAR(8), bc.Punch7, 108),'00:00:00')
               WHEN df.SourceFlag='MACHINE' AND edg.IsCrossMidnight = 1 THEN ISNULL(CONVERT(VARCHAR(8), mpc.Punch7, 108),'00:00:00')
               ELSE ISNULL(pc.Punch7,'00:00:00') END,

Punch8  = CASE WHEN c.IsCarryOver=1 THEN '00:00:00'
               WHEN df.SourceFlag='BOTH' THEN ISNULL(CONVERT(VARCHAR(8), bc.Punch8, 108),'00:00:00')
               WHEN df.SourceFlag='MACHINE' AND edg.IsCrossMidnight = 1 THEN ISNULL(CONVERT(VARCHAR(8), mpc.Punch8, 108),'00:00:00')
               ELSE ISNULL(pc.Punch8,'00:00:00') END,

Punch9  = CASE WHEN c.IsCarryOver=1 THEN '00:00:00'
               WHEN df.SourceFlag='BOTH' THEN ISNULL(CONVERT(VARCHAR(8), bc.Punch9, 108),'00:00:00')
               WHEN df.SourceFlag='MACHINE' AND edg.IsCrossMidnight = 1 THEN ISNULL(CONVERT(VARCHAR(8), mpc.Punch9, 108),'00:00:00')
               ELSE ISNULL(pc.Punch9,'00:00:00') END,

Punch10 = CASE WHEN c.IsCarryOver=1 THEN '00:00:00'
               WHEN df.SourceFlag='BOTH' THEN ISNULL(CONVERT(VARCHAR(8), bc.Punch10, 108),'00:00:00')
               WHEN df.SourceFlag='MACHINE' AND edg.IsCrossMidnight = 1 THEN ISNULL(CONVERT(VARCHAR(8), mpc.Punch10, 108),'00:00:00')
               ELSE ISNULL(pc.Punch10,'00:00:00') END,

Punch11 = CASE WHEN c.IsCarryOver=1 THEN '00:00:00'
               WHEN df.SourceFlag='BOTH' THEN ISNULL(CONVERT(VARCHAR(8), bc.Punch11, 108),'00:00:00')
               WHEN df.SourceFlag='MACHINE' AND edg.IsCrossMidnight = 1 THEN ISNULL(CONVERT(VARCHAR(8), mpc.Punch11, 108),'00:00:00')
               ELSE ISNULL(pc.Punch11,'00:00:00') END,

Punch12 = CASE WHEN c.IsCarryOver=1 THEN '00:00:00'
               WHEN df.SourceFlag='BOTH' THEN ISNULL(CONVERT(VARCHAR(8), bc.Punch12, 108),'00:00:00')
               WHEN df.SourceFlag='MACHINE' AND edg.IsCrossMidnight = 1 THEN ISNULL(CONVERT(VARCHAR(8), mpc.Punch12, 108),'00:00:00')
               ELSE ISNULL(pc.Punch12,'00:00:00') END,
        --Punch1  = CASE WHEN c.IsCarryOver=1 THEN '00:00:00'                WHEN df.SourceFlag='BOTH' THEN ISNULL(CONVERT(VARCHAR(8), bc.Punch1, 108),'00:00:00')                WHEN df.SourceFlag='MACHINE' AND edg.IsCrossMidnight = 1 THEN ISNULL(CONVERT(VARCHAR(8), mpc.Punch1, 108),'00:00:00')                ELSE ISNULL(pc.Punch1,'00:00:00') END,       
        --Punch2  = CASE WHEN c.IsCarryOver=1 THEN '00:00:00'                WHEN df.SourceFlag='BOTH' THEN ISNULL(CONVERT(VARCHAR(8), bc.Punch1, 108),'00:00:00')                WHEN df.SourceFlag='MACHINE' AND edg.IsCrossMidnight = 1 THEN ISNULL(CONVERT(VARCHAR(8), mpc.Punch1, 108),'00:00:00')                ELSE ISNULL(pc.Punch1,'00:00:00') END,      
        --Punch3  = CASE WHEN c.IsCarryOver=1 THEN '00:00:00'                WHEN df.SourceFlag='BOTH' THEN ISNULL(CONVERT(VARCHAR(8), bc.Punch1, 108),'00:00:00')                WHEN df.SourceFlag='MACHINE' AND edg.IsCrossMidnight = 1 THEN ISNULL(CONVERT(VARCHAR(8), mpc.Punch1, 108),'00:00:00')                ELSE ISNULL(pc.Punch1,'00:00:00') END,       
        --Punch4  = CASE WHEN c.IsCarryOver=1 THEN '00:00:00'                WHEN df.SourceFlag='BOTH' THEN ISNULL(CONVERT(VARCHAR(8), bc.Punch1, 108),'00:00:00')                WHEN df.SourceFlag='MACHINE' AND edg.IsCrossMidnight = 1 THEN ISNULL(CONVERT(VARCHAR(8), mpc.Punch1, 108),'00:00:00')                ELSE ISNULL(pc.Punch1,'00:00:00') END,     
        --Punch5  = CASE WHEN c.IsCarryOver=1 THEN '00:00:00'                WHEN df.SourceFlag='BOTH' THEN ISNULL(CONVERT(VARCHAR(8), bc.Punch1, 108),'00:00:00')                WHEN df.SourceFlag='MACHINE' AND edg.IsCrossMidnight = 1 THEN ISNULL(CONVERT(VARCHAR(8), mpc.Punch1, 108),'00:00:00')                ELSE ISNULL(pc.Punch1,'00:00:00') END,       
        --Punch6  = CASE WHEN c.IsCarryOver=1 THEN '00:00:00'                WHEN df.SourceFlag='BOTH' THEN ISNULL(CONVERT(VARCHAR(8), bc.Punch1, 108),'00:00:00')                WHEN df.SourceFlag='MACHINE' AND edg.IsCrossMidnight = 1 THEN ISNULL(CONVERT(VARCHAR(8), mpc.Punch1, 108),'00:00:00')                ELSE ISNULL(pc.Punch1,'00:00:00') END,       
        --Punch7  = CASE WHEN c.IsCarryOver=1 THEN '00:00:00'                WHEN df.SourceFlag='BOTH' THEN ISNULL(CONVERT(VARCHAR(8), bc.Punch1, 108),'00:00:00')                WHEN df.SourceFlag='MACHINE' AND edg.IsCrossMidnight = 1 THEN ISNULL(CONVERT(VARCHAR(8), mpc.Punch1, 108),'00:00:00')                ELSE ISNULL(pc.Punch1,'00:00:00') END,       
        --Punch8  = CASE WHEN c.IsCarryOver=1 THEN '00:00:00'                WHEN df.SourceFlag='BOTH' THEN ISNULL(CONVERT(VARCHAR(8), bc.Punch1, 108),'00:00:00')                WHEN df.SourceFlag='MACHINE' AND edg.IsCrossMidnight = 1 THEN ISNULL(CONVERT(VARCHAR(8), mpc.Punch1, 108),'00:00:00')                ELSE ISNULL(pc.Punch1,'00:00:00') END,       
        --Punch9  = CASE WHEN c.IsCarryOver=1 THEN '00:00:00'                WHEN df.SourceFlag='BOTH' THEN ISNULL(CONVERT(VARCHAR(8), bc.Punch1, 108),'00:00:00')                WHEN df.SourceFlag='MACHINE' AND edg.IsCrossMidnight = 1 THEN ISNULL(CONVERT(VARCHAR(8), mpc.Punch1, 108),'00:00:00')                ELSE ISNULL(pc.Punch1,'00:00:00') END,        
        --Punch10 = CASE WHEN c.IsCarryOver=1 THEN '00:00:00'                WHEN df.SourceFlag='BOTH' THEN ISNULL(CONVERT(VARCHAR(8), bc.Punch1, 108),'00:00:00')                WHEN df.SourceFlag='MACHINE' AND edg.IsCrossMidnight = 1 THEN ISNULL(CONVERT(VARCHAR(8), mpc.Punch1, 108),'00:00:00')                ELSE ISNULL(pc.Punch1,'00:00:00') END,      
        --Punch11 = CASE WHEN c.IsCarryOver=1 THEN '00:00:00'                WHEN df.SourceFlag='BOTH' THEN ISNULL(CONVERT(VARCHAR(8), bc.Punch1, 108),'00:00:00')                WHEN df.SourceFlag='MACHINE' AND edg.IsCrossMidnight = 1 THEN ISNULL(CONVERT(VARCHAR(8), mpc.Punch1, 108),'00:00:00')                ELSE ISNULL(pc.Punch1,'00:00:00') END,      
        --Punch12 = CASE WHEN c.IsCarryOver=1 THEN '00:00:00'                WHEN df.SourceFlag='BOTH' THEN ISNULL(CONVERT(VARCHAR(8), bc.Punch1, 108),'00:00:00')                WHEN df.SourceFlag='MACHINE' AND edg.IsCrossMidnight = 1 THEN ISNULL(CONVERT(VARCHAR(8), mpc.Punch1, 108),'00:00:00')                ELSE ISNULL(pc.Punch1,'00:00:00') END,        
        
        /* UPDATED: ValidPunchCount shift-aware + BOTH-aware */        
        --ValidPunchCount =        
        --CASE        
        --    WHEN edg.IsFutureDate = 1 THEN 0        
        --    WHEN c.IsCarryOver = 1 THEN 0        
        
        --    WHEN df.SourceFlag = 'BOTH'        
        --        THEN ISNULL(bd.BothTotalPunches,0)        
        
        --    WHEN df.SourceFlag IN ('GEO') OR df.GeoStatus IN ('GF','MISS')        
        --        THEN ISNULL(gd.GeoTotalPunches,0)        
        
        --    WHEN edg.IsCrossMidnight = 1        
        --         AND ec.UsedInDT  IS NOT NULL        
        --         AND ec.UsedOutDT IS NOT NULL        
        --         AND DATEDIFF(MINUTE, ec.UsedInDT, ec.UsedOutDT) > 0        
        --        THEN 2        
        
        --    ELSE ISNULL(pc.EffectivePunchCount, ISNULL(pc.PunchCount,0))        
        --END,        
      ValidPunchCount = CASE     WHEN edg.IsFutureDate = 1 THEN 0     WHEN c.IsCarryOver = 1 THEN 0      WHEN df.SourceFlag = 'BOTH'         THEN ISNULL(bd.BothTotalPunches,0)      WHEN df.SourceFlag = 'GEO' OR df.GeoStatus IN ('GF','MISS')         THEN ISNULL(gd.GeoTotalPunches,0)      WHEN df.SourceFlag = 'MACHINE' AND edg.IsCrossMidnight = 1         THEN ISNULL(md.MachineTotalPunches,0)      WHEN edg.IsCrossMidnight = 1          AND ec.UsedInDT IS NOT NULL          AND ec.UsedOutDT IS NOT NULL         
 AND DATEDIFF(MINUTE, ec.UsedInDT, ec.UsedOutDT) > 0         THEN 2      ELSE ISNULL(pc.EffectivePunchCount, ISNULL(pc.PunchCount,0)) END,  
        RegularizePunchIn = CASE WHEN c.IsCarryOver=1 THEN '00:00:00' ELSE ISNULL(pc.RegularizePunchIn,'00:00:00') END,        
        RegularizePuncOut = CASE WHEN c.IsCarryOver=1 THEN '00:00:00' ELSE ISNULL(pc.RegularizePuncOut,'00:00:00') END,        
        IsRegularize      = CASE WHEN c.IsCarryOver=1 THEN 0 ELSE ISNULL(pc.IsRegularize,0) END,        
        
        IsOnLeave = CASE WHEN ld.EmployeeId IS NOT NULL THEN 1 ELSE 0 END,        
        
        TotalWorkingMinutes =        
            CASE WHEN c.IsCarryOver=1 THEN CAST('00:00' AS NVARCHAR(10))        
                 ELSE RIGHT('0' + CAST(df.TotalDailyWorkingMinutes / 60 AS VARCHAR(2)),2)        
                      + ':'        
                      + RIGHT('0' + CAST(df.TotalDailyWorkingMinutes % 60 AS VARCHAR(2)),2)        
            END,        
        
      LateMinutes =
CASE
    WHEN c.IsCarryOver=1 OR edg.IsFutureDate=1 OR edg.IsHoliday=1 THEN 0
    WHEN ec.UsedInDT IS NULL OR ef.ShiftStartDT IS NULL THEN 0
    WHEN ec.UsedInDT > ef.ShiftStartDT THEN DATEDIFF(MINUTE, ef.ShiftStartDT, ec.UsedInDT)
    ELSE 0
END,      
        
        EarlyMinutes =        
            CASE        
                WHEN c.IsCarryOver=1 OR edg.IsFutureDate=1 OR edg.IsHoliday=1 THEN 0        
                WHEN ef.EffectiveInDT IS NULL OR ef.ShiftStartDT IS NULL THEN 0        
                WHEN ec.UsedInDT < ef.ShiftStartDT THEN DATEDIFF(MINUTE, ec.UsedInDT, ef.ShiftStartDT)        
                ELSE 0        
            END,        
        
        Status =        
        CASE        
            /* TVF special override */        
            WHEN edg.PunchDate = '2025-10-18'        
                 AND edg.LocationId = 313        
                 AND (edg.ShiftName = 'General Shift' OR edg.ShiftName = 'Flexible Shift')        
                 AND ISNULL(pc.IsRegularize,0) = 0        
                 AND (        
                      (df.GeoStatus='GF'        
                       AND gd.GeoFirstInUtc IS NOT NULL AND CAST(gd.GeoFirstInUtc AS TIME(0)) <  '09:00:00'        
                       AND gd.GeoLastOutUtc IS NOT NULL AND CAST(gd.GeoLastOutUtc AS TIME(0)) >= '15:30:00'        
                       AND ISNULL(df.TotalDailyWorkingMinutes,0) >= 360        
                      )        
                      OR        
                      (df.GeoStatus<>'GF'        
                       AND ef.EffectivePunchInTime  IS NOT NULL AND ef.EffectivePunchInTime  <  '09:00:00'        
                       AND ef.EffectivePunchOutTime IS NOT NULL AND ef.EffectivePunchOutTime >= '15:30:00'        
                       AND ISNULL(df.TotalDailyWorkingMinutes,0) >= 360        
                      )        
                 )        
            THEN 'Present'        
        
            WHEN edg.IsFutureDate = 1 THEN ''        
            WHEN edg.IsHoliday = 1 THEN 'Holiday'        
        
            WHEN ld.EmployeeId IS NOT NULL        
              AND ISNULL(df.TotalDailyWorkingMinutes,0) = 0        
              AND ISNULL(pc.PunchCount,0) = 0        
              AND ISNULL(gd.GeoTotalPunches,0) = 0        
            THEN 'On Leave'        
        
           WHEN c.IsCarryOver = 1 AND prev.IsHoliday = 1 THEN 'Holiday'        
            WHEN c.IsCarryOver = 1 AND prev.IsWeekend = 1 THEN 'Weekly Off'        
        
            /* Weekly off must be checked BEFORE carryover blank */        
            WHEN edg.IsWeekend = 1        
                 AND (ISNULL(df.TotalDailyWorkingMinutes,0) = 0 OR c.IsCarryOver = 1)        
            THEN 'Weekly Off'        
        
            WHEN c.IsCarryOver = 1 AND edg.IsWeekend = 0 THEN ''        
        
            WHEN edg.IsWeekend = 1        
                 AND (ISNULL(df.TotalDailyWorkingMinutes,0) = 0 OR c.IsCarryOver = 1)        
            THEN 'Weekly Off'        
        
            /* UPDATED: Odd punches => MIS / Present / Half Day Present / Absent (BOTH-aware via df.HasEffectivePair + minutes) */        
            WHEN ((        
                    CASE   WHEN df.SourceFlag='BOTH' THEN ISNULL(bd.BothTotalPunches,0)   WHEN df.SourceFlag='GEO'  THEN ISNULL(gd.GeoTotalPunches,0)   WHEN df.SourceFlag='MACHINE' AND edg.IsCrossMidnight = 1 THEN ISNULL(md.MachineTotalPunches,0)   ELSE ISNULL(pc.EffectivePunchCount, ISNULL(pc.PunchCount,0)) END       
                  ) % 2 = 1)        
              AND (        
                    CASE   WHEN df.SourceFlag='BOTH' THEN ISNULL(bd.BothTotalPunches,0)   WHEN df.SourceFlag='GEO'  THEN ISNULL(gd.GeoTotalPunches,0)   WHEN df.SourceFlag='MACHINE' AND edg.IsCrossMidnight = 1 THEN ISNULL(md.MachineTotalPunches,0)   ELSE ISNULL(pc.EffectivePunchCount, ISNULL(pc.PunchCount,0)) END      
                  ) > 0        
              AND ISNULL(df.HasEffectivePair,0) = 0        
            THEN        
                CASE        
                    WHEN ISNULL(df.TotalDailyWorkingMinutes,0) >= @FullDayMinutes THEN 'Present'        
                    WHEN ISNULL(df.TotalDailyWorkingMinutes,0) BETWEEN @HalfDayMinutes AND (@FullDayMinutes - 1) THEN 'Half Day Present'        
                    ELSE 'MIS'        
                END        
        
            WHEN fh.IsForcedHalfDay = 1 THEN 'Quarter Day Absent'        
        
            WHEN pc.IsRegularize = 1        
             AND NULLIF(pc.RegularizePunchIn,'00:00:00')  IS NOT NULL        
             AND NULLIF(pc.RegularizePuncOut,'00:00:00') IS NOT NULL        
             AND DATEDIFF(MINUTE, pc.RegularizePunchIn, pc.RegularizePuncOut) >= @FullDayMinutes        
            THEN 'Manual Present'        
        
            /* GF rule: compute status by minutes (labels preserved) */        
            WHEN df.GeoStatus = 'GF' AND ISNULL(df.TotalDailyWorkingMinutes,0) >= @FullDayMinutes THEN 'GF'        
            WHEN df.GeoStatus = 'GF' AND ISNULL(df.TotalDailyWorkingMinutes,0) BETWEEN @HalfDayMinutes AND (@FullDayMinutes - 1) THEN 'Half Day Absent'        
            WHEN df.GeoStatus = 'GF' THEN 'Absent'        
        
            WHEN ISNULL(df.TotalDailyWorkingMinutes,0) >= @FullDayMinutes THEN 'Present'        
            WHEN ISNULL(df.TotalDailyWorkingMinutes,0) BETWEEN @HalfDayMinutes AND (@FullDayMinutes - 1) THEN 'Half Day Absent'        
            WHEN ISNULL(df.TotalDailyWorkingMinutes,0) > 0 AND ISNULL(df.TotalDailyWorkingMinutes,0) < @HalfDayMinutes THEN 'Absent'        
            ELSE 'Absent'        
        END,        
        
        TotalWorkingDays = ISNULL(wda.TotalWorkingDays, 0.0),        
        TotalMonthlyWorkingHours = ISNULL(m.TotalMonthlyWorkingHours, '0 hours and 00 minutes'),        
        
        Location = CASE WHEN df.GeoStatus IN ('GF','MISS') THEN ISNULL(df.GeoAddress,'') ELSE edg.STCode END,        
        PunchSource = df.SourceFlag        
        
    FROM #EDG edg        
    LEFT JOIN #PunchCalc pc   ON pc.EmployeeId=edg.EmployeeId AND pc.PunchDate=edg.PunchDate        
    LEFT JOIN #Eff ef         ON ef.EmployeeId=edg.EmployeeId AND ef.PunchDate=edg.PunchDate        
    LEFT JOIN #GeoDaily gd    ON gd.EmployeeId=edg.EmployeeId AND gd.PunchDate=edg.PunchDate        
    LEFT JOIN #BothDaily bd   ON bd.EmployeeId=edg.EmployeeId AND bd.PunchDate=edg.PunchDate        
    LEFT JOIN #BothPunchCols bc ON bc.EmployeeId = edg.EmployeeId AND bc.WorkDate = edg.PunchDate        
    LEFT JOIN #DwmFinal df    ON df.EmployeeId=edg.EmployeeId AND df.PunchDate=edg.PunchDate        
    LEFT JOIN #Monthly m      ON m.EmployeeId=edg.EmployeeId        
    LEFT JOIN #Carry c        ON c.EmployeeId=edg.EmployeeId AND c.PunchDate=edg.PunchDate        
    LEFT JOIN #LeaveDays ld   ON ld.EmployeeId=edg.EmployeeId AND ld.PunchDate=edg.PunchDate        
    LEFT JOIN #WorkDayAgg wda ON wda.EmployeeId=edg.EmployeeId        
    LEFT JOIN #ForcedHalfDay fh ON fh.EmployeeId=edg.EmployeeId AND fh.PunchDate=edg.PunchDate        
    LEFT JOIN #EffChosen ec        
      ON ec.EmployeeId = edg.EmployeeId        
     AND ec.PunchDate  = edg.PunchDate        
    LEFT JOIN #EDG prev        
      ON prev.EmployeeId = edg.EmployeeId        
     AND prev.PunchDate  = DATEADD(DAY,-1, edg.PunchDate)        
      LEFT JOIN #MachineDaily md   ON md.EmployeeId = edg.EmployeeId  AND md.PunchDate  = edg.PunchDate  LEFT JOIN #MachinePunchCols mpc   ON mpc.EmployeeId = edg.EmployeeId  AND mpc.WorkDate   = edg.PunchDate  
    ORDER BY edg.EmployeeId, edg.PunchDate;        
        
END; 