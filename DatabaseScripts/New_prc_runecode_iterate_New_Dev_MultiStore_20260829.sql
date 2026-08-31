/*==============================================================================
  NEW PROCEDURE - prc_runecode_iterate_New_Dev_MultiStore
  Created 2026-08-29

  PURPOSE
    Run salary for SEVERAL store codes in one call.

    The existing single-store proc
        dbo.prc_runecode_iterate_New_Dev_LocationWise
    is NOT modified in any way. This new proc simply calls it once per store
    code and collects the results. If you ever drop this wrapper, the current
    single-store flow keeps working exactly as it does today.

  USAGE
        DECLARE @Msg nvarchar(max), @rv int;

        EXEC @rv = dbo.prc_runecode_iterate_New_Dev_MultiStore
             @MonthKey  = N'Aug-26',
             @STCodes   = N'RH02,HM45,HM59,HM52',
             @SkippedEcodesMsg = @Msg OUTPUT;

        SELECT @Msg AS [@SkippedEcodesMsg];

    Store codes are comma separated. Spaces and blank entries are ignored,
    duplicates are collapsed, and the order you type them is the order they run.

  BEHAVIOUR NOTES - please read before running
    1. BATCH NUMBERS. The inner proc allocates its own batch number
       (MAX(BatchNo)+1) on every call, so EACH STORE GETS ITS OWN BATCH NUMBER.
       This wrapper does not change that. The per-store result set below tells
       you exactly which batch each store landed in.

    2. THE INNER PROC IS SLOW. It runs
           usp_MergeEmpAttendanceFromMonthlySummary_Single_Dev @MonthKey, NULL
       on every call, and that merge covers ALL employees for the month, not
       just the store being processed. Passing 5 stores therefore repeats that
       full-month merge 5 times. Budget the time accordingly and run this from
       SSMS, not from the API (the API times out at 600 s).

    3. ONE STORE FAILING DOES NOT ABORT THE REST. Each store is wrapped in
       TRY/CATCH; a failure is recorded against that store and the loop
       continues to the next one.

    4. UNKNOWN STORE CODES are reported explicitly rather than silently doing
       nothing, and codes longer than 7 characters are rejected up front
       because the inner proc declares @STCode as NVARCHAR(7) and would
       silently truncate them.

  RETURNS
    - Output param @SkippedEcodesMsg : one summary line per store.
    - A result set               : one row per store with batch number,
                                   snapshot rows written, duration and message.
    - Return value 0 = every store succeeded, 1 = at least one store failed
      or matched nothing.
==============================================================================*/

USE [HRMS];
GO

CREATE OR ALTER PROCEDURE [dbo].[prc_runecode_iterate_New_Dev_MultiStore]
    @MonthKey         NVARCHAR(7),              -- e.g. 'Aug-26'
    @STCodes          NVARCHAR(MAX),            -- e.g. 'RH02,HM45,HM59'
    @EmployeeIds      NVARCHAR(MAX) = NULL,     -- optional, passed straight through
    @SkippedEcodesMsg NVARCHAR(MAX) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    ---------------------------------------------------------------------------
    -- 1. Validate inputs
    ---------------------------------------------------------------------------
    IF @MonthKey IS NULL OR LTRIM(RTRIM(@MonthKey)) = N''
    BEGIN
        SET @SkippedEcodesMsg = N'NOTHING PROCESSED - @MonthKey is required (e.g. ''Aug-26'').';
        RETURN 1;
    END

    IF @STCodes IS NULL OR LTRIM(RTRIM(@STCodes)) = N''
    BEGIN
        SET @SkippedEcodesMsg = N'NOTHING PROCESSED - @STCodes is required (e.g. ''RH02,HM45'').';
        RETURN 1;
    END

    ---------------------------------------------------------------------------
    -- 2. Split, trim, drop blanks, collapse duplicates, keep the typed order
    ---------------------------------------------------------------------------
    DECLARE @Stores TABLE
    (
        Seq     INT IDENTITY(1,1) PRIMARY KEY,
        STCode  NVARCHAR(50) NOT NULL
    );

    ;WITH Split AS
    (
        SELECT  STCode = LTRIM(RTRIM(s.value)),
                Ord    = ROW_NUMBER() OVER (ORDER BY (SELECT NULL))
        FROM STRING_SPLIT(@STCodes, ',') AS s
        WHERE LTRIM(RTRIM(s.value)) <> N''
    ),
    Distinct1 AS
    (
        SELECT STCode, Ord = MIN(Ord)
        FROM Split
        GROUP BY STCode
    )
    INSERT INTO @Stores (STCode)
    SELECT STCode FROM Distinct1 ORDER BY Ord;

    IF NOT EXISTS (SELECT 1 FROM @Stores)
    BEGIN
        SET @SkippedEcodesMsg = N'NOTHING PROCESSED - @STCodes contained no usable store code.';
        RETURN 1;
    END

    ---------------------------------------------------------------------------
    -- 3. Per-store results
    ---------------------------------------------------------------------------
    DECLARE @Result TABLE
    (
        Seq         INT,
        STCode      NVARCHAR(50),
        Outcome     NVARCHAR(20),          -- OK | NOTHING | UNKNOWN STORE | TOO LONG | ERROR
        BatchNo     INT          NULL,
        RowsWritten INT          NULL,
        Seconds     INT          NULL,
        [Message]   NVARCHAR(MAX) NULL
    );

    DECLARE @Seq       INT,
            @STCode    NVARCHAR(50),
            @Msg       NVARCHAR(MAX),
            @BatchBefore INT,
            @BatchAfter  INT,
            @Rows      INT,
            @T0        DATETIME2(0),
            @rv        INT,
            @StoreCount INT;

    -- Held in a variable because PRINT does not accept a subquery.
    SELECT @StoreCount = COUNT(*) FROM @Stores;

    DECLARE curStore CURSOR LOCAL FAST_FORWARD FOR
        SELECT Seq, STCode FROM @Stores ORDER BY Seq;

    OPEN curStore;
    FETCH NEXT FROM curStore INTO @Seq, @STCode;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @Msg  = NULL;
        SET @Rows = NULL;
        SET @T0   = SYSDATETIME();

        -- 3a. Reject codes the inner proc would silently truncate
        IF LEN(@STCode) > 7
        BEGIN
            INSERT INTO @Result (Seq, STCode, Outcome, [Message])
            VALUES (@Seq, @STCode, N'TOO LONG',
                    N'Skipped - store code is ' + CAST(LEN(@STCode) AS NVARCHAR(10))
                  + N' characters. prc_runecode_iterate_New_Dev_LocationWise declares '
                  + N'@STCode as NVARCHAR(7) and would truncate it.');
        END
        -- 3b. Reject codes that do not exist, rather than running a no-op
        ELSE IF NOT EXISTS (SELECT 1 FROM tblLocation WHERE STCode = @STCode)
        BEGIN
            INSERT INTO @Result (Seq, STCode, Outcome, [Message])
            VALUES (@Seq, @STCode, N'UNKNOWN STORE',
                    N'Skipped - no row in tblLocation has STCode = ''' + @STCode + N'''. '
                  + N'Check the spelling; nothing was processed for it.');
        END
        ELSE
        BEGIN
            BEGIN TRY
                SELECT @BatchBefore = ISNULL(MAX(BatchNo), 0) FROM EmpAttendanceViewSnapshot;

                PRINT N'=== Store ' + @STCode + N' (' + CAST(@Seq AS NVARCHAR(10))
                    + N' of ' + CAST(@StoreCount AS NVARCHAR(10))
                    + N') starting at ' + CONVERT(NVARCHAR(19), SYSDATETIME(), 120) + N' ===';

                -- The existing single-store proc, called exactly as it is today.
                EXEC @rv = dbo.prc_runecode_iterate_New_Dev_LocationWise
                        @MonthKey         = @MonthKey,
                        @STCode           = @STCode,
                        @EmployeeIds      = @EmployeeIds,
                        @SkippedEcodesMsg = @Msg OUTPUT;

                SELECT @BatchAfter = ISNULL(MAX(BatchNo), 0) FROM EmpAttendanceViewSnapshot;

                IF @BatchAfter > @BatchBefore
                    SELECT @Rows = COUNT(*)
                    FROM EmpAttendanceViewSnapshot
                    WHERE BatchNo = @BatchAfter;
                ELSE
                    SELECT @Rows = 0, @BatchAfter = NULL;

                INSERT INTO @Result (Seq, STCode, Outcome, BatchNo, RowsWritten, Seconds, [Message])
                VALUES (@Seq, @STCode,
                        CASE WHEN ISNULL(@Rows,0) = 0 THEN N'NOTHING' ELSE N'OK' END,
                        @BatchAfter, @Rows,
                        DATEDIFF(SECOND, @T0, SYSDATETIME()),
                        @Msg);
            END TRY
            BEGIN CATCH
                INSERT INTO @Result (Seq, STCode, Outcome, Seconds, [Message])
                VALUES (@Seq, @STCode, N'ERROR',
                        DATEDIFF(SECOND, @T0, SYSDATETIME()),
                        N'ERROR ' + CAST(ERROR_NUMBER() AS NVARCHAR(10))
                      + N' at line ' + CAST(ERROR_LINE() AS NVARCHAR(10))
                      + N': ' + ERROR_MESSAGE());
            END CATCH
        END

        FETCH NEXT FROM curStore INTO @Seq, @STCode;
    END

    CLOSE curStore;
    DEALLOCATE curStore;

    ---------------------------------------------------------------------------
    -- 4. Build the summary message - one line per store
    ---------------------------------------------------------------------------
    DECLARE @Ok INT, @Bad INT, @TotalRows INT;

    -- ISNULL goes INSIDE the SUM: skipped stores leave RowsWritten NULL, and
    -- summing over NULLs raises the noisy "Null value is eliminated by an
    -- aggregate" warning, which looks like a failure during a real run.
    SELECT @Ok        = SUM(CASE WHEN Outcome = N'OK' THEN 1 ELSE 0 END),
           @Bad       = SUM(CASE WHEN Outcome <> N'OK' THEN 1 ELSE 0 END),
           @TotalRows = SUM(ISNULL(RowsWritten, 0))
    FROM @Result;

    SET @SkippedEcodesMsg =
          N'Month ' + @MonthKey + N' | stores requested: '
        + CAST(@StoreCount AS NVARCHAR(10))
        + N' | succeeded: ' + CAST(ISNULL(@Ok,0) AS NVARCHAR(10))
        + N' | not processed: ' + CAST(ISNULL(@Bad,0) AS NVARCHAR(10))
        + N' | total snapshot rows written: ' + CAST(@TotalRows AS NVARCHAR(10));

    SELECT @SkippedEcodesMsg = @SkippedEcodesMsg
         + CHAR(13) + CHAR(10)
         + N'  [' + Outcome + N'] ' + STCode
         + CASE WHEN BatchNo IS NULL THEN N''
                ELSE N' (batch ' + CAST(BatchNo AS NVARCHAR(10))
                   + N', ' + CAST(ISNULL(RowsWritten,0) AS NVARCHAR(10)) + N' rows'
                   + N', ' + CAST(ISNULL(Seconds,0) AS NVARCHAR(10)) + N's)' END
         + N' - ' + ISNULL([Message], N'(no message returned)')
    FROM @Result
    ORDER BY Seq;

    -- 5. Per-store detail for the grid
    SELECT Seq, STCode, Outcome, BatchNo, RowsWritten, Seconds, [Message]
    FROM @Result
    ORDER BY Seq;

    RETURN CASE WHEN ISNULL(@Bad,0) > 0 THEN 1 ELSE 0 END;
END
GO


/*==============================================================================
  HOW TO RUN
==============================================================================*/
/*
USE [HRMS];
GO

DECLARE @return_value    int,
        @SkippedEcodesMsg nvarchar(max);

EXEC @return_value = [dbo].[prc_runecode_iterate_New_Dev_MultiStore]
        @MonthKey         = N'Aug-26',
        @STCodes          = N'RH02,HM45,HM59,HM52',
        -- @EmployeeIds   = N'V51674,V51675',   -- optional
        @SkippedEcodesMsg = @SkippedEcodesMsg OUTPUT;

SELECT @SkippedEcodesMsg AS N'@SkippedEcodesMsg';
SELECT 'Return Value' = @return_value;
GO
*/
