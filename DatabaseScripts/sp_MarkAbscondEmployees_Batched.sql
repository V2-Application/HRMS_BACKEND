
CREATE PROCEDURE [dbo].[sp_MarkAbscondEmployees_Batched]
    @BatchSize int = 500  -- tune as needed: 200-1000 are typical sweet spots
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    -- If your DB uses READ_COMMITTED_SNAPSHOT, this reduces shared locks on reads.
    -- Otherwise we still keep transactions short to avoid blocking.

    DECLARE @Now           DATETIME = GETDATE();
    DECLARE @FromDate      DATE     = DATEADD(DAY, -5, CAST(@Now AS DATE));
    DECLARE @ToDate        DATE     = CAST(@Now AS DATE);
    DECLARE @AbscondRemarks NVARCHAR(255) = N'Abscond by system: not seen for 5 days';
    DECLARE @AbscondedBy    NVARCHAR(50)  = N'System';

    ----------------------------------------------------------------------
    -- 0) Staging: active employees, and precompute leave overlaps once
    ----------------------------------------------------------------------
    IF OBJECT_ID('tempdb..#ActiveEmp') IS NOT NULL DROP TABLE #ActiveEmp;
    CREATE TABLE #ActiveEmp (
        EmployeeId   BIGINT       NOT NULL PRIMARY KEY,
        ECode        NVARCHAR(50) NOT NULL,
        EmployeeName NVARCHAR(255) NULL,
        LocationId   INT NULL,
        Processed    BIT NOT NULL DEFAULT(0)
    );

    INSERT INTO #ActiveEmp (EmployeeId, ECode, EmployeeName, LocationId)
    SELECT e.EmployeeId, e.ECode, e.[FULL NAME], e.LocationId
    FROM dbo.tblEmployee e WITH (READPAST)         -- skip locked rows if any
    WHERE e.IsActive = 1
      AND e.ECode IS NOT NULL;

    -- Precompute leave overlaps for the 3-day window
    IF OBJECT_ID('tempdb..#LeaveEmp') IS NOT NULL DROP TABLE #LeaveEmp;
    CREATE TABLE #LeaveEmp (
        EmployeeId BIGINT NOT NULL PRIMARY KEY
    );

    INSERT INTO #LeaveEmp(EmployeeId)
    SELECT DISTINCT l.EmployeeId
    FROM dbo.tblLeaveRequest l WITH (READPAST)
    WHERE l.StatusId IN (1,2)  -- Pending / Approved
      AND l.IsRevoked = 0
      AND l.StartDate <= @ToDate
      AND l.EndDate   >= @FromDate;

    -- Accumulator for final output across all batches
    IF OBJECT_ID('tempdb..#AbscondedOut') IS NOT NULL DROP TABLE #AbscondedOut;
    CREATE TABLE #AbscondedOut (
        EmployeeId   BIGINT       NOT NULL,
        ECode        NVARCHAR(50) NOT NULL,
        EmployeeName NVARCHAR(255) NULL,
        AbscondedOn  DATETIME     NOT NULL,
        Remarks      NVARCHAR(255) NOT NULL,
        PRIMARY KEY (EmployeeId)
    );

    ----------------------------------------------------------------------
    -- 1) Batch loop
    ----------------------------------------------------------------------
    WHILE EXISTS (SELECT 1 FROM #ActiveEmp WHERE Processed = 0)
    BEGIN
        -- Pick a batch deterministically to reduce deadlock cycles
        IF OBJECT_ID('tempdb..#Batch') IS NOT NULL DROP TABLE #Batch;
        CREATE TABLE #Batch (
            EmployeeId   BIGINT       NOT NULL PRIMARY KEY,
            ECode        NVARCHAR(50) NOT NULL,
            EmployeeName NVARCHAR(255) NULL,
            LocationId   INT NULL
        );

        INSERT INTO #Batch (EmployeeId, ECode, EmployeeName, LocationId)
        SELECT TOP(@BatchSize) EmployeeId, ECode, EmployeeName, LocationId
        FROM #ActiveEmp
        WHERE Processed = 0
        ORDER BY EmployeeId;  -- stable ordering avoids deadlock cycles

        -- Mark these as "in progress"
        UPDATE ae
        SET ae.Processed = 1
        FROM #ActiveEmp ae
        JOIN #Batch b ON b.EmployeeId = ae.EmployeeId;

        ------------------------------------------------------------------
        -- 2) For this batch, pull 3-day facts via your TVF
        ------------------------------------------------------------------
        IF OBJECT_ID('tempdb..#Facts') IS NOT NULL DROP TABLE #Facts;
        CREATE TABLE #Facts (
            EmployeeId   BIGINT       NOT NULL,
            ECode        NVARCHAR(50) NOT NULL,
            PunchDate    DATE         NOT NULL,
            StatusLabel  NVARCHAR(50) NULL,
            IsHoliday    BIT          NOT NULL,
            IsRegularize BIT          NOT NULL,
            ValidPunches INT          NOT NULL,
            PRIMARY KEY (EmployeeId, PunchDate)
        );

        -- Call TVF once per employee in the batch (date window is tiny => fast)
        INSERT INTO #Facts (EmployeeId, ECode, PunchDate, StatusLabel, IsHoliday, IsRegularize, ValidPunches)
        SELECT
            f.EmployeeId,
            f.ECode,
            f.AttendanceDate,
            f.Status,
            CAST(f.IsHoliday AS BIT),
            CAST(f.IsRegularize AS BIT),
            ISNULL(f.ValidPunchCount, 0)
        FROM #Batch b
        CROSS APPLY dbo.fn_GetMonthlyPunchesRange_productionnewnick_test(@FromDate, @ToDate, b.ECode) AS f
        OPTION (RECOMPILE);  -- good for small, varying batches

        ------------------------------------------------------------------
        -- 3) Build exclusions for this batch
        --    (Holiday/WeeklyOff/Regularize OR Leave overlap)
        ------------------------------------------------------------------
        IF OBJECT_ID('tempdb..#Excl') IS NOT NULL DROP TABLE #Excl;
        CREATE TABLE #Excl (EmployeeId BIGINT NOT NULL PRIMARY KEY);

        -- Holiday or Weekly Off on ANY of the 3 days
        INSERT INTO #Excl(EmployeeId)
        SELECT DISTINCT EmployeeId
        FROM #Facts
        WHERE IsHoliday = 1
           OR StatusLabel = N'Weekly Off';

        -- Any regularization on any day
        INSERT INTO #Excl(EmployeeId)
        SELECT DISTINCT EmployeeId
        FROM #Facts
        WHERE IsRegularize = 1
          AND EmployeeId NOT IN (SELECT EmployeeId FROM #Excl);

        -- Leave overlap (precomputed)
        INSERT INTO #Excl(EmployeeId)
        SELECT le.EmployeeId
        FROM #LeaveEmp le
        JOIN #Batch b ON b.EmployeeId = le.EmployeeId
        WHERE le.EmployeeId NOT IN (SELECT EmployeeId FROM #Excl);

        ------------------------------------------------------------------
        -- 4) Seen logic: any presence-like signal across the window
        ------------------------------------------------------------------
        IF OBJECT_ID('tempdb..#Seen') IS NOT NULL DROP TABLE #Seen;
        CREATE TABLE #Seen (EmployeeId BIGINT NOT NULL PRIMARY KEY);

        INSERT INTO #Seen(EmployeeId)
        SELECT DISTINCT EmployeeId
        FROM #Facts
        WHERE StatusLabel IN (N'Present', N'Manual Present', N'Manual Present Half Day', N'Half Day Absent', N'Mispunch', N'GF')
           OR ValidPunches > 0;

        ------------------------------------------------------------------
        -- 5) Final to-abscond for this batch
        ------------------------------------------------------------------
        IF OBJECT_ID('tempdb..#ToAbscond') IS NOT NULL DROP TABLE #ToAbscond;
        CREATE TABLE #ToAbscond (
            EmployeeId   BIGINT       NOT NULL PRIMARY KEY,
            ECode        NVARCHAR(50) NOT NULL,
            EmployeeName NVARCHAR(255) NULL
        );

        INSERT INTO #ToAbscond(EmployeeId, ECode, EmployeeName)
        SELECT b.EmployeeId, b.ECode, b.EmployeeName
        FROM #Batch b
        WHERE b.EmployeeId NOT IN (SELECT EmployeeId FROM #Excl)
          AND b.EmployeeId NOT IN (SELECT EmployeeId FROM #Seen)
          AND 5 = (SELECT COUNT(DISTINCT f.PunchDate) FROM #Facts f WHERE f.EmployeeId = b.EmployeeId);

        -- If nothing to abscond in this batch, continue to next
        IF NOT EXISTS (SELECT 1 FROM #ToAbscond)
            CONTINUE;

        ------------------------------------------------------------------
        -- 6) Write-throughs: short transaction per batch
        ------------------------------------------------------------------
        BEGIN TRY
            BEGIN TRAN;

            /* UPSERT: Update existing Type-10 separations */
            UPDATE es WITH (ROWLOCK, UPDLOCK)
            SET es.AbscondRemarks       = @AbscondRemarks,
                es.AbscondedBy          = @AbscondedBy,
                es.IsApprovedByManager  = 0,
                es.ManagerRemarks       = NULL,
                es.IsApprovedByHR       = 0,
                es.HRRemarks            = NULL,
                es.ResignationDate      = @Now
            FROM dbo.tblEmployeeSepration es
            JOIN #ToAbscond t ON t.EmployeeId = es.EmployeeId
            WHERE es.ResignationTypeId = 10;

            /* INSERT missing Type-10 separations */
            INSERT INTO dbo.tblEmployeeSepration
                (EmployeeId, ResignationTypeId, AbscondRemarks, AbscondedBy, ResignationDate,
                 IsApprovedByManager, ManagerRemarks, IsApprovedByHR, HRRemarks)
            SELECT t.EmployeeId, 10, @AbscondRemarks, @AbscondedBy, @Now,
                   0, NULL, 0, NULL
            FROM #ToAbscond t
            WHERE NOT EXISTS (
                SELECT 1 FROM dbo.tblEmployeeSepration es WITH (READPAST)
                WHERE es.EmployeeId = t.EmployeeId
                  AND es.ResignationTypeId = 10
            );

            /* Deactivate employees */
            UPDATE e WITH (ROWLOCK, UPDLOCK)
            SET e.IsActive = 0
            FROM dbo.tblEmployee e
            JOIN #ToAbscond t ON t.EmployeeId = e.EmployeeId
            WHERE e.IsActive <> 0;

            /* Abscond history: one row per employee per day */
            INSERT INTO dbo.AbscondHistory (EmployeeId, ECode, EmployeeName, AbscondDate, Remarks, CreatedBy)
            SELECT t.EmployeeId, t.ECode, t.EmployeeName, @Now, @AbscondRemarks, @AbscondedBy
            FROM #ToAbscond t
            WHERE NOT EXISTS (
                SELECT 1
                FROM dbo.AbscondHistory ah WITH (READPAST)
                WHERE ah.EmployeeId = t.EmployeeId
                  AND CAST(ah.AbscondDate AS DATE) = CAST(@Now AS DATE)
            );

            COMMIT TRAN;
        END TRY
        BEGIN CATCH
            IF XACT_STATE() <> 0 ROLLBACK TRAN;

            -- Surface minimal, safe error context for this batch, then continue
            -- (Optionally log to an internal table for ops)
            -- RAISERROR could abort the procedure; we choose to continue to next batch.
        END CATCH;

        ------------------------------------------------------------------
        -- 7) Accumulate output
        ------------------------------------------------------------------
        INSERT INTO #AbscondedOut(EmployeeId, ECode, EmployeeName, AbscondedOn, Remarks)
        SELECT t.EmployeeId, t.ECode, t.EmployeeName, @Now, @AbscondRemarks
        FROM #ToAbscond t
        WHERE NOT EXISTS (SELECT 1 FROM #AbscondedOut o WHERE o.EmployeeId = t.EmployeeId);
    END

    ----------------------------------------------------------------------
    -- 8) Final result set
    ----------------------------------------------------------------------
    SELECT EmployeeId, ECode, EmployeeName, AbscondedOn, Remarks
    FROM #AbscondedOut
    ORDER BY EmployeeName;

    -- Cleanup handled by temp-table scope end
    SET NOCOUNT OFF;
END;

