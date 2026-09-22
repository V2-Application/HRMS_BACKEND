ALTER PROCEDURE dbo.usp_GetMonthlyPunchesRange_Optimized    
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
    
    /*==========================================================    
      1) Employees (filter early)    
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
      2) Dates (fast tally)    
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
      3) Holidays (pre-aggregated)    
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
      4) Leave days (FIX for Msg 130 + faster)    
    ==========================================================*/    
    IF OBJECT_ID('tempdb..#LeaveDays') IS NOT NULL DROP TABLE #LeaveDays;    
    
    SELECT DISTINCT    
        l.EmployeeId,    
        d.PunchDate    
    INTO #LeaveDays    
    FROM dbo.tblLeaveRequest l WITH (NOLOCK)    
    JOIN #Emp e  ON e.EmployeeId = l.EmployeeId    
    JOIN #Dates d ON d.PunchDate BETWEEN l.StartDate AND l.EndDate    
    WHERE l.IsRevoked = 0    
      AND l.StatusId IN (1)    
      AND l.StartDate <= @ToDate    
      AND l.EndDate   >= @FromDate;    
    
    CREATE CLUSTERED INDEX IX_#LeaveDays ON #LeaveDays(EmployeeId, PunchDate);    
    
    /*==========================================================    
      5) Shift history sliced to range    
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
      6) Employee-Date grid (materialized + indexed)    
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
      7) Punch aggregate (one row per Emp/Date)    
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
    WHERE p.PunchDate >= @FromDate AND p.PunchDate <= @ToDate    
    GROUP BY e.EmployeeId, p.UserID, p.PunchDate;    
    
    CREATE CLUSTERED INDEX IX_#PunchAgg ON #PunchAgg(EmployeeId, PunchDate);    
    
    /*==========================================================    
      8) PunchCount / PunchIn / PunchOut computed once (fast)    
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
         THEN 1 ELSE 0 END  
  
, EffectivePunchCount =  
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
        END    
    INTO #PunchCalc    
    FROM #PunchAgg pa    
    CROSS APPLY    
    (    
        SELECT    
            PunchCount = SUM(CASE WHEN v.p IS NOT NULL AND v.p <> '00:00:00' THEN 1 ELSE 0 END),    
            MinPunch   = MIN(CASE WHEN v.p IS NOT NULL AND v.p <> '00:00:00' THEN v.p END),    
            MaxPunch   = MAX(CASE WHEN v.p IS NOT NULL AND v.p <> '00:00:00' THEN v.p END)    
        FROM (VALUES    
            (pa.Punch1),(pa.Punch2),(pa.Punch3),(pa.Punch4),(pa.Punch5),(pa.Punch6),    
            (pa.Punch7),(pa.Punch8),(pa.Punch9),(pa.Punch10),(pa.Punch11),(pa.Punch12)    
        ) v(p)    
    ) calc;    
    
    CREATE CLUSTERED INDEX IX_#PunchCalc ON #PunchCalc(EmployeeId, PunchDate);    
    
    /*==========================================================    
      9) Daily minutes from machine    
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
                    (CASE WHEN NULLIF(pc.Punch1,'00:00:00') IS NOT NULL AND NULLIF(pc.Punch2,'00:00:00') IS NOT NULL AND pc.Punch2 > pc.Punch1 THEN DATEDIFF(MINUTE, pc.Punch1, pc.Punch2) ELSE 0 END) +    
                    (CASE WHEN NULLIF(pc.Punch3,'00:00:00') IS NOT NULL AND NULLIF(pc.Punch4,'00:00:00') IS NOT NULL AND pc.Punch4 > pc.Punch3 THEN DATEDIFF(MINUTE, pc.Punch3, pc.Punch4) ELSE 0 END) +    
                    (CASE WHEN NULLIF(pc.Punch5,'00:00:00') IS NOT NULL AND NULLIF(pc.Punch6,'00:00:00') IS NOT NULL AND pc.Punch6 > pc.Punch5 THEN DATEDIFF(MINUTE, pc.Punch5, pc.Punch6) ELSE 0 END) +    
                    (CASE WHEN NULLIF(pc.Punch7,'00:00:00') IS NOT NULL AND NULLIF(pc.Punch8,'00:00:00') IS NOT NULL AND pc.Punch8 > pc.Punch7 THEN DATEDIFF(MINUTE, pc.Punch7, pc.Punch8) ELSE 0 END) +    
                    (CASE WHEN NULLIF(pc.Punch9,'00:00:00') IS NOT NULL AND NULLIF(pc.Punch10,'00:00:00') IS NOT NULL AND pc.Punch10 > pc.Punch9 THEN DATEDIFF(MINUTE, pc.Punch9, pc.Punch10) ELSE 0 END) +    
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
                    (NULLIF(pc.Punch1,'00:00:00') IS NOT NULL AND NULLIF(pc.Punch2,'00:00:00') IS NOT NULL AND pc.Punch2 > pc.Punch1) OR    
                    (NULLIF(pc.Punch3,'00:00:00') IS NOT NULL AND NULLIF(pc.Punch4,'00:00:00') IS NOT NULL AND pc.Punch4 > pc.Punch3) OR    
            (NULLIF(pc.Punch5,'00:00:00') IS NOT NULL AND NULLIF(pc.Punch6,'00:00:00') IS NOT NULL AND pc.Punch6 > pc.Punch5) OR    
                    (NULLIF(pc.Punch7,'00:00:00') IS NOT NULL AND NULLIF(pc.Punch8,'00:00:00') IS NOT NULL AND pc.Punch8 > pc.Punch7) OR    
                    (NULLIF(pc.Punch9,'00:00:00') IS NOT NULL AND NULLIF(pc.Punch10,'00:00:00') IS NOT NULL AND pc.Punch10 > pc.Punch9) OR    
                    (NULLIF(pc.Punch11,'00:00:00') IS NOT NULL AND NULLIF(pc.Punch12,'00:00:00') IS NOT NULL AND pc.Punch12 > pc.Punch11)    
                THEN 1 ELSE 0    
            END    
    INTO #DwmMachine    
    FROM #PunchCalc pc;    
    
    CREATE CLUSTERED INDEX IX_#DwmMachine ON #DwmMachine(EmployeeId, PunchDate);    
    
    /*==========================================================    
      10) Geofence approved punches (SARGABLE filter)    
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
      AND ar.PunchTimeUtc <  DATEADD(DAY, 1, @ToDate);    
    
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
            a.EmployeeId, a.PunchDate,    
            InTime  = a.PunchTimeUtc,    
            OutTime = b.PunchTimeUtc,    
            WorkedMinutes = DATEDIFF(MINUTE, a.PunchTimeUtc, b.PunchTimeUtc),    
            a.Address    
        FROM #GeoApproved a    
        JOIN #GeoApproved b    
          ON b.EmployeeId = a.EmployeeId    
         AND b.PunchDate  = a.PunchDate    
         AND b.rn = a.rn + 1    
        WHERE a.rn % 2 = 1    
    )  
 --,    
 --   PunchCounts AS    
 --   (    
 --       SELECT EmployeeId, PunchDate, TotalPunches = COUNT(*)    
 --       FROM #GeoApproved    
 --       GROUP BY EmployeeId, PunchDate    
 --   )   
    SELECT    
        p.EmployeeId,    
        p.PunchDate,    
        GeoFirstInUtc = MIN(p.InTime),    
        GeoLastOutUtc = MAX(p.OutTime),    
        GeoWorkedMinutes = SUM(p.WorkedMinutes),    
        GeoTotalPunches = pc.TotalPunches,    
        GeoStatus = CASE WHEN pc.TotalPunches % 2 <> 0 THEN 'MP' ELSE 'GF' END,    
        Address = MIN(p.Address)    
    INTO #GeoDaily    
    FROM Paired p    
    JOIN PunchCounts pc    
      ON pc.EmployeeId = p.EmployeeId AND pc.PunchDate = p.PunchDate    
    GROUP BY p.EmployeeId, p.PunchDate, pc.TotalPunches;    
    
    CREATE CLUSTERED INDEX IX_#GeoDaily ON #GeoDaily(EmployeeId, PunchDate);    
    
    /*==========================================================    
      11) Effective In/Out (lightweight; extend if needed)    
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
            CASE WHEN pc.PunchIn IS NULL OR pc.PunchIn='00:00:00' THEN NULL    
                 ELSE DATEADD(SECOND, DATEDIFF(SECOND,'00:00:00', CAST(pc.PunchIn AS TIME)), CAST(edg.PunchDate AS DATETIME))    
            END,    
    
        EffectiveOutDT =    
            CASE    
                WHEN edg.IsCrossMidnight = 1 THEN    
                    CASE    
                        WHEN pcN.PunchIn IS NOT NULL AND pcN.PunchIn <> '00:00:00'    
                             AND CAST(pcN.PunchIn AS TIME) <= CAST('14:00:00' AS TIME)    
                        THEN DATEADD(SECOND, DATEDIFF(SECOND,'00:00:00', CAST(pcN.PunchIn AS TIME)),    
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
    
        EffectivePunchInTime  = CAST(NULLIF(pc.PunchIn,'00:00:00')  AS TIME(0)),    
        EffectivePunchOutTime = CAST(NULLIF(pc.PunchOut,'00:00:00') AS TIME(0))    
    INTO #Eff    
    FROM #EDG edg    
    LEFT JOIN #PunchCalc pc    
      ON pc.EmployeeId = edg.EmployeeId AND pc.PunchDate = edg.PunchDate    
    LEFT JOIN #PunchCalc pcN    
      ON pcN.EmployeeId = edg.EmployeeId AND pcN.PunchDate = DATEADD(DAY,1,edg.PunchDate);    
    
    CREATE CLUSTERED INDEX IX_#Eff ON #Eff(EmployeeId, PunchDate);    
    
    /*==========================================================    
      12) Final Daily Minutes preference: Geo(GF) > Machine > EffectiveDT    
    ==========================================================*/    
    IF OBJECT_ID('tempdb..#DwmFinal') IS NOT NULL DROP TABLE #DwmFinal;    
    
    SELECT    
        edg.EmployeeId,    
        edg.PunchDate,    
        TotalDailyWorkingMinutes =    
            CASE    
                WHEN gd.GeoStatus = 'GF' AND gd.GeoWorkedMinutes IS NOT NULL THEN gd.GeoWorkedMinutes    
                WHEN dm.TotalDailyWorkingMinutes IS NOT NULL THEN dm.TotalDailyWorkingMinutes    
                WHEN ef.EffectiveInDT IS NOT NULL AND ef.EffectiveOutDT IS NOT NULL    
                     AND DATEDIFF(MINUTE, ef.EffectiveInDT, ef.EffectiveOutDT) > 0    
                THEN DATEDIFF(MINUTE, ef.EffectiveInDT, ef.EffectiveOutDT)    
                ELSE 0    
            END,    
        HasEffectivePair =    
  CASE    
                WHEN gd.GeoStatus = 'GF' AND gd.GeoWorkedMinutes IS NOT NULL THEN 1    
                WHEN dm.HasEffectivePair = 1 THEN 1    
                WHEN ef.EffectiveInDT IS NOT NULL AND ef.EffectiveOutDT IS NOT NULL    
                     AND DATEDIFF(MINUTE, ef.EffectiveInDT, ef.EffectiveOutDT) > 0    
                THEN 1 ELSE 0    
            END,    
        GeoStatus = ISNULL(gd.GeoStatus,''),    
        GeoAddress = gd.Address    
    INTO #DwmFinal    
    FROM #EDG edg    
    LEFT JOIN #GeoDaily   gd ON gd.EmployeeId = edg.EmployeeId AND gd.PunchDate = edg.PunchDate    
    LEFT JOIN #DwmMachine dm ON dm.EmployeeId = edg.EmployeeId AND dm.PunchDate = edg.PunchDate    
    LEFT JOIN #Eff        ef ON ef.EmployeeId = edg.EmployeeId AND ef.PunchDate = edg.PunchDate;    
    
    CREATE CLUSTERED INDEX IX_#DwmFinal ON #DwmFinal(EmployeeId, PunchDate);    
    
    /*==========================================================    
      13) Monthly hours    
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
      14) Carry-over rule (night shift next day single punch)    
    ==========================================================*/    
    IF OBJECT_ID('tempdb..#Carry') IS NOT NULL DROP TABLE #Carry;    
    
    SELECT    
        edg.EmployeeId,    
        edg.PunchDate,    
        IsCarryOver =    
            CASE    
                WHEN prev.IsCrossMidnight = 1    
                 AND prev.EffectiveOutDT IS NOT NULL    
                 AND ISNULL(pc.PunchCount,0) = 1    
                 AND CAST(ISNULL(pc.PunchIn,'00:00:00') AS TIME) <= CAST('14:00:00' AS TIME)    
                 AND ISNULL(df.GeoStatus,'') <> 'GF'    
                THEN 1 ELSE 0    
            END    
    INTO #Carry    
    FROM #EDG edg    
    LEFT JOIN #Eff prev    
      ON prev.EmployeeId = edg.EmployeeId AND prev.PunchDate = DATEADD(DAY,-1, edg.PunchDate)    
    LEFT JOIN #PunchCalc pc    
      ON pc.EmployeeId = edg.EmployeeId AND pc.PunchDate = edg.PunchDate    
    LEFT JOIN #DwmFinal df    
      ON df.EmployeeId = edg.EmployeeId AND df.PunchDate = edg.PunchDate;    
    
    CREATE CLUSTERED INDEX IX_#Carry ON #Carry(EmployeeId, PunchDate);    
    
    /*==========================================================    
      15) WorkDayValue per day + total per employee (FIX for Msg130)    
    ==========================================================*/    
    IF OBJECT_ID('tempdb..#WorkDay') IS NOT NULL DROP TABLE #WorkDay;    
    
    SELECT    
        edg.EmployeeId,    
        edg.PunchDate,    
        WorkDayValue =    
            CASE    
                WHEN edg.IsFutureDate = 1 THEN 0.0    
                WHEN edg.IsHoliday   = 1 THEN 0.0    
                WHEN ld.EmployeeId IS NOT NULL THEN 0.0    
                WHEN edg.IsWeekend = 1 AND ISNULL(df.TotalDailyWorkingMinutes,0) = 0 THEN 0.0    
                WHEN ISNULL(df.TotalDailyWorkingMinutes,0) >= 510 THEN 1.0    
                WHEN ISNULL(df.TotalDailyWorkingMinutes,0) >= 240 THEN 0.5    
                ELSE 0.0    
            END    
    INTO #WorkDay    
    FROM #EDG edg    
    LEFT JOIN #LeaveDays ld    
      ON ld.EmployeeId = edg.EmployeeId AND ld.PunchDate = edg.PunchDate    
    LEFT JOIN #DwmFinal df    
      ON df.EmployeeId = edg.EmployeeId AND df.PunchDate = edg.PunchDate;    
    
    CREATE CLUSTERED INDEX IX_#WorkDay ON #WorkDay(EmployeeId, PunchDate);    
    
    IF OBJECT_ID('tempdb..#WorkDayAgg') IS NOT NULL DROP TABLE #WorkDayAgg;    
    
    SELECT EmployeeId, TotalWorkingDays = SUM(WorkDayValue)    
    INTO #WorkDayAgg    
    FROM #WorkDay    
    GROUP BY EmployeeId;    
    
    CREATE UNIQUE CLUSTERED INDEX IX_#WorkDayAgg ON #WorkDayAgg(EmployeeId);    
    
    /*==========================================================    
      16) FINAL OUTPUT    
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
    
        PunchIn  = CASE    
                    WHEN c.IsCarryOver=1 THEN CAST('00:00:00' AS TIME(0))    
                    WHEN df.GeoStatus='GF' AND gd.GeoFirstInUtc IS NOT NULL THEN CAST(gd.GeoFirstInUtc AS TIME(0))    
                    ELSE ISNULL(ef.EffectivePunchInTime, CAST('00:00:00' AS TIME(0)))    
                  END,    
    
        PunchOut = CASE    
                    WHEN c.IsCarryOver=1 THEN CAST('00:00:00' AS TIME(0))    
                    WHEN df.GeoStatus='GF' AND gd.GeoLastOutUtc IS NOT NULL THEN CAST(gd.GeoLastOutUtc AS TIME(0))    
                    ELSE ISNULL(ef.EffectivePunchOutTime, CAST('00:00:00' AS TIME(0)))    
                  END,    
    
        Punch1  = CASE WHEN c.IsCarryOver=1 THEN '00:00:00' ELSE ISNULL(pc.Punch1,'00:00:00') END,    
        Punch2  = CASE WHEN c.IsCarryOver=1 THEN '00:00:00' ELSE ISNULL(pc.Punch2,'00:00:00') END,    
        Punch3  = CASE WHEN c.IsCarryOver=1 THEN '00:00:00' ELSE ISNULL(pc.Punch3,'00:00:00') END,    
        Punch4  = CASE WHEN c.IsCarryOver=1 THEN '00:00:00' ELSE ISNULL(pc.Punch4,'00:00:00') END,    
        Punch5  = CASE WHEN c.IsCarryOver=1 THEN '00:00:00' ELSE ISNULL(pc.Punch5,'00:00:00') END,    
        Punch6  = CASE WHEN c.IsCarryOver=1 THEN '00:00:00' ELSE ISNULL(pc.Punch6,'00:00:00') END,    
        Punch7  = CASE WHEN c.IsCarryOver=1 THEN '00:00:00' ELSE ISNULL(pc.Punch7,'00:00:00') END,    
        Punch8  = CASE WHEN c.IsCarryOver=1 THEN '00:00:00' ELSE ISNULL(pc.Punch8,'00:00:00') END,    
        Punch9  = CASE WHEN c.IsCarryOver=1 THEN '00:00:00' ELSE ISNULL(pc.Punch9,'00:00:00') END,    
        Punch10 = CASE WHEN c.IsCarryOver=1 THEN '00:00:00' ELSE ISNULL(pc.Punch10,'00:00:00') END,    
        Punch11 = CASE WHEN c.IsCarryOver=1 THEN '00:00:00' ELSE ISNULL(pc.Punch11,'00:00:00') END,    
        Punch12 = CASE WHEN c.IsCarryOver=1 THEN '00:00:00' ELSE ISNULL(pc.Punch12,'00:00:00') END,    
    
        --ValidPunchCount = CASE WHEN c.IsCarryOver=1 THEN 0 ELSE ISNULL(pc.PunchCount,0) + ISNULL(gd.GeoTotalPunches,0) END,  
  ValidPunchCount =  
    CASE  
        WHEN c.IsCarryOver=1 THEN 0  
        WHEN df.GeoStatus='GF' THEN ISNULL(gd.GeoTotalPunches,0)  
        ELSE ISNULL(pc.EffectivePunchCount,0) + ISNULL(gd.GeoTotalPunches,0)  
    END,  
    
        RegularizePunchIn = CASE WHEN c.IsCarryOver=1 THEN '00:00:00' ELSE ISNULL(pc.RegularizePunchIn,'00:00:00') END,    
        RegularizePuncOut = CASE WHEN c.IsCarryOver=1 THEN '00:00:00' ELSE ISNULL(pc.RegularizePuncOut,'00:00:00') END,    
        IsRegularize      = CASE WHEN c.IsCarryOver=1 THEN 0 ELSE ISNULL(pc.IsRegularize,0) END,    
    
        IsOnLeave = CASE WHEN ld.EmployeeId IS NOT NULL THEN 1 ELSE 0 END,    
    
        TotalWorkingMinutes =    
            CASE WHEN c.IsCarryOver=1 THEN CAST('00:00' AS NVARCHAR(10))    
                 ELSE RIGHT('0' + CAST(df.TotalDailyWorkingMinutes / 60 AS VARCHAR(2)),2)    
                      + ':' +    
                      RIGHT('0' + CAST(df.TotalDailyWorkingMinutes % 60 AS VARCHAR(2)),2)    
            END,    
    
        LateMinutes =    
            CASE    
                WHEN c.IsCarryOver=1 OR edg.IsFutureDate=1 OR edg.IsHoliday=1 THEN 0    
                WHEN ef.EffectiveInDT IS NULL OR ef.ShiftStartDT IS NULL THEN 0    
                WHEN ef.EffectiveInDT > ef.ShiftStartDT THEN DATEDIFF(MINUTE, ef.ShiftStartDT, ef.EffectiveInDT)    
                ELSE 0    
            END,    
    
        EarlyMinutes =    
            CASE    
                WHEN c.IsCarryOver=1 OR edg.IsFutureDate=1 OR edg.IsHoliday=1 THEN 0    
                WHEN ef.EffectiveInDT IS NULL OR ef.ShiftStartDT IS NULL THEN 0    
                WHEN ef.EffectiveInDT < ef.ShiftStartDT THEN DATEDIFF(MINUTE, ef.EffectiveInDT, ef.ShiftStartDT)    
                ELSE 0    
            END,    
    
        Status =    
            CASE    
                WHEN edg.IsFutureDate = 1 THEN ''    
                WHEN edg.IsHoliday = 1 THEN 'Holiday'    
                WHEN ld.EmployeeId IS NOT NULL THEN 'On Leave'    
                WHEN c.IsCarryOver = 1 THEN ''    
                WHEN edg.IsWeekend = 1 AND ISNULL(df.TotalDailyWorkingMinutes,0) = 0 THEN 'Weekly Off'    
       -- ✅ Your requirement:  
        WHEN df.GeoStatus = 'MP' THEN 'MIS'  
        WHEN df.GeoStatus = 'GF' THEN 'GF'  
   WHEN (ISNULL(pc.EffectivePunchCount,0) + ISNULL(gd.GeoTotalPunches,0)) % 2 = 1  
             AND (ISNULL(pc.EffectivePunchCount,0) + ISNULL(gd.GeoTotalPunches,0)) > 0  
             AND ISNULL(pc.HasRegularizePair,0) = 0  
        THEN 'MIS'  
--    WHEN  
--(  
--    CASE  
--        WHEN df.GeoStatus='GF' THEN ISNULL(gd.GeoTotalPunches,0)  
--        ELSE ISNULL(pc.EffectivePunchCount,0) + ISNULL(gd.GeoTotalPunches,0)  
--    END  
--) % 2 = 1  
--AND  
--(  
--    CASE  
--        WHEN df.GeoStatus='GF' THEN ISNULL(gd.GeoTotalPunches,0)  
--        ELSE ISNULL(pc.EffectivePunchCount,0) + ISNULL(gd.GeoTotalPunches,0)  
--    END  
--) > 0  
--AND ISNULL(pc.HasRegularizePair,0) = 0   -- key line: if regularized pair exists, never mispunch  
--THEN 'Mispunch'  
                --WHEN (ISNULL(pc.PunchCount,0) + ISNULL(gd.GeoTotalPunches,0)) % 2 = 1    
                --     AND (ISNULL(pc.PunchCount,0) + ISNULL(gd.GeoTotalPunches,0)) > 0    
                --     AND NOT (edg.IsCrossMidnight=1 AND df.HasEffectivePair=1)    
                --THEN 'Mispunch'    
                WHEN ISNULL(df.TotalDailyWorkingMinutes,0) >= 510 THEN 'Present'    
                WHEN ISNULL(df.TotalDailyWorkingMinutes,0) BETWEEN 240 AND 509 THEN 'Half Day Absent'    
                WHEN ISNULL(df.TotalDailyWorkingMinutes,0) > 0 AND ISNULL(df.TotalDailyWorkingMinutes,0) < 240 THEN 'Absent'    
                ELSE 'Absent'    
            END,    
    
        TotalWorkingDays = ISNULL(wda.TotalWorkingDays, 0.0),    
        TotalMonthlyWorkingHours = ISNULL(m.TotalMonthlyWorkingHours, '0 hours and 00 minutes'),    
        Location = CASE WHEN df.GeoStatus='GF' THEN ISNULL(df.GeoAddress,'') ELSE edg.STCode END    
    
    FROM #EDG edg    
    LEFT JOIN #PunchCalc pc  ON pc.EmployeeId=edg.EmployeeId AND pc.PunchDate=edg.PunchDate    
    LEFT JOIN #Eff ef        ON ef.EmployeeId=edg.EmployeeId AND ef.PunchDate=edg.PunchDate    
    LEFT JOIN #GeoDaily gd   ON gd.EmployeeId=edg.EmployeeId AND gd.PunchDate=edg.PunchDate    
    LEFT JOIN #DwmFinal df   ON df.EmployeeId=edg.EmployeeId AND df.PunchDate=edg.PunchDate    
    LEFT JOIN #Monthly m     ON m.EmployeeId=edg.EmployeeId    
    LEFT JOIN #Carry c       ON c.EmployeeId=edg.EmployeeId AND c.PunchDate=edg.PunchDate    
    LEFT JOIN #LeaveDays ld  ON ld.EmployeeId=edg.EmployeeId AND ld.PunchDate=edg.PunchDate    
    LEFT JOIN #WorkDayAgg wda ON wda.EmployeeId=edg.EmployeeId    
    ORDER BY edg.EmployeeId, edg.PunchDate;    
    
END; 