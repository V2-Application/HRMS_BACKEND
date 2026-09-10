/* =============================================================================
   Shift Master: effective date range
   -----------------------------------------------------------------------------
   Adds EffectiveFrom / EffectiveTo to dbo.tblShiftMaster so the Shift Master
   page carries the same effective-date pair the Emp Shift Alignment page
   already has (EmployeeShiftHistory.EffectiveFrom / EffectiveTo).

   Same shape as that table on purpose:
       EffectiveFrom  date  -- the day the shift timing starts applying
       EffectiveTo    date  -- NULL = open ended

   Both are NULLable here, unlike EmployeeShiftHistory.EffectiveFrom which is
   NOT NULL. Reason: existing shift rows have no effective date and this script
   deliberately does NOT invent one for them -- no existing row is updated. The
   API requires EffectiveFrom whenever a shift is created or edited, so rows
   fill in as they are touched, and a NULL simply reads as "always effective".

   ADD COLUMN only. Nothing is dropped, deleted or truncated. Re-runnable.
   ============================================================================= */

SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.tblShiftMaster', N'U') IS NULL
BEGIN
    RAISERROR('dbo.tblShiftMaster does not exist on this database - aborting.', 16, 1);
    RETURN;
END

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'dbo.tblShiftMaster') AND name = N'EffectiveFrom')
BEGIN
    ALTER TABLE dbo.tblShiftMaster ADD EffectiveFrom date NULL;
    PRINT 'Added dbo.tblShiftMaster.EffectiveFrom';
END
ELSE
    PRINT 'dbo.tblShiftMaster.EffectiveFrom already present - skipped';

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'dbo.tblShiftMaster') AND name = N'EffectiveTo')
BEGIN
    ALTER TABLE dbo.tblShiftMaster ADD EffectiveTo date NULL;
    PRINT 'Added dbo.tblShiftMaster.EffectiveTo';
END
ELSE
    PRINT 'dbo.tblShiftMaster.EffectiveTo already present - skipped';

GO

-- Verification
SELECT c.name AS ColumnName, t.name AS DataType, c.is_nullable AS IsNullable
FROM sys.columns c
JOIN sys.types  t ON t.user_type_id = c.user_type_id
WHERE c.object_id = OBJECT_ID(N'dbo.tblShiftMaster')
  AND c.name IN (N'EffectiveFrom', N'EffectiveTo');

SELECT ShiftID, ShiftName, StartTime, EndTime, EffectiveFrom, EffectiveTo, IsActive
FROM dbo.tblShiftMaster
ORDER BY ShiftID;
