-- =============================================
-- Attendance Count Approval System
-- Two Level Approval: CM (Cluster Manager) and RM (Regional Manager)
-- =============================================

-- Main Attendance Count Approval Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[tblAttendanceCountApproval]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[tblAttendanceCountApproval] (
        [AttendanceCountApprovalId] BIGINT PRIMARY KEY IDENTITY(1,1),
        [ECode] NVARCHAR(50) NOT NULL,
        [MonthYear] NVARCHAR(10) NOT NULL, -- Format: MMM-YY (e.g., Jan-25, Feb-25)
        [AttendanceCount] INT NOT NULL,
        [EmployeeRemarks] NVARCHAR(1000),
        
        -- CM (Cluster Manager) Approval
        -- NULL = Not Reviewed, 0 = Rejected, 1 = Approved
        [IsCMApproved] BIT NULL,
        [CMApprovedBy] NVARCHAR(50),
        [CMApprovedOn] DATETIME,
        [CMRemarks] NVARCHAR(1000),
        
        -- RM (Regional Manager) Approval - Upper Level Authority
        -- NULL = Not Reviewed, 0 = Rejected, 1 = Approved
        -- RM can override CM decision as they are upper level
        [IsRMApproved] BIT NULL,
        [RMApprovedBy] NVARCHAR(50),
        [RMApprovedOn] DATETIME,
        [RMRemarks] NVARCHAR(1000),
        
        -- Note: Status is calculated dynamically based on approvals
        -- If RM Approved = Final Approved (even if CM rejected)
        -- If RM Rejected = Final Rejected (even if CM approved)
        -- If RM NULL and CM Approved = Pending RM
        -- If RM NULL and CM Rejected = Pending RM (RM can override)
        -- If both NULL = Pending CM
        
        -- Audit Fields
        [CreatedBy] NVARCHAR(50),
        [CreatedOn] DATETIME DEFAULT GETUTCDATE(),
        [LastUpdatedBy] NVARCHAR(50),
        [UpdatedOn] DATETIME,
        
        -- Unique constraint: One request per ECode per MonthYear
        CONSTRAINT [UQ_AttendanceCount_ECode_MonthYear] UNIQUE([ECode], [MonthYear])
    );
    
    PRINT 'Table tblAttendanceCountApproval created successfully';
END
ELSE
BEGIN
    PRINT 'Table tblAttendanceCountApproval already exists';
END
GO

-- Attachments/Proof Documents Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[tblAttendanceCountAttachments]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[tblAttendanceCountAttachments] (
        [AttachmentId] BIGINT PRIMARY KEY IDENTITY(1,1),
        [AttendanceCountApprovalId] BIGINT NOT NULL,
        [FileUrl] NVARCHAR(500) NOT NULL,
        [FileName] NVARCHAR(255),
        [FileSize] BIGINT,
        [CreatedOn] DATETIME DEFAULT GETUTCDATE(),
        [CreatedBy] NVARCHAR(50)
        
        -- No foreign key constraint as requested
        -- Join will be done manually in queries when needed
    );
    
    PRINT 'Table tblAttendanceCountAttachments created successfully';
END
ELSE
BEGIN
    PRINT 'Table tblAttendanceCountAttachments already exists';
END
GO

-- Create Index for better performance
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AttendanceCountApproval_ECode_MonthYear')
BEGIN
    CREATE INDEX [IX_AttendanceCountApproval_ECode_MonthYear] 
    ON [dbo].[tblAttendanceCountApproval]([ECode], [MonthYear]);
    PRINT 'Index IX_AttendanceCountApproval_ECode_MonthYear created successfully';
END
GO

-- Status is now calculated dynamically, so no index needed

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AttendanceCountAttachments_ApprovalId')
BEGIN
    CREATE INDEX [IX_AttendanceCountAttachments_ApprovalId] 
    ON [dbo].[tblAttendanceCountAttachments]([AttendanceCountApprovalId]);
    PRINT 'Index IX_AttendanceCountAttachments_ApprovalId created successfully';
END
GO

PRINT 'Attendance Count Approval System Database Setup Completed';

