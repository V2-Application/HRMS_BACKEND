-- Adds SubDepartmentId1/2/3 (tblEmployee already has these columns) to the
-- Vendor-employee insert/update/get/list procs so Manpower onboarding can
-- capture Department + up to 3 levels of Sub-Department, same as the
-- Dept/SubDept/Designation mapping module.

-- =====================================================================
-- 1) usp_InsertVendorEmployee (single "Add Employee" flow)
-- =====================================================================
CREATE OR ALTER PROCEDURE [dbo].[usp_InsertVendorEmployee]
(
    @ContractorCode NVARCHAR(200) = NULL,
    @FirstName NVARCHAR(100),
    @MiddleName NVARCHAR(100),
    @LastName NVARCHAR(100),
    @FATHER_S_NAME NVARCHAR(50) = NULL,
    @EMAIL_ADDRESS NVARCHAR(100),
    @MOBILE NVARCHAR(20),
    @DepartmentId INT = NULL,
    @SubDepartmentId1 INT = NULL,
    @SubDepartmentId2 INT = NULL,
    @SubDepartmentId3 INT = NULL,
    @DesignationId INT = NULL,
    @LocationId INT = NULL,
    @DOJ DATETIME = NULL,
    @PasswordHash NVARCHAR(255) = NULL,
    @Password VARCHAR(255) = NULL,
    @DOB DATE = NULL,
    @GENDER NVARCHAR(10) = NULL,
    @PAN_NO NVARCHAR(50) = NULL,
    @AADHAR_NO NVARCHAR(50) = NULL,
    @PERMANENT_ADDRESS NVARCHAR(255) = NULL,
    @PERMANENT_ADDRESS_PIN_CODE NVARCHAR(10) = NULL,
    @CreatedOn DATETIME = NULL,
    @CreatedBy NVARCHAR(100),
    @IsActive BIT = 1,
    @IsDeleted BIT = 0,
    @HusbandName NVARCHAR(100) = NULL,
    @ShiftId INT = NULL,
    @CompanyId INT = 4,
    @ContractStartDate DATE = NULL,
    @ContractEndDate DATE = NULL,
    @Ecode NVARCHAR(20) = NULL,
    @NewEcode NVARCHAR(20) OUTPUT
)
AS
BEGIN
    SET NOCOUNT ON;

    SET @ShiftId = COALESCE(NULLIF(@ShiftId,0),1);

    DECLARE @Prefix NVARCHAR(10),
            @NextNumber INT,
            @FinalEcode NVARCHAR(20);

    IF @Ecode IS NOT NULL AND LTRIM(RTRIM(@Ecode)) != ''
    BEGIN
        SET @FinalEcode = LTRIM(RTRIM(@Ecode));

        IF EXISTS (SELECT 1 FROM tblEmployee WHERE Ecode = @FinalEcode AND IsDeleted = 0)
        BEGIN
            THROW 50002, 'Provided Ecode already exists. Please use a different Ecode or leave it empty for auto-generation.', 1;
        END
    END
    ELSE
    BEGIN
        SET @Prefix = CASE @CompanyId
                        WHEN 1 THEN 'V'
                        WHEN 2 THEN 'V2S'
                        WHEN 3 THEN 'PT'
                        WHEN 4 THEN 'CT'
                        WHEN 6 THEN 'E'
                      END;

        SELECT @NextNumber =
               ISNULL(MAX(
                   TRY_CAST(
                     SUBSTRING(Ecode, LEN(@Prefix)+1, 10) AS INT
                   )
               ),0) + 1
        FROM tblEmployee WITH (UPDLOCK, HOLDLOCK)
        WHERE Ecode LIKE @Prefix + '%'
          AND CompanyId = @CompanyId;

        SET @FinalEcode = @Prefix
                           + RIGHT('00000' + CAST(@NextNumber AS VARCHAR),5);

        IF EXISTS (SELECT 1 FROM tblEmployee WHERE Ecode = @FinalEcode)
        BEGIN
            THROW 50001, 'Duplicate Ecode detected. Please retry.', 1;
        END
    END

    BEGIN TRANSACTION;

    BEGIN TRY

        DECLARE @FULL_NAME NVARCHAR(255) = LTRIM(RTRIM(
            COALESCE(@FirstName + ' ','') +
            COALESCE(@MiddleName + ' ','') +
            COALESCE(@LastName,'')
        ));

        INSERT INTO tblEmployee
        (
            [FULL NAME], FirstName, MiddleName, LastName,
            [EMAIL ADDRESS], MOBILE,
            DepartmentId, SubDepartmentId1, SubDepartmentId2, SubDepartmentId3,
            DesignationId, LocationId, DOJ,
            Ecode, PasswordHash, [Password],
            [FATHER'S NAME], DOB, GENDER,
            [PAN NO],
            [AADHAR NO],
            [PRESENT ADDRESS], [PRESENT ADDRESS PIN CODE],
            CreatedOn, CreatedBy, IsActive, IsDeleted,
            ShiftID,
            [Husband Name],
            ContractorCode, CompanyId,
            [From], [To]
        )
        VALUES
        (
            @FULL_NAME, @FirstName, @MiddleName, @LastName,
            @EMAIL_ADDRESS, @MOBILE,
            @DepartmentId, @SubDepartmentId1, @SubDepartmentId2, @SubDepartmentId3,
            @DesignationId, @LocationId,
            ISNULL(@DOJ,GETDATE()),
            @FinalEcode, @PasswordHash, @Password,
            @FATHER_S_NAME, @DOB, @GENDER,
            @PAN_NO,
            @AADHAR_NO,
            @PERMANENT_ADDRESS, @PERMANENT_ADDRESS_PIN_CODE,
            ISNULL(@CreatedOn,GETDATE()), @CreatedBy,
            @IsActive, @IsDeleted,
            @ShiftId,
            @HusbandName,
            @ContractorCode, @CompanyId,
            @ContractStartDate, @ContractEndDate
        );

        DECLARE @NewEmployeeId BIGINT = SCOPE_IDENTITY();

        INSERT INTO tblEmployeeRole
        (
            EmployeeId, RoleId,
            AssignedOn, AssignedBy,
            LastUpdatedBy, LastUpdatedOn
        )
        VALUES
        (
            @NewEmployeeId, 3,
            GETDATE(), 'System',
            'System', GETDATE()
        );

        SET @NewEcode = @FinalEcode;

        COMMIT TRANSACTION;

    END TRY
    BEGIN CATCH

        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        THROW;

    END CATCH
END
GO

-- =====================================================================
-- 2) usp_InsertVendorEmployee2 (bulk-Excel upsert flow)
-- =====================================================================
CREATE OR ALTER PROCEDURE [dbo].[usp_InsertVendorEmployee2]
(
    @ContractorCode NVARCHAR(200) = NULL,
    @FirstName NVARCHAR(100),
    @MiddleName NVARCHAR(100) = NULL,
    @LastName NVARCHAR(100) = NULL,
    @FATHER_S_NAME NVARCHAR(50) = NULL,
    @EMAIL_ADDRESS NVARCHAR(100),
    @MOBILE NVARCHAR(20),
    @DepartmentId INT = NULL,
    @SubDepartmentId1 INT = NULL,
    @SubDepartmentId2 INT = NULL,
    @SubDepartmentId3 INT = NULL,
    @DesignationId INT = NULL,
    @LocationId INT = NULL,
    @DOJ DATETIME = NULL,
    @PasswordHash NVARCHAR(255) = NULL,
    @Password VARCHAR(255) = NULL,
    @DOB DATE = NULL,
    @GENDER NVARCHAR(10) = NULL,
    @PAN_NO NVARCHAR(50) = NULL,
    @AADHAR_NO NVARCHAR(50) = NULL,
    @PERMANENT_ADDRESS NVARCHAR(255) = NULL,
    @PERMANENT_ADDRESS_PIN_CODE NVARCHAR(10) = NULL,
    @CreatedOn DATETIME = NULL,
    @CreatedBy NVARCHAR(100),
    @IsActive BIT = 1,
    @IsDeleted BIT = 0,
    @HusbandName NVARCHAR(100) = NULL,
    @ShiftId INT = NULL,
    @CompanyId INT = 4,
    @ContractStartDate DATE = NULL,
    @ContractEndDate DATE = NULL,
    @Ecode NVARCHAR(20) = NULL,
    @NewEcode NVARCHAR(20) OUTPUT,

    -- Salary Fields
    @BasicSalary DECIMAL(18,2) = NULL,
    @CCA DECIMAL(18,2) = NULL,
    @DA DECIMAL(18,2) = NULL,
    @ExtraAllowance DECIMAL(18,2) = NULL,
    @SpecialAllowance DECIMAL(18,2) = NULL,
    @HRA DECIMAL(18,2) = NULL,
    @GROSS_SALARY DECIMAL(18,2) = NULL,
    @monthlyGrossCTC DECIMAL(18,2) = NULL,
    @annuallyNetCTC DECIMAL(18,2) = NULL,
    @ContractorRatePerDay DECIMAL(18,2) = NULL
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @ShiftId = COALESCE(NULLIF(@ShiftId,0),1);

    DECLARE @Prefix NVARCHAR(10),
            @NextNumber INT,
            @FinalEcode NVARCHAR(20),
            @ExistingEmployeeId BIGINT,
            @FULL_NAME NVARCHAR(255);

    BEGIN TRANSACTION;

    BEGIN TRY

        IF @Ecode IS NOT NULL AND LTRIM(RTRIM(@Ecode)) <> ''
        BEGIN
            SET @FinalEcode = LTRIM(RTRIM(@Ecode));
        END
        ELSE
        BEGIN
            SET @Prefix = CASE @CompanyId
                            WHEN 1 THEN 'V'
                            WHEN 2 THEN 'V2S'
                            WHEN 3 THEN 'PT'
                            WHEN 4 THEN 'CT'
                          END;

            SELECT @NextNumber =
                ISNULL(MAX(
                    TRY_CAST(SUBSTRING(Ecode, LEN(@Prefix)+1, 10) AS INT)
                ),0) + 1
            FROM tblEmployee WITH (UPDLOCK, HOLDLOCK)
            WHERE Ecode LIKE @Prefix + '%'
              AND CompanyId = @CompanyId;

            SET @FinalEcode = @Prefix
                              + RIGHT('00000' + CAST(@NextNumber AS VARCHAR),5);
        END

        SELECT @ExistingEmployeeId = EmployeeId
        FROM tblEmployee WITH (UPDLOCK, HOLDLOCK)
        WHERE Ecode = @FinalEcode;

        SET @FULL_NAME =
            LTRIM(RTRIM(
                COALESCE(@FirstName + ' ','') +
                COALESCE(@MiddleName + ' ','') +
                COALESCE(@LastName,'')
            ));

        IF @ExistingEmployeeId IS NOT NULL
        BEGIN
            UPDATE tblEmployee
            SET
                ContractorCode = @ContractorCode,
                CompanyId = 4,
                ContractorRatePerDay = @ContractorRatePerDay
            WHERE EmployeeId = @ExistingEmployeeId;

            SET @NewEcode = @FinalEcode;

            COMMIT TRANSACTION;
            RETURN;
        END

        INSERT INTO tblEmployee
        (
            [FULL NAME], FirstName, MiddleName, LastName,
            [EMAIL ADDRESS], MOBILE,
            DepartmentId, SubDepartmentId1, SubDepartmentId2, SubDepartmentId3,
            DesignationId, LocationId, DOJ,
            Ecode, PasswordHash, [Password],
            [FATHER'S NAME], DOB, GENDER,
            [PAN NO], [AADHAR NO],
            [PRESENT ADDRESS], [PRESENT ADDRESS PIN CODE],
            CreatedOn, CreatedBy, IsActive, IsDeleted,
            ShiftID,
            [Husband Name],
            ContractorCode, CompanyId,
            [From], [To],
            BasicSalary, CCA, DA, ExtraAllowance,
            SpecialAllowance, HRA,
            [GROSS SALARY], monthlyGrossCTC, annuallyNetCTC, ContractorRatePerDay
        )
        VALUES
        (
            @FULL_NAME, @FirstName, @MiddleName, @LastName,
            @EMAIL_ADDRESS, @MOBILE,
            @DepartmentId, @SubDepartmentId1, @SubDepartmentId2, @SubDepartmentId3,
            @DesignationId, @LocationId,
            ISNULL(@DOJ,GETDATE()),
            @FinalEcode, @PasswordHash, @Password,
            @FATHER_S_NAME, @DOB, @GENDER,
            @PAN_NO, @AADHAR_NO,
            @PERMANENT_ADDRESS, @PERMANENT_ADDRESS_PIN_CODE,
            ISNULL(@CreatedOn,GETDATE()), @CreatedBy,
            @IsActive, @IsDeleted,
            @ShiftId,
            @HusbandName,
            @ContractorCode, @CompanyId,
            @ContractStartDate, @ContractEndDate,
            @BasicSalary, @CCA, @DA, @ExtraAllowance,
            @SpecialAllowance, @HRA,
            @GROSS_SALARY, @monthlyGrossCTC, @annuallyNetCTC, @ContractorRatePerDay
        );

        DECLARE @NewEmployeeId BIGINT = SCOPE_IDENTITY();

        INSERT INTO tblEmployeeRole
        (
            EmployeeId, RoleId,
            AssignedOn, AssignedBy,
            LastUpdatedBy, LastUpdatedOn
        )
        VALUES
        (
            @NewEmployeeId, 3,
            GETDATE(), 'System',
            'System', GETDATE()
        );

        SET @NewEcode = @FinalEcode;

        COMMIT TRANSACTION;

    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        THROW;
    END CATCH

END
GO

-- =====================================================================
-- 3) usp_UpdateVendorEmployee
-- =====================================================================
CREATE OR ALTER PROCEDURE [dbo].[usp_UpdateVendorEmployee]
    @Ecode VARCHAR(20),
    @ContractorCode NVARCHAR(200) = NULL,
    @FirstName NVARCHAR(100) = NULL,
    @MiddleName NVARCHAR(100) = NULL,
    @LastName NVARCHAR(100) = NULL,
    @FATHER_S_NAME NVARCHAR(50) = NULL,
    @EMAIL_ADDRESS NVARCHAR(100) = NULL,
    @MOBILE NVARCHAR(20) = NULL,
    @DepartmentId INT = NULL,
    @SubDepartmentId1 INT = NULL,
    @SubDepartmentId2 INT = NULL,
    @SubDepartmentId3 INT = NULL,
    @DesignationId INT = NULL,
    @LocationId INT = NULL,
    @DOJ DATETIME = NULL,
    @DOB DATE = NULL,
    @GENDER NVARCHAR(10) = NULL,
    @PAN_NO NVARCHAR(50) = NULL,
    @AADHAR_NO NVARCHAR(50) = NULL,
    @PERMANENT_ADDRESS NVARCHAR(255) = NULL,
    @PERMANENT_ADDRESS_PIN_CODE NVARCHAR(10) = NULL,
    @UpdatedBy NVARCHAR(100) = NULL,
    @IsActive BIT = NULL,
    @HusbandName NVARCHAR(100) = NULL,
    @ShiftId INT = NULL,
    @ContractStartDate DATE = NULL,
    @ContractEndDate DATE = NULL,
    -- Salary Fields
    @BasicSalary DECIMAL(18,2) = NULL,
    @CCA DECIMAL(18,2) = NULL,
    @DA DECIMAL(18,2) = NULL,
    @ExtraAllowance DECIMAL(18,2) = NULL,
    @SpecialAllowance DECIMAL(18,2) = NULL,
    @HRA DECIMAL(18,2) = NULL,
    @GROSS_SALARY DECIMAL(18,2) = NULL,
    @monthlyGrossCTC DECIMAL(18,2) = NULL,
    @annuallyNetCTC DECIMAL(18,2) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SET @ShiftId = COALESCE(NULLIF(@ShiftId, 0), 1);

    DECLARE @FULL_NAME NVARCHAR(255);
    SELECT @FULL_NAME =
        LTRIM(RTRIM(
            COALESCE(NULLIF(@FirstName, ''), FirstName, '') + ' ' +
            COALESCE(NULLIF(@MiddleName, ''), MiddleName, '') + ' ' +
            COALESCE(NULLIF(@LastName, ''), LastName, '')
        ))
    FROM tblEmployee
    WHERE Ecode = @Ecode AND ContractorCode = @ContractorCode;

    IF EXISTS (
        SELECT 1
        FROM tblEmployee
        WHERE Ecode = @Ecode
          AND ContractorCode = @ContractorCode
    )
    BEGIN
        UPDATE tblEmployee
        SET
            [FULL NAME] = COALESCE(@FULL_NAME, [FULL NAME]),
            FirstName = COALESCE(NULLIF(@FirstName, ''), FirstName),
            MiddleName = COALESCE(NULLIF(@MiddleName, ''), MiddleName),
            LastName = COALESCE(NULLIF(@LastName, ''), LastName),
            [EMAIL ADDRESS] = COALESCE(NULLIF(@EMAIL_ADDRESS, ''), [EMAIL ADDRESS]),
            MOBILE = COALESCE(NULLIF(@MOBILE, ''), MOBILE),
            DepartmentId = COALESCE(@DepartmentId, DepartmentId),
            SubDepartmentId1 = COALESCE(@SubDepartmentId1, SubDepartmentId1),
            SubDepartmentId2 = COALESCE(@SubDepartmentId2, SubDepartmentId2),
            SubDepartmentId3 = COALESCE(@SubDepartmentId3, SubDepartmentId3),
            DesignationId = COALESCE(@DesignationId, DesignationId),
            LocationId = COALESCE(@LocationId, LocationId),
            DOJ = COALESCE(@DOJ, DOJ),
            [FATHER'S NAME] = COALESCE(NULLIF(@FATHER_S_NAME, ''), [FATHER'S NAME]),
            DOB = COALESCE(@DOB, DOB),
            GENDER = COALESCE(NULLIF(@GENDER, ''), GENDER),
            [PAN NO] = COALESCE(NULLIF(@PAN_NO, ''), [PAN NO]),
            [AADHAR NO] = COALESCE(NULLIF(@AADHAR_NO, ''), [AADHAR NO]),
            [PRESENT ADDRESS] = COALESCE(NULLIF(@PERMANENT_ADDRESS, ''), [PRESENT ADDRESS]),
            [PRESENT ADDRESS PIN CODE] = COALESCE(NULLIF(@PERMANENT_ADDRESS_PIN_CODE, ''), [PRESENT ADDRESS PIN CODE]),
            IsActive = COALESCE(@IsActive, IsActive),
            ShiftID = COALESCE(@ShiftId, ShiftID),
            [From] = COALESCE(@ContractStartDate, [From]),
            [To] = COALESCE(@ContractEndDate, [To]),
            [Husband Name] = COALESCE(NULLIF(@HusbandName, ''), [Husband Name]),
            BasicSalary = COALESCE(@BasicSalary, BasicSalary),
            CCA = COALESCE(@CCA, CCA),
            DA = COALESCE(@DA, DA),
            ExtraAllowance = COALESCE(@ExtraAllowance, ExtraAllowance),
            SpecialAllowance = COALESCE(@SpecialAllowance, SpecialAllowance),
            HRA = COALESCE(@HRA, HRA),
            [GROSS SALARY] = COALESCE(@GROSS_SALARY, [GROSS SALARY]),
            monthlyGrossCTC = COALESCE(@monthlyGrossCTC, monthlyGrossCTC),
            annuallyNetCTC = COALESCE(@annuallyNetCTC, annuallyNetCTC),
            UpdatedOn = GETDATE(),
            UpdatedBy = COALESCE(@UpdatedBy, UpdatedBy)
        WHERE
            Ecode = @Ecode
            AND ContractorCode = @ContractorCode;
    END
    ELSE
    BEGIN
        RAISERROR('Employee with given Ecode does not exist.', 16, 1);
    END
END
GO

-- =====================================================================
-- 4) usp_GetVendorEmployeesByEcode
-- =====================================================================
CREATE OR ALTER PROCEDURE dbo.usp_GetVendorEmployeesByEcode
(
    @Ecode VARCHAR(20),
    @ContractorCode NVARCHAR(200)
)
AS
BEGIN
    SET NOCOUNT ON;

    IF @Ecode IS NULL OR @ContractorCode IS NULL
    BEGIN
        RAISERROR('Ecode and ContractorCode are required parameters.', 16, 1);
        RETURN;
    END

    SELECT
        e.EmployeeId,
        e.Ecode,
        e.[FULL NAME],
        e.FirstName,
        e.MiddleName,
        e.LastName,
        e.[EMAIL ADDRESS],
        e.MOBILE,
        e.[PRESENT ADDRESS],
        e.[PRESENT ADDRESS PIN CODE],
        e.DOB,
        e.GENDER,
        e.[UAN NO],
        e.[PAN NO],
        e.[AADHAR NO],
        e.PFApplicable,
        e.ESICApplicable,
        e.ShiftID,
        m.ShiftName,
        e.ESICNO,
        e.[FATHER'S NAME] as FatherName,
        e.[Husband Name],
        e.ContractorCode,
        d.DepartmentId,
        d.DepartmentName,
        sd1.SubDepartmentId AS SubDepartmentId1,
        sd1.SubDepartmentName AS SubDepartmentName1,
        sd2.SubDepartmentId AS SubDepartmentId2,
        sd2.SubDepartmentName AS SubDepartmentName2,
        sd3.SubDepartmentId AS SubDepartmentId3,
        sd3.SubDepartmentName AS SubDepartmentName3,
        dg.DesignationId,
        dg.DesignationName,
        v.ContractorName,
        l.LocationId,
        l.LocationName,
        e.IsActive,
        e.DOJ ,
        e.[From] as ContractStartDate,
        e.[To] as ContractEndDate,
        e.BasicSalary,
        e.CCA,
        e.DA,
        e.ExtraAllowance,
        e.SpecialAllowance,
        e.HRA,
        e.[GROSS SALARY],
        e.monthlyGrossCTC,
        e.annuallyNetCTC,
        e.ContractorRatePerDay
    FROM tblEmployee e
    LEFT JOIN tblDepartment d
        ON e.DepartmentId = d.DepartmentId
    LEFT JOIN tblSubDepartment sd1
        ON e.SubDepartmentId1 = sd1.SubDepartmentId
    LEFT JOIN tblSubDepartment sd2
        ON e.SubDepartmentId2 = sd2.SubDepartmentId
    LEFT JOIN tblSubDepartment sd3
        ON e.SubDepartmentId3 = sd3.SubDepartmentId
    LEFT JOIN tblDesignation dg
        ON e.DesignationId = dg.DesignationId
    INNER JOIN tblVendorMaster v
        ON e.ContractorCode = v.ContractorCode AND v.IsActive = 1
    LEFT JOIN tblLocation l
        ON e.LocationId = l.LocationId
    LEFT JOIN tblShiftMaster m
        ON e.ShiftID = m.ShiftID

    WHERE
        e.IsDeleted = 0
        AND e.Ecode = @Ecode
        AND e.ContractorCode = @ContractorCode
        AND e.CompanyId = 4
    ORDER BY e.Ecode;
END
GO

-- =====================================================================
-- 5) usp_GetVendorEmployeesListByFilter
-- =====================================================================
CREATE OR ALTER PROCEDURE usp_GetVendorEmployeesListByFilter
(
    @ContractorCode NVARCHAR(200),
    @SearchTerm NVARCHAR(100) = '',
    @IsActiveFilter INT = NULL,
    @ContractStartDate DATE = NULL,
    @ContractEndDate DATE = NULL,
    @PageNumber INT = 1,
    @PageSize INT = 10,
    @TotalRecords INT OUTPUT
)
AS
BEGIN
    SET NOCOUNT ON;

    IF @ContractorCode IS NULL OR LTRIM(RTRIM(@ContractorCode)) = ''
    BEGIN
        RAISERROR('ContractorCode is required.', 16, 1);
        RETURN;
    END

    DECLARE @FilteredEmployees TABLE
    (
        EmployeeId BIGINT,
        Ecode NVARCHAR(50),
        FullName NVARCHAR(100),
        DOJ DATE,
        IsActive BIT,
        DepartmentName NVARCHAR(100),
        SubDepartmentName1 NVARCHAR(200),
        SubDepartmentName2 NVARCHAR(200),
        SubDepartmentName3 NVARCHAR(200),
        DesignationName NVARCHAR(100),
        ContractorName NVARCHAR(200),
        ShiftName NVARCHAR(100),
        ContractStartDate DATE,
        ContractEndDate DATE
    );

    INSERT INTO @FilteredEmployees
    SELECT
        e.EmployeeId,
        e.Ecode,
        e.[FULL NAME] AS FullName,
        e.DOJ,
        e.IsActive,
        d.DepartmentName,
        sd1.SubDepartmentName,
        sd2.SubDepartmentName,
        sd3.SubDepartmentName,
        dg.DesignationName,
        v.ContractorName,
        s.ShiftName,
        e.[From] as ContractStartDate,
        e.[To] as ContractEndDate
    FROM tblEmployee e
    LEFT JOIN tblDepartment d ON e.DepartmentId = d.DepartmentId
    LEFT JOIN tblSubDepartment sd1 ON e.SubDepartmentId1 = sd1.SubDepartmentId
    LEFT JOIN tblSubDepartment sd2 ON e.SubDepartmentId2 = sd2.SubDepartmentId
    LEFT JOIN tblSubDepartment sd3 ON e.SubDepartmentId3 = sd3.SubDepartmentId
    LEFT JOIN tblDesignation dg ON e.DesignationId = dg.DesignationId
    INNER JOIN tblVendorMaster v ON e.ContractorCode = v.ContractorCode AND v.IsActive = 1
    LEFT JOIN tblShiftMaster s ON e.ShiftID = s.ShiftID
    WHERE
        e.IsDeleted = 0
        AND e.ContractorCode = @ContractorCode
        AND e.CompanyId = 4
        AND (@IsActiveFilter IS NULL OR e.IsActive = CASE WHEN @IsActiveFilter = 1 THEN 1 ELSE 0 END)
        AND (@ContractStartDate IS NULL OR e.[From] >= @ContractStartDate)
        AND (@ContractEndDate IS NULL OR e.[To] <= @ContractEndDate)
        AND (
            @SearchTerm = ''
            OR e.[FULL NAME] LIKE '%' + @SearchTerm + '%'
            OR e.Ecode LIKE '%' + @SearchTerm + '%'
            OR d.DepartmentName LIKE '%' + @SearchTerm + '%'
            OR sd1.SubDepartmentName LIKE '%' + @SearchTerm + '%'
            OR sd2.SubDepartmentName LIKE '%' + @SearchTerm + '%'
            OR sd3.SubDepartmentName LIKE '%' + @SearchTerm + '%'
            OR dg.DesignationName LIKE '%' + @SearchTerm + '%'
            OR s.ShiftName LIKE '%' + @SearchTerm + '%'
            OR v.ContractorName LIKE '%' + @SearchTerm + '%'
            OR CONVERT(VARCHAR, e.[From], 120) LIKE '%' + @SearchTerm + '%'
            OR CONVERT(VARCHAR, e.[To], 120) LIKE '%' + @SearchTerm + '%'
        );

    SELECT @TotalRecords = COUNT(*) FROM @FilteredEmployees;

    SELECT *
    FROM @FilteredEmployees
    ORDER BY Ecode
    OFFSET CASE WHEN @PageNumber > 0 AND @PageSize > 0 THEN (@PageNumber - 1) * @PageSize ELSE 0 END ROWS
    FETCH NEXT CASE WHEN @PageNumber > 0 AND @PageSize > 0 THEN @PageSize ELSE 2147483647 END ROWS ONLY;
END
GO
