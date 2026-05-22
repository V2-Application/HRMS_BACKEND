-- Temporary shift overrides (with EffectiveTo) auto-revert to the prior shift
-- after expiration:
--   * usp_AssignEmployeeShift no longer closes the prior open-ended row when
--     the new assignment has @EffectiveTo set; the prior row remains open and
--     resumes once the override passes.
--   * usp_ApplyScheduledShifts is rewritten to recompute each employee's
--     active shift from history coverage (runs daily at 4 AM via
--     ScheduledShiftApplicationService).
-- Idempotent CREATE OR ALTER. SET options required for indexed views /
-- filtered indexes on touched tables.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO

CREATE OR ALTER PROCEDURE dbo.usp_AssignEmployeeShift
    @EmployeeId    INT,
    @ShiftId       INT,
    @EffectiveFrom DATE,
    @EffectiveTo   DATE          = NULL,
    @AssignedBy    NVARCHAR(50)  = NULL,
    @Remarks       NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @EffectiveTo IS NOT NULL AND @EffectiveTo < @EffectiveFrom
    BEGIN
        ;THROW 50001, 'EffectiveTo cannot be earlier than EffectiveFrom.', 1;
    END

    DECLARE @Today DATE = CAST(GETDATE() AS DATE);

    BEGIN TRY
        BEGIN TRAN;

        /*====================================================
          CASE 1: Same day assignment → UPDATE ONLY
        ====================================================*/
        IF EXISTS (
            SELECT 1
            FROM dbo.EmployeeShiftHistory
            WHERE EmployeeId = @EmployeeId
              AND EffectiveFrom = @EffectiveFrom
        )
        BEGIN
            UPDATE dbo.EmployeeShiftHistory
               SET ShiftId      = @ShiftId,
                   EffectiveTo  = @EffectiveTo,
                   AssignedBy   = @AssignedBy,
                   Remarks      = @Remarks,
                   AppliedOn    = SYSUTCDATETIME()
             WHERE EmployeeId    = @EmployeeId
               AND EffectiveFrom = @EffectiveFrom;
        END
        ELSE
        BEGIN
            /*================================================
              CASE 2: New row → insert.
              Only permanently close the prior open-ended row
              when the NEW row is also open-ended. For a closed-
              range override, the prior row stays open so it
              resumes after @EffectiveTo passes.
            ================================================*/
            IF @EffectiveTo IS NULL
            BEGIN
                UPDATE h
                   SET h.EffectiveTo = DATEADD(DAY, -1, @EffectiveFrom)
                FROM dbo.EmployeeShiftHistory h
                WHERE h.EmployeeId = @EmployeeId
                  AND h.EffectiveTo IS NULL
                  AND h.EffectiveFrom < @EffectiveFrom;
            END

            INSERT INTO dbo.EmployeeShiftHistory
            (EmployeeId, ShiftId, EffectiveFrom, EffectiveTo, AssignedBy, Remarks)
            VALUES
            (@EmployeeId, @ShiftId, @EffectiveFrom, @EffectiveTo, @AssignedBy, @Remarks);
        END

        /*====================================================
          Apply immediately if effective today or earlier
          (and not already past its EffectiveTo).
        ====================================================*/
        IF (@EffectiveFrom <= @Today
            AND (@EffectiveTo IS NULL OR @EffectiveTo >= @Today))
        BEGIN
            UPDATE dbo.tblEmployee
               SET ShiftID = @ShiftId
             WHERE EmployeeId = @EmployeeId;

            UPDATE dbo.EmployeeShiftHistory
               SET AppliedOn = SYSUTCDATETIME()
             WHERE EmployeeId    = @EmployeeId
               AND EffectiveFrom = @EffectiveFrom;
        END

        COMMIT;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK;

        THROW;
    END CATCH
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_ApplyScheduledShifts
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Today DATE = CAST(GETDATE() AS DATE);

    BEGIN TRAN;

    -- For every employee, the "active" shift today is the most-specific
    -- history row that COVERS today (EffectiveFrom <= Today
    -- <= COALESCE(EffectiveTo, +inf)). When a closed-range override expires,
    -- the prior open-ended row again becomes the covering row, so this
    -- naturally reverts the employee's shift.
    ;WITH covering AS
    (
        SELECT
            h.EmployeeId,
            h.ShiftId,
            h.HistoryId,
            ROW_NUMBER() OVER (
                PARTITION BY h.EmployeeId
                ORDER BY h.EffectiveFrom DESC, h.HistoryId DESC
            ) AS rn
        FROM dbo.EmployeeShiftHistory h
        WHERE h.EffectiveFrom <= @Today
          AND (h.EffectiveTo IS NULL OR h.EffectiveTo >= @Today)
    ),
    target AS
    (
        SELECT EmployeeId, ShiftId, HistoryId
        FROM covering
        WHERE rn = 1
    )
    UPDATE e
       SET e.ShiftID = t.ShiftId
    FROM dbo.tblEmployee e
    INNER JOIN target t ON t.EmployeeId = e.EmployeeId
    WHERE ISNULL(e.ShiftID, 0) <> t.ShiftId;

    -- Stamp AppliedOn on history rows whose EffectiveFrom is today (audit).
    UPDATE h
       SET h.AppliedOn = SYSUTCDATETIME()
    FROM dbo.EmployeeShiftHistory h
    WHERE h.EffectiveFrom = @Today
      AND h.AppliedOn IS NULL;

    COMMIT;
END;
GO
