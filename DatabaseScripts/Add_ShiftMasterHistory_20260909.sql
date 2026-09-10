/* =============================================================================
   Shift Master: timing history
   -----------------------------------------------------------------------------
   Gives the Shift Master page the same "what did this look like before, and
   what is coming next" view that Emp Shift Alignment gets from
   dbo.EmployeeShiftHistory -- except the subject is a shift, not an employee.

   dbo.tblShiftMaster keeps holding the CURRENT timing (nothing about how it is
   read changes, so no other screen is affected). Every create/edit also writes
   one row here, so a past timing is never lost.

   Deliberate choices:
     * No FOREIGN KEY to tblShiftMaster. Shift Master already lets you delete a
       shift that no employee/candidate uses; an FK would start failing those
       deletes. The ShiftID is indexed instead, and ShiftName is stored on the
       row so history stays readable even after a rename or delete.
     * EffectiveFrom/EffectiveTo are NULLable for the same reason as on
       tblShiftMaster: the 12 rows that predate this feature genuinely have no
       effective date, and NULL EffectiveTo means open ended.

   CREATE + INSERT only. Nothing is dropped, deleted, truncated or updated.
   Re-runnable.
   ============================================================================= */

SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.tblShiftMaster', N'U') IS NULL
BEGIN
    RAISERROR('dbo.tblShiftMaster does not exist on this database - aborting.', 16, 1);
    RETURN;
END

IF OBJECT_ID(N'dbo.tblShiftMasterHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblShiftMasterHistory
    (
        ShiftHistoryId bigint        IDENTITY(1,1) NOT NULL,
        ShiftID        int           NOT NULL,
        ShiftName      varchar(50)   NOT NULL,
        StartTime      time(7)       NOT NULL,
        EndTime        time(7)       NOT NULL,
        EffectiveFrom  date          NULL,
        EffectiveTo    date          NULL,
        Remarks        nvarchar(200) NULL,
        ChangeType     varchar(20)   NOT NULL,   -- Baseline | Created | Updated
        ChangedBy      varchar(50)   NOT NULL,
        ChangedOn      datetime      NOT NULL
            CONSTRAINT DF_tblShiftMasterHistory_ChangedOn DEFAULT (GETDATE()),
        CONSTRAINT PK_tblShiftMasterHistory PRIMARY KEY CLUSTERED (ShiftHistoryId)
    );

    CREATE NONCLUSTERED INDEX IX_tblShiftMasterHistory_ShiftID
        ON dbo.tblShiftMasterHistory (ShiftID, EffectiveFrom, ShiftHistoryId);

    PRINT 'Created dbo.tblShiftMasterHistory';
END
ELSE
    PRINT 'dbo.tblShiftMasterHistory already present - skipped';

GO

/* -----------------------------------------------------------------------------
   Baseline: one row per existing shift, recording the timing it has right now,
   so the history panel is not empty on day one. Only inserts for shifts that
   have no history row yet, so re-running adds nothing.
   ----------------------------------------------------------------------------- */
INSERT INTO dbo.tblShiftMasterHistory
    (ShiftID, ShiftName, StartTime, EndTime, EffectiveFrom, EffectiveTo, Remarks, ChangeType, ChangedBy, ChangedOn)
SELECT  s.ShiftID,
        s.ShiftName,
        s.StartTime,
        s.EndTime,
        s.EffectiveFrom,
        s.EffectiveTo,
        N'Timing on record when history tracking was switched on',
        'Baseline',
        ISNULL(s.LastUpdatedBy, s.CreatedBy),
        ISNULL(s.LastUpdatedOn, s.CreatedOn)
FROM dbo.tblShiftMaster s
WHERE NOT EXISTS (SELECT 1 FROM dbo.tblShiftMasterHistory h WHERE h.ShiftID = s.ShiftID);

PRINT CONCAT('Baseline rows inserted: ', @@ROWCOUNT);

GO

-- Verification
SELECT h.ShiftHistoryId, h.ShiftID, h.ShiftName, h.StartTime, h.EndTime,
       h.EffectiveFrom, h.EffectiveTo, h.ChangeType, h.ChangedBy, h.ChangedOn
FROM dbo.tblShiftMasterHistory h
ORDER BY h.ShiftID, h.ShiftHistoryId;
