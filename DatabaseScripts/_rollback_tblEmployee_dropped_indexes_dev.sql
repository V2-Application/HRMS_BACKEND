-- Rollback script: recreate dropped tblEmployee indexes (dev)
CREATE NONCLUSTERED INDEX [IX_tblEmployee_Ecode_DOL] ON dbo.tblEmployee ([Ecode]) INCLUDE ([DepartmentId], [DesignationId], [FULL NAME], [DateOfLeft], [IsFNFCompleted]);
CREATE NONCLUSTERED INDEX [IX_tblEmployee_EmployeeId] ON dbo.tblEmployee ([EmployeeId]) INCLUDE ([UpdatedBy], [UpdatedOn], [FULL NAME]);
CREATE NONCLUSTERED INDEX [IX_tblEmployee_IsActiveCompany] ON dbo.tblEmployee ([IsActive], [CompanyId]);

