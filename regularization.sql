REGULARIZATION 


REPORTING MANAGER APPROVAL ONLY NOW


NOW ADD LP AFTER APPROVAL REPORTING MANAGER 



USE [HRMS_Dev];
GO

/* --- 1a. Add HR approval columns --- */
ALTER TABLE [dbo].[tblAttendanceRegularizationRequest]
ADD [HrApprovalStatusId] INT NULL
  CONSTRAINT [DF_tblARR_HrApprovalStatusId] DEFAULT(4),                 -- Pending
    [HrApproverId]        BIGINT NULL,
    [HrApprovalOn]        DATETIME NULL,
    [HrRemarks]           NVARCHAR(500) NULL;

/* --- 1b. Add LP approval columns --- */
ALTER TABLE [dbo].[tblAttendanceRegularizationRequest]
ADD [LpApprovalStatusId] INT NULL
  CONSTRAINT [DF_tblARR_LpApprovalStatusId] DEFAULT(4),                 -- Pending
    [LpApproverId]        BIGINT NULL,
    [LpApprovalOn]        DATETIME NULL,
    [LpRemarks]           NVARCHAR(500) NULL;

/* --- 1c. FKs to tblStatus for both approval statuses --- */
ALTER TABLE [dbo].[tblAttendanceRegularizationRequest]  WITH CHECK
ADD CONSTRAINT [FK_tblARR_HrApprovalStatus]
    FOREIGN KEY([HrApprovalStatusId]) REFERENCES [dbo].[tblStatus]([StatusId]);

ALTER TABLE [dbo].[tblAttendanceRegularizationRequest] CHECK CONSTRAINT [FK_tblARR_HrApprovalStatus];

ALTER TABLE [dbo].[tblAttendanceRegularizationRequest]  WITH CHECK
ADD CONSTRAINT [FK_tblARR_LpApprovalStatus]
    FOREIGN KEY([LpApprovalStatusId]) REFERENCES [dbo].[tblStatus]([StatusId]);

ALTER TABLE [dbo].[tblAttendanceRegularizationRequest] CHECK CONSTRAINT [FK_tblARR_LpApprovalStatus];

/* --- 1d. (Optional) FKs to tblEmployee for approver identities --- */
ALTER TABLE [dbo].[tblAttendanceRegularizationRequest]  WITH CHECK
ADD CONSTRAINT [FK_tblARR_HrApprover]
    FOREIGN KEY([HrApproverId]) REFERENCES [dbo].[tblEmployee]([EmployeeId]);

ALTER TABLE [dbo].[tblAttendanceRegularizationRequest] CHECK CONSTRAINT [FK_tblARR_HrApprover];

ALTER TABLE [dbo].[tblAttendanceRegularizationRequest]  WITH CHECK
ADD CONSTRAINT [FK_tblARR_LpApprover]
    FOREIGN KEY([LpApproverId]) REFERENCES [dbo].[tblEmployee]([EmployeeId]);

ALTER TABLE [dbo].[tblAttendanceRegularizationRequest] CHECK CONSTRAINT [FK_tblARR_LpApprover];

/* --- 1e. Backfill existing rows to Pending when null --- */
UPDATE R
SET
  HrApprovalStatusId = ISNULL(HrApprovalStatusId, 4),
  LpApprovalStatusId = ISNULL(LpApprovalStatusId, 4)
FROM [dbo].[tblAttendanceRegularizationRequest] R;

/* --- 1f. Helpful indexes --- */
CREATE INDEX [IX_tblARR_HrApprovalStatusId]
  ON [dbo].[tblAttendanceRegularizationRequest]([HrApprovalStatusId]);

CREATE INDEX [IX_tblARR_LpApprovalStatusId]
  ON [dbo].[tblAttendanceRegularizationRequest]([LpApprovalStatusId]);

CREATE INDEX [IX_tblARR_HrApproverId]
  ON [dbo].[tblAttendanceRegularizationRequest]([HrApproverId]);

CREATE INDEX [IX_tblARR_LpApproverId]
  ON [dbo].[tblAttendanceRegularizationRequest]([LpApproverId]);
GO



  "DefaultConnection": "Data Source=192.168.151.28\\hrms;Initial Catalog=HRMS_Dev;User ID=sa_hrms;Password=CIHTY5pBmRRwjAw;Trust Server Certificate=True" //  prod
  
  
  USE [HRMS_Dev];
GO

/* === A1) Rename columns: Hr* -> Manager* === */
IF COL_LENGTH('dbo.tblAttendanceRegularizationRequest', 'HrApprovalStatusId') IS NOT NULL
    EXEC sp_rename 'dbo.tblAttendanceRegularizationRequest.HrApprovalStatusId',
                   'ManagerApprovalStatusId', 'COLUMN';
IF COL_LENGTH('dbo.tblAttendanceRegularizationRequest', 'HrApproverId') IS NOT NULL
    EXEC sp_rename 'dbo.tblAttendanceRegularizationRequest.HrApproverId',
                   'ManagerApproverId', 'COLUMN';
IF COL_LENGTH('dbo.tblAttendanceRegularizationRequest', 'HrApprovalOn') IS NOT NULL
    EXEC sp_rename 'dbo.tblAttendanceRegularizationRequest.HrApprovalOn',
                   'ManagerApprovalOn', 'COLUMN';
IF COL_LENGTH('dbo.tblAttendanceRegularizationRequest', 'HrRemarks') IS NOT NULL
    EXEC sp_rename 'dbo.tblAttendanceRegularizationRequest.HrRemarks',
                   'ManagerRemarks', 'COLUMN';
GO

/* === A2) Rename default constraint (if you created DF_tblARR_HrApprovalStatusId) === */
IF OBJECT_ID('DF_tblARR_HrApprovalStatusId', 'D') IS NOT NULL
    EXEC sp_rename 'DF_tblARR_HrApprovalStatusId', 'DF_tblARR_ManagerApprovalStatusId', 'OBJECT';
GO

/* === A3) Rename foreign keys (only if those exact names exist) === */
IF OBJECT_ID('FK_tblARR_HrApprovalStatus', 'F') IS NOT NULL
    EXEC sp_rename 'FK_tblARR_HrApprovalStatus', 'FK_tblARR_ManagerApprovalStatus', 'OBJECT';
IF OBJECT_ID('FK_tblARR_HrApprover', 'F') IS NOT NULL
    EXEC sp_rename 'FK_tblARR_HrApprover', 'FK_tblARR_ManagerApprover', 'OBJECT';
GO

/* === A4) Rename indexes (if you created them) === */
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_tblARR_HrApprovalStatusId'
          AND object_id = OBJECT_ID('dbo.tblAttendanceRegularizationRequest'))
    EXEC sp_rename N'dbo.tblAttendanceRegularizationRequest.IX_tblARR_HrApprovalStatusId',
                   N'IX_tblARR_ManagerApprovalStatusId', N'INDEX';

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_tblARR_HrApproverId'
          AND object_id = OBJECT_ID('dbo.tblAttendanceRegularizationRequest'))
    EXEC sp_rename N'dbo.tblAttendanceRegularizationRequest.IX_tblARR_HrApproverId',
                   N'IX_tblARR_ManagerApproverId', N'INDEX';
GO

/* === A5) Sanity: ensure defaults are Pending(4) after rename === */
/* If default constraint didn't exist earlier, add it now */
IF COL_LENGTH('dbo.tblAttendanceRegularizationRequest', 'ManagerApprovalStatusId') IS NOT NULL
AND NOT EXISTS (
    SELECT 1
    FROM sys.default_constraints dc
    JOIN sys.columns c ON c.default_object_id = dc.object_id
    WHERE dc.parent_object_id = OBJECT_ID('dbo.tblAttendanceRegularizationRequest')
      AND c.name = 'ManagerApprovalStatusId'
)
BEGIN
    ALTER TABLE dbo.tblAttendanceRegularizationRequest
      ADD CONSTRAINT DF_tblARR_ManagerApprovalStatusId DEFAULT(4) FOR ManagerApprovalStatusId;
END

/* LP side (in case not present) */
IF COL_LENGTH('dbo.tblAttendanceRegularizationRequest', 'LpApprovalStatusId') IS NOT NULL
AND NOT EXISTS (
    SELECT 1
    FROM sys.default_constraints dc
    JOIN sys.columns c ON c.default_object_id = dc.object_id
    WHERE dc.parent_object_id = OBJECT_ID('dbo.tblAttendanceRegularizationRequest')
      AND c.name = 'LpApprovalStatusId'
)
BEGIN
    ALTER TABLE dbo.tblAttendanceRegularizationRequest
      ADD CONSTRAINT DF_tblARR_LpApprovalStatusId DEFAULT(4) FOR LpApprovalStatusId;
END
GO

/* === A6) Backfill nulls to Pending(4) just in case === */
UPDATE r
SET ManagerApprovalStatusId = ISNULL(ManagerApprovalStatusId, 4),
    LpApprovalStatusId      = ISNULL(LpApprovalStatusId, 4)
FROM dbo.tblAttendanceRegularizationRequest r;
GO




