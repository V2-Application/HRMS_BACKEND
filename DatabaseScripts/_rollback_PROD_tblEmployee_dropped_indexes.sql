-- PROD rollback: recreate dropped tblEmployee indexes
CREATE NONCLUSTERED INDEX [IX_tblEmployee_Ecode_DOL] ON dbo.tblEmployee ([Ecode]) INCLUDE ([DepartmentId], [DesignationId], [FULL NAME], [DateOfLeft], [IsFNFCompleted]);
CREATE NONCLUSTERED INDEX [IX_tblEmployee_IsActiveCompany] ON dbo.tblEmployee ([IsActive], [CompanyId]);

