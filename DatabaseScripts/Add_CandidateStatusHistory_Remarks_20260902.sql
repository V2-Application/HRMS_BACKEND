/*==============================================================================
  ADD Remarks to CandidateStatus_History
  Date : 2026-09-02

  WHY
    The "Reopen" action on the Rejected tab of the Applicant list makes remarks
    MANDATORY in the UI:

        if (!reopenRemarks.trim()) { message.error('Remarks are mandatory'); return }

    ReopenCandidateDto carries them to the API, but ReopenCandidateAsync never
    reads dto.Remarks and CandidateStatus_History has no column to hold them.
    Every reopen reason typed so far has been discarded, so the audit trail
    looks complete and is not.

  WHAT THIS DOES
    Adds one nullable column. Additive and safe:
      - nullable, so existing rows stay valid and no backfill is needed
      - no data is read, moved, or deleted
      - re-runnable; does nothing if the column already exists

  ROLLBACK
    ALTER TABLE dbo.CandidateStatus_History DROP COLUMN Remarks;
    (only safe while no deployed build writes to it)

  RUN ORDER
    Dev first, verify the Reopen flow end to end, then prod alongside the
    matching API build.
==============================================================================*/

SET NOCOUNT ON;

IF COL_LENGTH('dbo.CandidateStatus_History', 'Remarks') IS NULL
BEGIN
    ALTER TABLE dbo.CandidateStatus_History
        ADD Remarks NVARCHAR(500) NULL;
    PRINT 'Added column CandidateStatus_History.Remarks NVARCHAR(500) NULL';
END
ELSE
BEGIN
    PRINT 'Column CandidateStatus_History.Remarks already exists - nothing to do';
END
GO

-- verify
SELECT c.name, TYPE_NAME(c.user_type_id) AS DataType, c.max_length, c.is_nullable
FROM sys.columns c
WHERE c.object_id = OBJECT_ID('dbo.CandidateStatus_History')
ORDER BY c.column_id;
GO
