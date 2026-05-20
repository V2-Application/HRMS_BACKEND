-- =============================================================================
-- Update dbo.usp_Incentive: add optional @CreatedByFilter parameter for LIST.
--
-- Why:   The Incentive "My Requests" page sends mine=true so a logged-in user
--        sees only the requests they themselves created. Previously the LIST
--        action returned every incentive in the system regardless of caller.
--
-- What:  Adds a new optional parameter @CreatedByFilter (VARCHAR(50) = NULL).
--        When non-NULL/non-empty, the LIST action restricts rows to
--        i.CreatedBy = @CreatedByFilter. All other behavior is unchanged.
--
-- Safe to re-run (CREATE OR ALTER). UPSERT / GET behavior is not modified.
-- =============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Incentive
    @Action             NVARCHAR(20) = N'UPSERT',      -- UPSERT | GET | LIST

    -- Common inputs (ALL NULLABLE)
    @IncentiveId        BIGINT = NULL,
    @Ecode              VARCHAR(50) = NULL,
    @Month              DATE = NULL,                   -- first day of month
    @Amount             DECIMAL(12,2) = NULL,
    @Remarks            NVARCHAR(MAX) = NULL,
    @CreatedBy          VARCHAR(50) = NULL,

    -- Stage statuses + remarks (caller sets ONLY these)
    @CmdStatusId        INT = NULL,
    @HrStatusId         INT = NULL,
    @CmdRemarks         NVARCHAR(MAX) = NULL,
    @HrRemarks          NVARCHAR(MAX) = NULL,

    -- Attachments TVP (metadata only)
    @Attachments        dbo.tt_IncentiveAttachment READONLY,
    @ReplaceAttachments BIT = 0,

    -- LIST paging (optional)
    @PageNumber         INT = 1,
    @PageSize           INT = 10,
    @SearchTerm         NVARCHAR(100) = NULL,

    -- LIST: restrict to rows whose CreatedBy matches this value
    -- (used by "My Requests" so a user sees only their own incentives).
    @CreatedByFilter    VARCHAR(50) = NULL,

    -- Outputs for LIST
    @TotalCount         BIGINT = NULL OUTPUT,
    @CurrentPageNumber  INT    = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    -- Resolve core statuses
    DECLARE @ApprovedId INT = (SELECT TOP 1 StatusId FROM dbo.tblStatus WHERE StatusName='Approved');
    DECLARE @RejectedId INT = (SELECT TOP 1 StatusId FROM dbo.tblStatus WHERE StatusName='Rejected');
    DECLARE @PendingId  INT = (SELECT TOP 1 StatusId FROM dbo.tblStatus WHERE StatusName='Pending');

    /* ========================= GET ========================= */
    IF @Action = 'GET'
    BEGIN
        SELECT  i.IncentiveId, i.Ecode, i.[Month], i.Amount, i.Remarks, i.CreatedBy,
                i.StatusId, s.StatusName AS OverallStatusName,
                i.CmdStatusId, sc.StatusName AS CmdStatusName,
                i.HrStatusId,  sh.StatusName AS HrStatusName,
                i.CmdRemarks,  i.HrRemarks,
                i.CreatedAt,   i.UpdatedAt
        FROM dbo.tblIncentive i
        LEFT JOIN dbo.tblStatus s  ON s.StatusId  = i.StatusId
        LEFT JOIN dbo.tblStatus sc ON sc.StatusId = i.CmdStatusId
        LEFT JOIN dbo.tblStatus sh ON sh.StatusId = i.HrStatusId
        WHERE i.IncentiveId = @IncentiveId;

        SELECT AttachmentId, IncentiveId, FileName, FileType, FileSizeBytes, FilePath, UploadedAt
        FROM dbo.tblIncentiveAttachment
        WHERE IncentiveId = @IncentiveId
        ORDER BY AttachmentId;

        RETURN;
    END

    /* ========================= LIST ========================= */
    IF @Action = 'LIST'
    BEGIN
        SET @SearchTerm      = NULLIF(@SearchTerm, N'');
        SET @CreatedByFilter = NULLIF(@CreatedByFilter, '');

        IF OBJECT_ID('tempdb..#Base') IS NOT NULL DROP TABLE #Base;

        SELECT  i.IncentiveId, i.Ecode, i.[Month], i.Amount, i.Remarks, i.CreatedBy,
                i.StatusId, s.StatusName AS OverallStatusName,
                i.CmdStatusId, sc.StatusName AS CmdStatusName,
                i.HrStatusId,  sh.StatusName AS HrStatusName,
                i.CmdRemarks,  i.HrRemarks,
                i.CreatedAt,   i.UpdatedAt
        INTO #Base
        FROM dbo.tblIncentive i
        LEFT JOIN dbo.tblStatus s  ON s.StatusId  = i.StatusId
        LEFT JOIN dbo.tblStatus sc ON sc.StatusId = i.CmdStatusId
        LEFT JOIN dbo.tblStatus sh ON sh.StatusId = i.HrStatusId
        WHERE (@CreatedByFilter IS NULL OR i.CreatedBy = @CreatedByFilter)
          AND (@SearchTerm IS NULL
               OR i.Ecode            LIKE '%' + @SearchTerm + '%'
               OR i.CreatedBy        LIKE '%' + @SearchTerm + '%'
               OR s.StatusName       LIKE '%' + @SearchTerm + '%'
               OR sc.StatusName      LIKE '%' + @SearchTerm + '%'
               OR sh.StatusName      LIKE '%' + @SearchTerm + '%');

        SELECT @TotalCount = COUNT(*) FROM #Base;

        IF @PageNumber IS NULL OR @PageNumber < 1 SET @PageNumber = 1;
        IF @PageSize   IS NULL OR @PageSize   < 1 SET @PageSize   = 10;

        ;WITH Paged AS (
            SELECT *, ROW_NUMBER() OVER (ORDER BY IncentiveId DESC) AS rn
            FROM #Base
        )
        SELECT IncentiveId, Ecode, [Month], Amount, Remarks, CreatedBy,
               StatusId, OverallStatusName,
               CmdStatusId, CmdStatusName,
               HrStatusId,  HrStatusName,
               CmdRemarks,  HrRemarks,
               CreatedAt,   UpdatedAt
        FROM Paged
        WHERE rn BETWEEN ((@PageNumber-1)*@PageSize + 1) AND (@PageNumber*@PageSize)
        ORDER BY rn;

        SET @CurrentPageNumber = @PageNumber;
        RETURN;
    END

    /* ========================= UPSERT ========================= */
    BEGIN TRY
        BEGIN TRAN;

        -- Upsert-by-(Ecode,Month) if id not supplied
        IF @IncentiveId IS NULL AND @Ecode IS NOT NULL AND @Month IS NOT NULL
        BEGIN
            SELECT @IncentiveId = IncentiveId
            FROM dbo.tblIncentive
            WHERE Ecode = @Ecode AND [Month] = @Month;
        END

        -- CREATE
        IF @IncentiveId IS NULL
        BEGIN
            IF @Ecode IS NULL OR @Month IS NULL OR @Amount IS NULL OR @CreatedBy IS NULL
                THROW 51001, 'For create, @Ecode, @Month, @Amount, @CreatedBy are required.', 1;

            DECLARE @CmdStage INT = ISNULL(@CmdStatusId, @PendingId);
            DECLARE @HrStage  INT = ISNULL(@HrStatusId,  @PendingId);

            DECLARE @Overall INT =
                CASE
                    WHEN @CmdStage = @RejectedId OR @HrStage = @RejectedId THEN @RejectedId
                    WHEN @CmdStage = @ApprovedId AND @HrStage = @ApprovedId THEN @ApprovedId
                    ELSE @PendingId
                END;

            INSERT INTO dbo.tblIncentive
                (Ecode, [Month], Amount, Remarks, CreatedBy,
                 CmdStatusId, HrStatusId, CmdRemarks, HrRemarks,
                 StatusId)
            VALUES
                (@Ecode, @Month, @Amount, @Remarks, @CreatedBy,
                 @CmdStage, @HrStage, @CmdRemarks, @HrRemarks,
                 @Overall);

            SET @IncentiveId = SCOPE_IDENTITY();
        END
        ELSE
        -- UPDATE
        BEGIN
            UPDATE i
            SET Ecode       = COALESCE(@Ecode,      i.Ecode),
                [Month]     = COALESCE(@Month,      i.[Month]),
                Amount      = COALESCE(@Amount,     i.Amount),
                Remarks     = COALESCE(@Remarks,    i.Remarks),
                CreatedBy   = COALESCE(@CreatedBy,  i.CreatedBy),
                CmdStatusId = COALESCE(@CmdStatusId,i.CmdStatusId),
                HrStatusId  = COALESCE(@HrStatusId, i.HrStatusId),
                CmdRemarks  = COALESCE(@CmdRemarks, i.CmdRemarks),
                HrRemarks   = COALESCE(@HrRemarks,  i.HrRemarks),
                UpdatedAt   = SYSUTCDATETIME()
            FROM dbo.tblIncentive i
            WHERE i.IncentiveId = @IncentiveId;

            IF @@ROWCOUNT = 0
                THROW 51002, 'Incentive not found.', 1;

            DECLARE @Cmd2 INT, @Hr2 INT;
            SELECT @Cmd2 = CmdStatusId, @Hr2 = HrStatusId
            FROM dbo.tblIncentive WHERE IncentiveId = @IncentiveId;

            DECLARE @Overall2 INT =
                CASE
                    WHEN @Cmd2 = @RejectedId OR @Hr2 = @RejectedId THEN @RejectedId
                    WHEN @Cmd2 = @ApprovedId AND @Hr2 = @ApprovedId THEN @ApprovedId
                    ELSE @PendingId
                END;

            UPDATE dbo.tblIncentive
            SET StatusId = @Overall2,
                UpdatedAt = SYSUTCDATETIME()
            WHERE IncentiveId = @IncentiveId;
        END

        -- Attachments (optional TVP)
        IF EXISTS (SELECT 1 FROM @Attachments)
        BEGIN
            IF @ReplaceAttachments = 1
                DELETE FROM dbo.tblIncentiveAttachment WHERE IncentiveId = @IncentiveId;

            INSERT INTO dbo.tblIncentiveAttachment (IncentiveId, FileName, FileType, FileSizeBytes, FilePath)
            SELECT @IncentiveId, FileName, FileType, FileSizeBytes, FilePath
            FROM @Attachments;
        END

        COMMIT;

        -- Return like GET
        SELECT  i.IncentiveId, i.Ecode, i.[Month], i.Amount, i.Remarks, i.CreatedBy,
                i.StatusId, s.StatusName AS OverallStatusName,
                i.CmdStatusId, sc.StatusName AS CmdStatusName,
                i.HrStatusId,  sh.StatusName AS HrStatusName,
                i.CmdRemarks,  i.HrRemarks,
                i.CreatedAt,   i.UpdatedAt
        FROM dbo.tblIncentive i
        LEFT JOIN dbo.tblStatus s  ON s.StatusId  = i.StatusId
        LEFT JOIN dbo.tblStatus sc ON sc.StatusId = i.CmdStatusId
        LEFT JOIN dbo.tblStatus sh ON sh.StatusId = i.HrStatusId
        WHERE i.IncentiveId = @IncentiveId;

    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK;
        THROW;
    END CATCH
END
GO
