-- Resolve AssignedBy (which is sometimes an Ecode and sometimes an EmployeeId
-- depending on the upload path) into AssignedByEcode + AssignedByName so the
-- UI can display a human-readable value instead of a raw ID.
-- Idempotent CREATE OR ALTER.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO

CREATE OR ALTER PROCEDURE dbo.usp_GetEmployeeShiftAndHistory
(
    @EmployeeId INT = NULL,
    @Ecode      NVARCHAR(20) = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    IF (@EmployeeId IS NULL AND (@Ecode IS NULL OR LTRIM(RTRIM(@Ecode)) = ''))
    BEGIN
        RAISERROR('Pass @EmployeeId or @Ecode.', 16, 1);
        RETURN;
    END;

    DECLARE @EmpId INT;

    SELECT TOP (1) @EmpId = e.EmployeeId
    FROM dbo.tblEmployee e
    WHERE (@EmployeeId IS NOT NULL AND e.EmployeeId = @EmployeeId)
       OR (@EmployeeId IS NULL AND e.Ecode = @Ecode);

    IF (@EmpId IS NULL)
    BEGIN
        RAISERROR('Employee not found.', 16, 1);
        RETURN;
    END;

    --------------------------------------------------------------------
    -- Result Set #1: Employee + Reporting Head + Current Shift
    --------------------------------------------------------------------
    SELECT
        e.EmployeeId,
        e.Ecode,
        e.FirstName,
        e.LastName,
        e.[FULL NAME]      AS FullName,
        e.ReportHeadEcode,

        rh.EmployeeId      AS ReportHeadEmployeeId,
        rh.Ecode           AS ReportHeadEcode,
        rh.[FULL NAME]     AS ReportHeadFullName,

        e.ShiftID          AS CurrentShiftId,
        s.*
    FROM dbo.tblEmployee e
    LEFT JOIN dbo.tblEmployee rh
           ON rh.Ecode = e.ReportHeadEcode
    LEFT JOIN dbo.tblShiftMaster s
           ON s.ShiftID = e.ShiftID
    WHERE e.EmployeeId = @EmpId;

    --------------------------------------------------------------------
    -- Result Set #2: Shift History + Shift Details + resolved assigner
    --------------------------------------------------------------------
    SELECT
        h.HistoryId,
        h.EmployeeId,
        h.ShiftId,
        h.EffectiveFrom,
        h.EffectiveTo,
        h.AssignedOn,
        h.AssignedBy,
        ab.Ecode           AS AssignedByEcode,
        ab.[FULL NAME]     AS AssignedByName,
        h.Remarks,
        h.AppliedOn,

        CASE
            WHEN h.EffectiveFrom > CAST(GETDATE() AS DATE) THEN 'Future'
            WHEN h.EffectiveTo IS NULL OR h.EffectiveTo >= CAST(GETDATE() AS DATE) THEN 'Current'
            ELSE 'Past'
        END AS ShiftStatus,

        s.*
    FROM dbo.EmployeeShiftHistory h
    LEFT JOIN dbo.tblShiftMaster s
           ON s.ShiftID = h.ShiftId
    LEFT JOIN dbo.tblEmployee ab
           ON ab.Ecode = h.AssignedBy
           OR (TRY_CAST(h.AssignedBy AS BIGINT) IS NOT NULL
               AND ab.EmployeeId = TRY_CAST(h.AssignedBy AS BIGINT))
    WHERE h.EmployeeId = @EmpId
    ORDER BY h.EffectiveFrom DESC, h.HistoryId DESC;
END;
GO
