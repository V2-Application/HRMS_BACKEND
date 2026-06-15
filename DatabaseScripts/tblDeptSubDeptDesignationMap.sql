-- Mapping: (Department + SubDepartment levels 1/2/3) -> Designation
-- Drives the designation dropdown on the candidate/employee form.
-- Additive only: new table, no existing object/data touched.
IF OBJECT_ID('dbo.tblDeptSubDeptDesignationMap') IS NULL
BEGIN
    CREATE TABLE dbo.tblDeptSubDeptDesignationMap (
        Id               INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DeptSubDeptDesigMap PRIMARY KEY,
        DepartmentId     INT          NOT NULL,
        SubDepartmentId1 INT          NULL,
        SubDepartmentId2 INT          NULL,
        SubDepartmentId3 INT          NULL,
        DesignationId    INT          NOT NULL,
        IsActive         BIT          NOT NULL CONSTRAINT DF_DSDDM_IsActive  DEFAULT(1),
        IsDeleted        BIT          NOT NULL CONSTRAINT DF_DSDDM_IsDeleted DEFAULT(0),
        CreatedBy        NVARCHAR(100) NULL,
        CreatedOn        DATETIME     NOT NULL CONSTRAINT DF_DSDDM_CreatedOn DEFAULT(GETDATE()),
        UpdatedBy        NVARCHAR(100) NULL,
        UpdatedOn        DATETIME     NULL
    );

    CREATE INDEX IX_DSDDM_Lookup
        ON dbo.tblDeptSubDeptDesignationMap (DepartmentId, SubDepartmentId1, SubDepartmentId2, SubDepartmentId3)
        INCLUDE (DesignationId, IsActive, IsDeleted);
END
