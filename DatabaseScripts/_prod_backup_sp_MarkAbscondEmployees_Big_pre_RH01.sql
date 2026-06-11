-- PROD backup of dbo.sp_MarkAbscondEmployees_Big taken before RH01=7/others=6 rule deploy
CREATE  PROCEDURE  dbo.sp_MarkAbscondEmployees_Big  
    @BatchSize  INT = 8000,     -- tune: 400–1200  
    @WindowDays INT = 3        -- how many trailing days to check  
AS  
BEGIN  
    SET NOCOUNT ON;  
    SET XACT_ABORT ON;  
    -- This proc benefits if DB has READ_COMMITTED_SNAPSHOT = ON (ask DBA)  
  
    DECLARE @Now       DATETIME = GETDATE();  
    DECLARE @ToDate    DATE     = CAST(@Now AS DATE);  
    DECLARE @FromDate  DATE     = DATEADD(DAY, -@WindowDays, @ToDate);  
    DECLARE @Remarks   NVARCHAR(255) = N'Abscond by system: not seen for ' + CAST(@WindowDays AS NVARCHAR(12)) + N' day(s)';  
    DECLARE @By        NVARCHAR(50)  = N'System';  
  
    /* 0) Active employees */  
    IF OBJECT_ID('tempdb..#ActiveEmp') IS NOT NULL DROP TABLE #ActiveEmp;  
    CREATE TABLE #ActiveEmp(  
        EmployeeId   BIGINT       NOT NULL PRIMARY KEY,  
        ECode        NVARCHAR(50) NOT NULL,  
        EmployeeName NVARCHAR(255) NULL,  
        LocationId   INT NULL,  
        Processed    BIT NOT NULL DEFAULT(0)  
    );  
  
    INSERT INTO #ActiveEmp(EmployeeId,ECode,EmployeeName,LocationId)  
    SELECT e.EmployeeId, e.ECode, e.[FULL NAME], e.LocationId  
    FROM dbo.tblEmployee e WITH (READPAST)  
    WHERE e.IsActive = 1  
      AND e.ECode IS NOT NULL;  
  
    /* 1) Very fast “seen” pre-filter using raw sources only */  
    IF OBJECT_ID('tempdb..#SeenFast') IS NOT NULL DROP TABLE #SeenFast;  
    CREATE TABLE #SeenFast(EmployeeId BIGINT NOT NULL PRIMARY KEY);  
  
    -- Any multi-punch within window?  
    INSERT INTO #SeenFast(EmployeeId)  
    SELECT DISTINCT e.EmployeeId  
    FROM #ActiveEmp e  
    JOIN dbo.tblEmployeeMultiPunches p WITH (READPAST)  
      ON p.UserID = e.ECode  
     AND p.PunchDate BETWEEN @FromDate AND @ToDate;  
  
    -- OR any approved geofence punch within window?  
    INSERT INTO #SeenFast(EmployeeId)  
    SELECT DISTINCT e.EmployeeId  
    FROM #ActiveEmp e  
    JOIN dbo.AttendanceRecord ar WITH (READPAST)  
      ON ar.EmployeeId = e.EmployeeId  
     AND ar.StatusId = 1  
     AND CONVERT(date, ar.PunchTimeUtc) BETWEEN @FromDate AND @ToDate  
    WHERE e.EmployeeId NOT IN (SELECT EmployeeId FROM #SeenFast);  
  
    /* 2) Precompute leave overlaps once */  
    IF OBJECT_ID('tempdb..#LeaveEmp') IS NOT NULL DROP TABLE #LeaveEmp;  
    CREATE TABLE #LeaveEmp(EmployeeId BIGINT NOT NULL PRIMARY KEY);  
  
    INSERT INTO #LeaveEmp(EmployeeId)  
    SELECT DISTINCT l.EmployeeId  
    FROM dbo.tblLeaveRequest l WITH (READPAST)  
    WHERE l.StatusId IN (1,2)      -- pending/approved  
      AND l.IsRevoked = 0  
      AND l.StartDate <= @ToDate  
      AND l.EndDate   >= @FromDate;  
  
    /* 3) Staging for final output */  
    IF OBJECT_ID('tempdb..#AbscondedOut') IS NOT NULL DROP TABLE #AbscondedOut;  
    CREATE TABLE #AbscondedOut(  
        EmployeeId   BIGINT       NOT NULL PRIMARY KEY,  
        ECode        NVARCHAR(50) NOT NULL,  
        EmployeeName NVARCHAR(255) NULL,  
        AbscondedOn  DATETIME     NOT NULL,  
        Remarks      NVARCHAR(255) NOT NULL  
    );  
  
    /* 4) Process only not-seen-fast employees, in batches */  
    WHILE EXISTS (  
        SELECT 1 FROM #ActiveEmp a  
        WHERE a.Processed = 0  
          AND a.EmployeeId NOT IN (SELECT EmployeeId FROM #SeenFast)  
    )  
    BEGIN  
        IF OBJECT_ID('tempdb..#Batch') IS NOT NULL DROP TABLE #Batch;  
        CREATE TABLE #Batch(  
            EmployeeId   BIGINT       NOT NULL PRIMARY KEY,  
            ECode        NVARCHAR(50) NOT NULL,  
            EmployeeName NVARCHAR(255) NULL,  
            LocationId   INT NULL  
        );  
  
        INSERT INTO #Batch(EmployeeId,ECode,EmployeeName,LocationId)  
        SELECT TOP(@BatchSize) a.EmployeeId, a.ECode, a.EmployeeName, a.LocationId  
        FROM #ActiveEmp a  
        WHERE a.Processed = 0  
          AND a.EmployeeId NOT IN (SELECT EmployeeId FROM #SeenFast)  
        ORDER BY a.EmployeeId;  
  
        UPDATE a SET Processed = 1  
        FROM #ActiveEmp a JOIN #Batch b ON b.EmployeeId = a.EmployeeId;  
  
        /* 4.a) Pull detailed facts from your TVF ONLY for this batch */  
        IF OBJECT_ID('tempdb..#Facts') IS NOT NULL DROP TABLE #Facts;  
        CREATE TABLE #Facts(  
            EmployeeId   BIGINT       NOT NULL,  
            ECode        NVARCHAR(50) NOT NULL,  
            PunchDate    DATE         NOT NULL,  
            StatusLabel  NVARCHAR(50) NULL,  
            IsHoliday    BIT          NOT NULL,  
            IsRegularize BIT          NOT NULL,  
            ValidPunches INT          NOT NULL,  
            PRIMARY KEY (EmployeeId, PunchDate)  
        );  
  
        INSERT INTO #Facts(EmployeeId,ECode,PunchDate,StatusLabel,IsHoliday,IsRegularize,ValidPunches)  
        SELECT  
            f.EmployeeId,  
            f.ECode,  
            f.AttendanceDate,  
            f.Status,  
            CAST(f.IsHoliday AS BIT),  
            CAST(f.IsRegularize AS BIT),  
            ISNULL(f.ValidPunchCount, 0)  
        FROM #Batch b  
        CROSS APPLY dbo.fn_GetMonthlyPunchesRange_productionnewnick_live(@FromDate, @ToDate, b.ECode) AS f  
        OPTION (RECOMPILE);  
  
        /* 4.b) Build exclusions (Holiday/WeeklyOff/Regularize/Leave) */  
        IF OBJECT_ID('tempdb..#Excl') IS NOT NULL DROP TABLE #Excl;  
        CREATE TABLE #Excl(EmployeeId BIGINT NOT NULL PRIMARY KEY);  
  
        INSERT INTO #Excl(EmployeeId)  
        SELECT DISTINCT EmployeeId  
        FROM #Facts  
        WHERE IsHoliday = 1 OR StatusLabel = N'Weekly Off';  
  
        INSERT INTO #Excl(EmployeeId)  
        SELECT DISTINCT EmployeeId  
        FROM #Facts  
        WHERE IsRegularize = 1  
          AND EmployeeId NOT IN (SELECT EmployeeId FROM #Excl);  
  
        INSERT INTO #Excl(EmployeeId)  
        SELECT le.EmployeeId  
        FROM #LeaveEmp le  
        JOIN #Batch b ON b.EmployeeId = le.EmployeeId  
        WHERE le.EmployeeId NOT IN (SELECT EmployeeId FROM #Excl);  
  
        /* 4.c) Seen according to detailed facts (covers “Mispunch”, GF, etc.) */  
        IF OBJECT_ID('tempdb..#SeenDetail') IS NOT NULL DROP TABLE #SeenDetail;  
        CREATE TABLE #SeenDetail(EmployeeId BIGINT NOT NULL PRIMARY KEY);  
  
        INSERT INTO #SeenDetail(EmployeeId)  
        SELECT DISTINCT EmployeeId  
        FROM #Facts  
        WHERE StatusLabel IN (N'Present', N'Manual Present', N'Manual Present Half Day', N'Half Day Absent', N'Mispunch', N'GF')  
           OR ValidPunches > 0;  
  
        /* 4.d) Final to-abscond in this batch (not excluded, not seen at all, has all window days) */  
        IF OBJECT_ID('tempdb..#ToAbscond') IS NOT NULL DROP TABLE #ToAbscond;  
        CREATE TABLE #ToAbscond(  
            EmployeeId   BIGINT       NOT NULL PRIMARY KEY,  
            ECode        NVARCHAR(50) NOT NULL,  
            EmployeeName NVARCHAR(255) NULL  
        );  
  
        INSERT INTO #ToAbscond(EmployeeId,ECode,EmployeeName)  
        SELECT b.EmployeeId, b.ECode, b.EmployeeName  
        FROM #Batch b  
        WHERE b.EmployeeId NOT IN (SELECT EmployeeId FROM #Excl)  
          AND b.EmployeeId NOT IN (SELECT EmployeeId FROM #SeenDetail)  
          AND @WindowDays = (SELECT COUNT(DISTINCT f.PunchDate) FROM #Facts f WHERE f.EmployeeId = b.EmployeeId);  
  
        IF EXISTS (SELECT 1 FROM #ToAbscond)  
        BEGIN  
            BEGIN TRY  
                BEGIN TRAN;  
  
                /* Update existing abscond separations */  
                UPDATE es WITH (ROWLOCK, UPDLOCK)  
                SET es.AbscondRemarks       = @Remarks,  
                    es.AbscondedBy          = @By,  
                    es.IsApprovedByManager  = 0,  
                    es.ManagerRemarks       = NULL,  
                    es.IsApprovedByHR       = 0,  
                    es.HRRemarks            = NULL,  
                    es.ResignationDate      = @Now  
                FROM dbo.tblEmployeeSepration es  
                JOIN #ToAbscond t ON t.EmployeeId = es.EmployeeId  
                WHERE es.ResignationTypeId = 10;  
  
                /* Insert missing abscond separations */  
                INSERT INTO dbo.tblEmployeeSepration  
                    (EmployeeId, ResignationTypeId, AbscondRemarks, AbscondedBy, ResignationDate,  
                     IsApprovedByManager, ManagerRemarks, IsApprovedByHR, HRRemarks)  
                SELECT t.EmployeeId, 10, @Remarks, @By, @Now, 0, NULL, 0, NULL  
                FROM #ToAbscond t  
                WHERE NOT EXISTS (  
                    SELECT 1 FROM dbo.tblEmployeeSepration es WITH (READPAST)  
                    WHERE es.EmployeeId = t.EmployeeId AND es.ResignationTypeId = 10  
                );  
  
                /* Deactivate employees */  
                UPDATE e WITH (ROWLOCK, UPDLOCK)  
                SET e.IsActive = 0  
                FROM dbo.tblEmployee e  
                JOIN #ToAbscond t ON t.EmployeeId = e.EmployeeId  
                WHERE e.IsActive <> 0;  
  
                /* History (dedupe per day) */  
                INSERT INTO dbo.AbscondHistory (EmployeeId, ECode, EmployeeName, AbscondDate, Remarks, CreatedBy)  
                SELECT t.EmployeeId, t.ECode, t.EmployeeName, @Now, @Remarks, @By  
                FROM #ToAbscond t  
                WHERE NOT EXISTS (  
                    SELECT 1 FROM dbo.AbscondHistory ah WITH (READPAST)  
                    WHERE ah.EmployeeId = t.EmployeeId  
                      AND CAST(ah.AbscondDate AS DATE) = @ToDate  
                );  
  
                COMMIT TRAN;  
            END TRY  
            BEGIN CATCH  
                IF XACT_STATE() <> 0 ROLLBACK TRAN;  
                -- swallow batch error to proceed; optionally log somewhere  
            END CATCH;  
  
            /* accumulate output */  
            INSERT INTO #AbscondedOut(EmployeeId,ECode,EmployeeName,AbscondedOn,Remarks)  
            SELECT t.EmployeeId, t.ECode, t.EmployeeName, @Now, @Remarks  
            FROM #ToAbscond t  
            WHERE NOT EXISTS (SELECT 1 FROM #AbscondedOut o WHERE o.EmployeeId = t.EmployeeId);  
        END  
    END  
  
    /* 5) Final output */  
    SELECT EmployeeId, ECode, EmployeeName, AbscondedOn, Remarks  
    FROM #AbscondedOut  
    ORDER BY EmployeeName;  
  
    SET NOCOUNT OFF;  
END;  
  
  
  
  
