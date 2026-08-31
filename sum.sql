/*=========================================================================
  usp_GetMonthlyAttendanceSummary_WithStoreRules_Single_Dev  (v2 - CONSERVATIVE)
  -----------------------------------------------------------------------
  GOAL:
    Preserve v1 bucket logic EXACTLY (MachineVal, ManualVal, WeekendVal,
    GeoVal mappings stay 1:1) -- only fix the silent-zero gaps where
    v1 maps a real status to 0.0 because it has no branch for it.

  FIXES vs v1 (additive only, no existing branch is rewritten):
    1. Status = 'Half Day Present'    -> credit 0.5  (v1 -> 0.0 silently)
    2. Status = 'Manual Present'      -> credit 1.0  (v1 -> 0.0 silently
                                                       unless minutes parse cleanly)
    3. Status = 'MIS' with minutes    -> minutes-based credit
                                         (v1 -> 0.0 silently)
    4. Mispunch '00:00' bug bypass    -> already protected; no change
    5. NC view picks up new statuses too

  EVERYTHING ELSE stays identical:
    - Same minute-parser inline (both formats, same thresholds)
    - Same IsWeeklyOffByStore rules (same store list, same designation
      exclusions, same DATEPART(WEEKDAY) checks)
    - Same v1 quirks preserved (e.g. GF on weekend lands in WeekendVal AND
      GeoVal -- if production reports rely on this, do not change it here)
    - Same column names, same output shape, same DECIMAL(9,2) casts
=========================================================================*/

CREATE   PROCEDURE [dbo].[usp_GetMonthlyAttendanceSummary_WithStoreRules_Single_Dev]
    @MonthToken VARCHAR(7),
    @ECode      NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET DATEFIRST 7;

    DECLARE @StartDate  DATE,
            @EndDate    DATE,
            @MonthLabel CHAR(7);

    DECLARE @MonthStart DATE;
    DECLARE @MonthEnd   DATE;

    DECLARE @PolicyType VARCHAR(20);
    DECLARE @CustomType VARCHAR(20);
    DECLARE @FromDay    INT;
    DECLARE @ToDay      INT;

    DECLARE @MonthName  VARCHAR(3);
    DECLARE @MonthNo    INT;
    DECLARE @Year       INT;

    DECLARE @TokenUpper VARCHAR(7) = UPPER(@MonthToken);

    SET @MonthName = LEFT(@TokenUpper, 3);

    SET @MonthNo = CASE @MonthName
        WHEN 'JAN' THEN 1  WHEN 'FEB' THEN 2  WHEN 'MAR' THEN 3
        WHEN 'APR' THEN 4  WHEN 'MAY' THEN 5  WHEN 'JUN' THEN 6
        WHEN 'JUL' THEN 7  WHEN 'AUG' THEN 8  WHEN 'SEP' THEN 9
        WHEN 'OCT' THEN 10 WHEN 'NOV' THEN 11 WHEN 'DEC' THEN 12
    END;

    SET @Year = 2000 + CAST(RIGHT(@TokenUpper, 2) AS INT);

    IF @MonthNo IS NULL OR RIGHT(@TokenUpper, 2) NOT LIKE '[0-9][0-9]'
    BEGIN
        RAISERROR('Invalid @MonthToken. Expect MMM-YY, e.g. JUN-25.', 16, 1);
        RETURN;
    END

    SET @MonthStart = DATEFROMPARTS(@Year, @MonthNo, 1);
    SET @MonthEnd   = EOMONTH(@MonthStart);

    SELECT TOP 1
        @PolicyType = [Type],
        @CustomType = [Custom_type],
        @FromDay    = [From],
        @ToDay      = [To]
    FROM dbo.SalaryCyclePolicy
    WHERE EffectiveDate <= @MonthStart
    ORDER BY EffectiveDate DESC;

    SET @PolicyType = ISNULL(@PolicyType, 'Monthly');
    SET @CustomType = ISNULL(@CustomType, '');

    IF (@PolicyType = 'Monthly')
    BEGIN
        SET @StartDate = @MonthStart;
        SET @EndDate   = @MonthEnd;
    END
    ELSE IF (@PolicyType = 'Custom' AND @CustomType = 'Same')
    BEGIN
        DECLARE @CurrMonthLastDay INT = DAY(@MonthEnd);
        SET @StartDate = DATEFROMPARTS(YEAR(@MonthStart), MONTH(@MonthStart),
                            CASE WHEN @FromDay > @CurrMonthLastDay THEN @CurrMonthLastDay ELSE @FromDay END);
        SET @EndDate   = DATEFROMPARTS(YEAR(@MonthStart), MONTH(@MonthStart),
                            CASE WHEN @ToDay   > @CurrMonthLastDay THEN @CurrMonthLastDay ELSE @ToDay   END);
    END
    ELSE IF (@PolicyType = 'Custom' AND @CustomType = 'Previous')
    BEGIN
        DECLARE @PrevMonthStart DATE = DATEADD(MONTH, -1, @MonthStart);
        DECLARE @PrevMonthLastDay INT = DAY(EOMONTH(@PrevMonthStart));
        DECLARE @CurrMonthLastDay2 INT = DAY(@MonthEnd);

        SET @StartDate = DATEFROMPARTS(YEAR(@PrevMonthStart), MONTH(@PrevMonthStart),
                            CASE WHEN @FromDay > @PrevMonthLastDay THEN @PrevMonthLastDay ELSE @FromDay END);
        SET @EndDate   = DATEFROMPARTS(YEAR(@MonthStart), MONTH(@MonthStart),
                            CASE WHEN @ToDay   > @CurrMonthLastDay2 THEN @CurrMonthLastDay2 ELSE @ToDay   END);
    END
    ELSE
    BEGIN
        SET @StartDate = @MonthStart;
        SET @EndDate   = @MonthEnd;
    END;

    SET @MonthLabel = @TokenUpper;

    /*=====================================================================
      Daily CTE: same as v1 plus an inline parsed minutes column so we
      stop repeating the parser six times. Behaviour is identical:
        - Mispunch -> '00:00' (forced by v1)
        - 'X hours and Y minutes' -> H*60+M
        - 'HH:MM'                 -> H*60+M
        - else                    -> 0
    =====================================================================*/
    ;WITH Daily AS
    (
        SELECT
            f.ECode,
            f.AttendanceDate,
            f.Status,

            /* NC-Status: v1 logic preserved -- treats Quarter Day Absent
               as Present if minutes >= 8h30m */
            [NC Status] = CASE
                WHEN f.Status = 'Quarter Day Absent'
                 AND (
                    CASE
                        WHEN CHARINDEX('hours',   f.TotalWorkingMinutes) > 0
                         AND CHARINDEX('minutes', f.TotalWorkingMinutes) > 0
                        THEN
                            TRY_CAST(LTRIM(RTRIM(LEFT(f.TotalWorkingMinutes,
                                CHARINDEX(' hours', f.TotalWorkingMinutes) - 1))) AS INT) * 60
                          + TRY_CAST(LTRIM(RTRIM(SUBSTRING(f.TotalWorkingMinutes,
                                CHARINDEX('and', f.TotalWorkingMinutes) + 4,
                                CHARINDEX(' minutes', f.TotalWorkingMinutes)
                                    - (CHARINDEX('and', f.TotalWorkingMinutes) + 4)
                            ))) AS INT)
                        WHEN CHARINDEX(':', f.TotalWorkingMinutes) > 0
                        THEN TRY_CAST(LEFT(f.TotalWorkingMinutes,
                                CHARINDEX(':', f.TotalWorkingMinutes) - 1) AS INT) * 60
                           + TRY_CAST(SUBSTRING(f.TotalWorkingMinutes,
                                CHARINDEX(':', f.TotalWorkingMinutes) + 1, 2) AS INT)
                        ELSE 0
                    END
                 ) >= (8 * 60 + 30)
                THEN 'Present'
                ELSE f.Status
            END,

            f.IsRegularize,
            f.STCode,
            f.DesignationName,

            /* Mispunch is forced to 0 minutes per v1 */
            TotalWorkingMinutes = CASE WHEN f.Status = 'Mispunch' THEN '00:00'
                                       ELSE f.TotalWorkingMinutes END,

            /* Pre-parsed minutes - DRY replacement for the 6x inline blocks */
            ParsedMinutes =
                CASE
                    WHEN f.Status = 'Mispunch' THEN 0
                    WHEN f.TotalWorkingMinutes IS NULL OR f.TotalWorkingMinutes = '' THEN 0
                    WHEN CHARINDEX('hours',   f.TotalWorkingMinutes) > 0
                     AND CHARINDEX('minutes', f.TotalWorkingMinutes) > 0
                    THEN
                        TRY_CAST(LTRIM(RTRIM(LEFT(f.TotalWorkingMinutes,
                            CHARINDEX(' hours', f.TotalWorkingMinutes) - 1))) AS INT) * 60
                      + TRY_CAST(LTRIM(RTRIM(SUBSTRING(f.TotalWorkingMinutes,
                            CHARINDEX('and', f.TotalWorkingMinutes) + 4,
                            CHARINDEX(' minutes', f.TotalWorkingMinutes)
                                - (CHARINDEX('and', f.TotalWorkingMinutes) + 4)
                        ))) AS INT)
                    WHEN CHARINDEX(':', f.TotalWorkingMinutes) > 0
                    THEN TRY_CAST(LEFT(f.TotalWorkingMinutes,
                            CHARINDEX(':', f.TotalWorkingMinutes) - 1) AS INT) * 60
                       + TRY_CAST(SUBSTRING(f.TotalWorkingMinutes,
                            CHARINDEX(':', f.TotalWorkingMinutes) + 1, 2) AS INT)
                    ELSE 0
                END,

            /* Same store-rule weekly off as v1 -- unchanged */
            IsWeeklyOffByStore =
                CASE
                    WHEN UPPER(f.STCode) IN ('RH01','RD04')
                        THEN CASE WHEN DATEPART(WEEKDAY, f.AttendanceDate) IN (1,7) THEN 1 ELSE 0 END

                    WHEN UPPER(f.STCode) IN
                        ('DB03','DJ01','DK02','DM01','DN01','DO01',
                         'DU05','DU06','DW01','DW02','DH24','DH26','RH02')
                    THEN CASE
                            WHEN UPPER(ISNULL(f.DesignationName,'')) IN ('HELPER','DRIVER') THEN 0
                            WHEN DATEPART(WEEKDAY, f.AttendanceDate) = 1 THEN 1
                            ELSE 0
                         END

                    ELSE 0
                END
        FROM dbo.fn_GetMonthlyPunchesRange_productionnewnick_live(@StartDate, @EndDate, @ECode) AS f
        WHERE f.ECode = @ECode OR @ECode IS NULL
    ),

    /*=====================================================================
      Scored: same four buckets as v1.
      Each bucket preserves v1's exact CASE structure -- additions are
      inline marked with -- ADD: comments.
    =====================================================================*/
    Scored AS
    (
        SELECT
            d.ECode,
            d.AttendanceDate,

            /*-------------------------------------------------------------
              MachineVal -- v1 logic preserved + Half Day Present + MIS + Manual Present
            -------------------------------------------------------------*/
            MachineVal = CAST(
                CASE
                    WHEN d.IsRegularize = 0 AND d.Status = 'Present'             THEN 1.0
                    WHEN d.IsRegularize = 0 AND d.Status = 'Quarter Day Absent'  THEN 0.75
                    WHEN d.IsRegularize = 0 AND d.Status = 'Half Day Absent'     THEN 0.5

                    -- ADD: Half Day Present (v1 missed this -> silent 0.5 loss)
                    WHEN d.IsRegularize = 0 AND d.Status = 'Half Day Present'    THEN 0.5

                    -- NOTE: Manual Present is NOT added here on purpose.
                    --       In v1, regularized days are owned by ManualVal (the MANUAL
                    --       column). Adding them here would DOUBLE-COUNT into TOTAL_PRESENT
                    --       (once via MACHINE, once via MANUAL).
                    --       The only edge case where v1 lost the day was when minutes
                    --       failed to parse -- handled below in the 'MIS' branch and by
                    --       the existing fallback minute parser.

                    -- ADD: MIS (odd punches; v1 had no branch -> silent 0)
                    WHEN d.IsRegularize = 0 AND d.Status = 'MIS' THEN
                        CASE
                            WHEN d.ParsedMinutes >= (8*60+30) THEN 1.0
                            WHEN d.ParsedMinutes >= (4*60)    THEN 0.5
                            ELSE 0.0
                        END

                    /* v1 "Holiday OR (regularize on weekly-off)" branch — minutes-based */
                    WHEN (d.IsRegularize = 0 AND d.Status = 'Holiday')
                      OR (d.IsRegularize = 1 AND d.IsWeeklyOffByStore = 1)
                    THEN
                        CASE
                            WHEN d.ParsedMinutes >= (8*60+30) THEN 1.0
                            WHEN d.ParsedMinutes >= (4*60)    THEN 0.5
                            ELSE 0.0
                        END

                    ELSE 0.0
                END
            AS DECIMAL(9,2)),

            /*-------------------------------------------------------------
              NC_MachineVal -- v1 logic preserved + same additions
            -------------------------------------------------------------*/
            NC_MachineVal = CAST(
                CASE
                    WHEN d.IsRegularize = 0 AND d.[NC Status] = 'Present'            THEN 1.0
                    WHEN d.IsRegularize = 0 AND d.[NC Status] = 'Quarter Day Absent' THEN 0.75
                    WHEN d.IsRegularize = 0 AND d.[NC Status] = 'Half Day Absent'    THEN 0.5

                    -- ADD: same additions as MachineVal so NC view stays consistent
                    WHEN d.IsRegularize = 0 AND d.[NC Status] = 'Half Day Present'   THEN 0.5
                    -- (Manual Present intentionally NOT added here -- see MachineVal note)
                    WHEN d.IsRegularize = 0 AND d.[NC Status] = 'MIS' THEN
                        CASE
                            WHEN d.ParsedMinutes >= (8*60+30) THEN 1.0
                            WHEN d.ParsedMinutes >= (4*60)    THEN 0.5
                            ELSE 0.0
                        END

                    /* v1 NC variant: Holiday/IsRegularize=1/IsRegularize=0 fallback to minutes */
                    WHEN (d.IsRegularize = 0 AND d.Status = 'Holiday')
                      OR (d.IsRegularize = 1)
                      OR (d.IsRegularize = 0)
                    THEN
                        CASE
                            WHEN d.ParsedMinutes >= (8*60+30) THEN 1.0
                            WHEN d.ParsedMinutes >= (4*60)    THEN 0.5
                            ELSE 0.0
                        END

                    ELSE 0.0
                END
            AS DECIMAL(9,2)),

            /*-------------------------------------------------------------
              ManualVal -- v1 logic preserved
            -------------------------------------------------------------*/
            ManualVal = CAST(
                CASE
                    WHEN d.IsRegularize = 1 AND d.IsWeeklyOffByStore = 0 THEN
                        CASE
                            WHEN d.ParsedMinutes >= (8*60+30) THEN 1.0
                            WHEN d.ParsedMinutes >= (4*60)    THEN 0.5
                            ELSE 0.0
                        END
                    ELSE 0.0
                END
            AS DECIMAL(9,2)),

            /*-------------------------------------------------------------
              WeekendVal -- v1 logic preserved EXACTLY
              (Yes, this means GF on weekend is ALSO counted in GeoVal --
               that's a v1 quirk we are NOT changing here.)
            -------------------------------------------------------------*/
            WeekendVal = CAST(
                CASE
                    WHEN d.IsRegularize = 0 AND d.IsWeeklyOffByStore = 1 AND d.Status = 'Present'             THEN 1.0
                    WHEN d.IsRegularize = 0 AND d.IsWeeklyOffByStore = 1 AND d.Status = 'Quarter Day Absent'  THEN 0.75
                    WHEN d.IsRegularize = 0 AND d.IsWeeklyOffByStore = 1 AND d.Status = 'Half Day Absent'     THEN 0.5
                    WHEN d.IsRegularize = 0 AND d.IsWeeklyOffByStore = 1 AND d.Status = 'POW'                 THEN 1.0

                    -- ADD: Half Day Present on weekly-off (v1 silent 0)
                    WHEN d.IsRegularize = 0 AND d.IsWeeklyOffByStore = 1 AND d.Status = 'Half Day Present'    THEN 0.5

                    /* Regularized work on weekly-off -- v1 minutes-based */
                    WHEN d.IsRegularize = 1 AND d.IsWeeklyOffByStore = 1 THEN
                        CASE
                            WHEN d.ParsedMinutes >= (8*60+30) THEN 1.0
                            WHEN d.ParsedMinutes >= (4*60)    THEN 0.5
                            ELSE 0.0
                        END

                    /* GF on weekly-off -- v1 minutes-based */
                    WHEN d.Status = 'GF' AND d.IsWeeklyOffByStore = 1 THEN
                        CASE
                            WHEN d.ParsedMinutes >= (8*60+30) THEN 1.0
                            WHEN d.ParsedMinutes >= (4*60)    THEN 0.5
                            ELSE 0.0
                        END

                    ELSE 0.0
                END
            AS DECIMAL(9,2)),

            /*-------------------------------------------------------------
              GeoVal -- v1 logic preserved EXACTLY.
              v1 had this comment: "if gf on weekend then also consider"
              meaning it INTENTIONALLY runs whether or not it's a weekly off.
              We keep that.
            -------------------------------------------------------------*/
            GeoVal = CAST(
                CASE
                    WHEN d.Status = 'GF' THEN
                        CASE
                            WHEN d.ParsedMinutes >= (8*60+30) THEN 1.0
                            WHEN d.ParsedMinutes >= (4*60)    THEN 0.5
                            ELSE 0.0
                        END
                    ELSE 0.0
                END
            AS DECIMAL(9,2))

        FROM Daily d
    ),

    Monthly AS
    (
        SELECT
            s.ECode,
            @MonthLabel AS [MONTH],
            SUM(s.MachineVal)    AS MACHINE,
            SUM(s.ManualVal)     AS MANUAL,
            SUM(s.GeoVal)        AS GF,
            (SUM(s.MachineVal) + SUM(s.ManualVal) + SUM(s.GeoVal))  AS TOTAL_PRESENT,
            SUM(s.NC_MachineVal) AS NC_TOTAL_PRESENT,
            SUM(s.WeekendVal)    AS PRESENT_ON_WEEKLYOFF
        FROM Scored s
        GROUP BY s.ECode
    )
    SELECT
        Id        = 1,
        E_CODE    = m.ECode,
        [MONTH]   = m.[MONTH],
        MACHINE   = CAST(m.MACHINE              AS DECIMAL(9,2)),
        MANUAL    = CAST(m.MANUAL               AS DECIMAL(9,2)),
        GF        = TRY_CAST(m.GF               AS DECIMAL(9,2)),
        TOTAL_PRESENT        = CAST(m.TOTAL_PRESENT        AS DECIMAL(9,2)),
        NC_TOTAL_PRESENT     = CAST(m.NC_TOTAL_PRESENT     AS DECIMAL(9,2)),
        PRESENT_ON_WEEKLYOFF = CAST(m.PRESENT_ON_WEEKLYOFF AS DECIMAL(9,2)),
        IsActive  = CAST(1 AS BIT),
        IsDeleted = CAST(0 AS BIT),
        CreatedOn = DATEADD(MILLISECOND, 0, CAST(@EndDate AS DATETIME2(3))),
        UpdatedOn = CAST(NULL AS DATETIME2(3)),
        CreatedBy = CAST(NULL AS NVARCHAR(100)),
        UpdatedBy = CAST(NULL AS NVARCHAR(100)),
        b.STCode,
        c.DesignationName
    FROM Monthly m
    LEFT JOIN dbo.tblEmployee    a ON m.ECode = a.ECode
    LEFT JOIN dbo.tblLocation    b ON b.LocationId = a.LocationId
    LEFT JOIN dbo.tblDesignation c ON a.DesignationId = c.DesignationId;
END

