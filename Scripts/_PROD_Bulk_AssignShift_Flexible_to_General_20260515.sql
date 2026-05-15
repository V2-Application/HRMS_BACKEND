-- =============================================================================
-- PROD BULK SHIFT CHANGE  -- 114 employees, Flexible -> General, eff 2026-05-01
-- =============================================================================
-- Calls dbo.usp_AssignEmployeeShift per employee to keep EmployeeShiftHistory
-- in sync (close old row, insert new), and lets the SP also update
-- tblEmployee.ShiftID (effective date is in the past so it applies immediately).
--
-- Pre-snapshot saved to: tblEmployee_ShiftId_Backup_20260515
-- Wrapped in a single transaction; rolls back on any failure.
-- =============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

DECLARE @NewShiftId    INT          = 1;                                              -- General Shift
DECLARE @EffectiveFrom DATE         = '2026-05-01';
DECLARE @AssignedBy    NVARCHAR(50) = N'SuperAdmin-Bulk20260515';
DECLARE @Remarks       NVARCHAR(200)= N'all 114 employees to general shift effective from 01/05/2026';

-- ---------------------------------------------------------------------------
-- 1) Pre-snapshot (instant rollback target)
-- ---------------------------------------------------------------------------
IF OBJECT_ID('dbo.tblEmployee_ShiftId_Backup_20260515', 'U') IS NOT NULL
    DROP TABLE dbo.tblEmployee_ShiftId_Backup_20260515;

;WITH Ecodes (Ecode) AS (
    SELECT v.Ecode FROM (VALUES
        ('V25415'),('V25659'),('V00025'),('V26191'),('V00498'),('V26510'),('V26548'),
        ('V26552'),('V25541'),('V00362'),('V26600'),('V14071'),('V39630'),('V14436'),
        ('V01305'),('V14940'),('V27810'),('V40561'),('V27695'),('V15104'),('V15109'),
        ('V41278'),('V15999'),('V41772'),('V41802'),('V41805'),('V41810'),('V41862'),
        ('V29249'),('V29250'),('V41893'),('V16670'),('V42190'),('V2S033'),('V42648'),
        ('V04101'),('V42962'),('V2S292'),('V2S417'),('V18233'),('V18239'),('V18272'),
        ('V18302'),('V18585'),('V29382'),('V18601'),('V17012'),('V17354'),('V17795'),
        ('V04324'),('V18079'),('V18132'),('V18379'),('V18412'),('V18468'),('V43726'),
        ('V43898'),('V18769'),('V19145'),('V44613'),('V31664'),('V31687'),('V44943'),
        ('V45169'),('V20074'),('V45211'),('V20322'),('V20597'),('V20634'),('V44739'),
        ('V44747'),('V31736'),('V20193'),('V45561'),('V45839'),('V33167'),('V46116'),
        ('V46259'),('V21367'),('V21371'),('V21452'),('V08177'),('V22222'),('V08787'),
        ('V34688'),('V47824'),('V35001'),('V09553'),('V09562'),('V21318'),('V21498'),
        ('V33832'),('V47091'),('V47601'),('V35575'),('V09843'),('V35824'),('V35870'),
        ('V36036'),('V36046'),('V36625'),('V36656'),('V36657'),('V49500'),('V24565'),
        ('V36983'),('V48370'),('V24663'),('V24008'),('V24323'),('V49897'),('V11290'),
        ('V35982'),('V10426')
    ) v(Ecode)
)
SELECT
    e.EmployeeId,
    e.Ecode,
    e.ShiftID            AS OldShiftID,
    s.ShiftName          AS OldShiftName,
    SYSUTCDATETIME()     AS BackedUpOn
INTO dbo.tblEmployee_ShiftId_Backup_20260515
FROM Ecodes ec
INNER JOIN dbo.tblEmployee e ON e.Ecode = ec.Ecode
LEFT JOIN dbo.tblShiftMaster s ON s.ShiftID = e.ShiftID;

DECLARE @BackedUp INT = (SELECT COUNT(*) FROM dbo.tblEmployee_ShiftId_Backup_20260515);
PRINT CONCAT('>> Backup rows: ', @BackedUp);
GO

DECLARE @NewShiftId    INT          = 1;
DECLARE @EffectiveFrom DATE         = '2026-05-01';
DECLARE @AssignedBy    NVARCHAR(50) = N'SuperAdmin-Bulk20260515';
DECLARE @Remarks       NVARCHAR(200)= N'all 114 employees to general shift effective from 01/05/2026';
DECLARE @TotalProcessed INT = 0, @Skipped INT = 0;

BEGIN TRY
    BEGIN TRAN BulkShift;

    -- -----------------------------------------------------------------------
    -- 2) Loop the 114 employees, call usp_AssignEmployeeShift per row
    -- -----------------------------------------------------------------------
    DECLARE @EmployeeId BIGINT, @Ecode VARCHAR(50);

    DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
        SELECT EmployeeId, Ecode FROM dbo.tblEmployee_ShiftId_Backup_20260515 ORDER BY Ecode;

    OPEN cur;
    FETCH NEXT FROM cur INTO @EmployeeId, @Ecode;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        EXEC dbo.usp_AssignEmployeeShift
            @EmployeeId    = @EmployeeId,
            @ShiftId       = @NewShiftId,
            @EffectiveFrom = @EffectiveFrom,
            @AssignedBy    = @AssignedBy,
            @Remarks       = @Remarks;

        SET @TotalProcessed = @TotalProcessed + 1;
        FETCH NEXT FROM cur INTO @EmployeeId, @Ecode;
    END

    CLOSE cur;
    DEALLOCATE cur;

    PRINT CONCAT('>> usp_AssignEmployeeShift called for ', @TotalProcessed, ' employees');

    COMMIT TRAN BulkShift;
    PRINT '>> COMMITTED';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN BulkShift;
    PRINT CONCAT('>> ROLLED BACK: ', ERROR_MESSAGE());
    THROW;
END CATCH;
GO

-- ---------------------------------------------------------------------------
-- 3) Verification
-- ---------------------------------------------------------------------------
PRINT '>> Post-state by ShiftName:';
SELECT s.ShiftName, COUNT(*) AS EmpCount
FROM dbo.tblEmployee_ShiftId_Backup_20260515 b
INNER JOIN dbo.tblEmployee e ON e.EmployeeId = b.EmployeeId
LEFT JOIN dbo.tblShiftMaster s ON s.ShiftID = e.ShiftID
GROUP BY s.ShiftName
ORDER BY EmpCount DESC;
GO

PRINT '>> Sample EmployeeShiftHistory rows just inserted:';
SELECT TOP 5 h.EmployeeId, e.Ecode, h.ShiftId, h.EffectiveFrom, h.EffectiveTo, h.AssignedBy, h.AppliedOn, h.Remarks
FROM dbo.EmployeeShiftHistory h
INNER JOIN dbo.tblEmployee_ShiftId_Backup_20260515 b ON b.EmployeeId = h.EmployeeId
INNER JOIN dbo.tblEmployee e ON e.EmployeeId = h.EmployeeId
WHERE h.AssignedBy = N'SuperAdmin-Bulk20260515'
ORDER BY h.EmployeeId;
GO

PRINT '>> Done';
GO
