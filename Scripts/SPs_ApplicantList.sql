-- =============================================================================
-- Category: Applicant
-- Source:   dev DB (192.168.151.27\KARMA / HRMS)
-- Objects rewritten as CREATE OR ALTER for safe re-run on production.
-- =============================================================================
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- -----------------------------------------------------------------------------
-- dbo.sp_GetApplicantListNew01
-- Adds StatusName column (joined from tblStatus) so the exported Excel shows
-- the human-readable applicant status. The existing StatusId column is
-- preserved for backward compatibility with consumers that already use it.
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_GetApplicantListNew01
(
    @PageNumber INT = 1,
    @PageSize INT = 10,
    @StatusId INT = 0,
    @SearchTerm NVARCHAR(200) = NULL,
    @RoleId INT = NULL,
    @EmployeeId INT = NULL
)
AS
BEGIN
    SET NOCOUNT ON;
    SET ARITHABORT ON;

    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;
    SET @SearchTerm = LTRIM(RTRIM(ISNULL(@SearchTerm, '')));

    ---------------------------------------------------------
    -- HEADER
    ---------------------------------------------------------
    SELECT
        COUNT(*) AS TotalRecords,
        SUM(CASE WHEN StatusId = 4 THEN 1 ELSE 0 END) AS PendingCount
    FROM Candidate
    WHERE IsApplicant = 1
      AND IsActive = 1
      AND IsDeleted = 0
      AND (@StatusId = 0 OR StatusId = @StatusId);

    ---------------------------------------------------------
    -- PAGED RESULT
    ---------------------------------------------------------
    ;WITH BaseData AS
    (
        SELECT *
        FROM Candidate c
        WHERE
            c.IsApplicant = 1
            AND c.IsActive = 1
            AND c.IsDeleted = 0
            AND (@StatusId = 0 OR c.StatusId = @StatusId)
            AND (
                @SearchTerm = '' OR
                c.[FIRST NAME] LIKE '%' + @SearchTerm + '%' OR
                c.[MIDDLE NAME] LIKE '%' + @SearchTerm + '%' OR
                c.[LAST NAME] LIKE '%' + @SearchTerm + '%' OR
                c.[EMP CODE] LIKE '%' + @SearchTerm + '%' OR
                c.MOBILE LIKE '%' + @SearchTerm + '%'
            )
    )

    SELECT
        c.Id AS ID,
        c.[FIRST NAME] AS FirstName,
        c.[MIDDLE NAME] AS MiddleName,
        c.[LAST NAME] AS LastName,
        c.MOBILE AS Phone,
        c.[EMAIL ADDRESS] AS Email,
        CASE WHEN c.StatusId = 2 THEN 1 ELSE 0 END AS IsReopenAllowed,
        c.DESIGNATION AS Designation,
        c.DOB,
        c.StatusId,
        s.StatusName AS Status,
        d.DesignationName,
        CONCAT(l.STCode,'-',l.LocationName) AS LocationName,
        c.[POSITION HELD IN PREVIOUS COMPANY] AS PositionHeldInPreviousCompany,
        c.[EMP CODE] AS ApplicantCode,
        c.IsApplicant,

        docs.ResumeLink,
        docs.OfferLetterLink,

        ir.InterviewRounds,
        ir.Type,
        ir.CurrentRound,
        ir.LastInterviewDateTime,
        ir.LastScheduleId,
        ir.FinalResult,
        ir.IsStatus,

        c.IsActive,
        c.IsDeleted,
        cr.[FULL NAME] + ' (' + cr.Ecode + ')' AS CreatedBy,
        up.[FULL NAME] + ' (' + up.Ecode + ')' AS UpdatedBy,
        c.CreatedOn,
        c.UpdatedOn,
        c.CreatedOn AS DateOfApply,

        c.[WORK LOCATION],
        c.[APPLICANT CODE],
        c.[COMPANY 1],
        c.[COMPANY 2],
        c.[COMPANY 3],
        c.[In Hand Salary],
        c.[LAST CTC(ANNUAL)],

        e.TotalIndustryExperience_yrs,
        e.TotalRetailExperience_yrs,

        c.CurrentLocation,
        c.PreferredLocation,
        c.StateId,
        st.StateName,
        c.NoticePeriod

    FROM BaseData c

    LEFT JOIN tblDesignation d
        ON TRY_CAST(c.DESIGNATION AS INT) = d.DesignationId

    LEFT JOIN tblLocation l
        ON TRY_CAST(c.LOCATION AS INT) = l.LocationId

    LEFT JOIN tblExperience e
        ON e.CID = c.Id

    LEFT JOIN tblEmployee cr
        ON TRY_CAST(c.CreatedBy AS INT) = cr.EmployeeId

    LEFT JOIN tblEmployee up
        ON TRY_CAST(c.UpdatedBy AS INT) = up.EmployeeId

    LEFT JOIN StateMasterWithMinWages st
        ON c.StateId = st.Id

    LEFT JOIN tblStatus s
        ON s.StatusId = c.StatusId

    OUTER APPLY
    (
        SELECT
            MAX(CASE WHEN FileType='Resume' THEN FilePath END) AS ResumeLink,
            MAX(CASE WHEN FileType='OfferLetter' THEN FilePath END) AS OfferLetterLink
        FROM CanidateDocs
        WHERE CId = c.Id AND IsDeleted = 0
    ) docs

    OUTER APPLY
    (
        SELECT
            (
                SELECT r.RoundId,
                       r.ScheduleId,
                       s.InterviewLocation,
                       s.InterviewDateTime,
                       ISNULL(r.Status,'') AS Status
                FROM tblInterviewRounds r
                JOIN tblScheduleInterview s
                    ON r.ScheduleId = s.ScheduleId
                WHERE s.ApplicantId = c.Id
                  AND s.IsActive = 1
                  AND s.IsDeleted = 0
                FOR JSON PATH
            ) AS InterviewRounds,

            (SELECT TOP 1 s.InterviewLocation
             FROM tblScheduleInterview s
             WHERE s.ApplicantId = c.Id
             ORDER BY s.ScheduleId DESC) AS Type,

            (SELECT ISNULL(MAX(r.RoundId),0)
             FROM tblInterviewRounds r
             JOIN tblScheduleInterview s
                ON r.ScheduleId = s.ScheduleId
             WHERE s.ApplicantId = c.Id) AS CurrentRound,

            (SELECT TOP 1 CONVERT(VARCHAR(19),s.InterviewDateTime,120)
             FROM tblScheduleInterview s
             WHERE s.ApplicantId = c.Id
             ORDER BY s.InterviewDateTime DESC) AS LastInterviewDateTime,

            (SELECT TOP 1 s.ScheduleId
             FROM tblScheduleInterview s
             WHERE s.ApplicantId = c.Id
             ORDER BY s.ScheduleId DESC) AS LastScheduleId,

            '' AS FinalResult,
            0 AS IsStatus
    ) ir

    ORDER BY c.Id DESC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY

    OPTION (RECOMPILE);
END
GO
