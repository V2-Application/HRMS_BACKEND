-- Updated stored procedure with salary fields
CREATE PROCEDURE [dbo].[usp_UpdateVendorEmployee]                            
    @Ecode VARCHAR(20),        
    @ContractorCode NVARCHAR(200) = NULL,        
    @FirstName NVARCHAR(100) = NULL,        
    @MiddleName NVARCHAR(100) = NULL,        
    @LastName NVARCHAR(100) = NULL,        
    @FATHER_S_NAME NVARCHAR(50) = NULL,        
    @EMAIL_ADDRESS NVARCHAR(100) = NULL,        
    @MOBILE NVARCHAR(20) = NULL,        
    @DepartmentId INT = NULL,        
    @DesignationId INT = NULL,        
    @LocationId INT = NULL,        
    @DOJ DATETIME = NULL,        
   -- @PasswordHash NVARCHAR(255) = NULL,        
   -- @Password VARCHAR(255) = NULL,        
    @DOB DATE = NULL,        
    @GENDER NVARCHAR(10) = NULL,        
    --@UAN_NO NVARCHAR(50) = NULL,        
    @PAN_NO NVARCHAR(50) = NULL,        
    @AADHAR_NO NVARCHAR(50) = NULL,        
    @PERMANENT_ADDRESS NVARCHAR(255) = NULL,        
    @PERMANENT_ADDRESS_PIN_CODE NVARCHAR(10) = NULL,        
    @UpdatedBy NVARCHAR(100) = NULL,        
    @IsActive BIT = NULL,        
   -- @PFApplicable BIT = NULL,        
   -- @ESICApplicable BIT = NULL,        
    -- @ESICNO NVARCHAR(100) = NULL,        
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
            DesignationId = COALESCE(@DesignationId, DesignationId),        
            LocationId = COALESCE(@LocationId, LocationId),        
            DOJ = COALESCE(@DOJ, DOJ),        
            -- PasswordHash = COALESCE(@PasswordHash, PasswordHash),        
            -- [Password] = COALESCE(@Password, [Password]),        
            [FATHER'S NAME] = COALESCE(NULLIF(@FATHER_S_NAME, ''), [FATHER'S NAME]),        
            DOB = COALESCE(@DOB, DOB),        
            GENDER = COALESCE(NULLIF(@GENDER, ''), GENDER),        
           -- [UAN NO] = COALESCE(NULLIF(@UAN_NO, ''), [UAN NO]),        
            [PAN NO] = COALESCE(NULLIF(@PAN_NO, ''), [PAN NO]),        
            [AADHAR NO] = COALESCE(NULLIF(@AADHAR_NO, ''), [AADHAR NO]),        
            [PRESENT ADDRESS] = COALESCE(NULLIF(@PERMANENT_ADDRESS, ''), [PRESENT ADDRESS]),        
            [PRESENT ADDRESS PIN CODE] = COALESCE(NULLIF(@PERMANENT_ADDRESS_PIN_CODE, ''), [PRESENT ADDRESS PIN CODE]),        
            IsActive = COALESCE(@IsActive, IsActive),        
           -- PFApplicable = COALESCE(@PFApplicable, PFApplicable),        
            -- ESICApplicable = COALESCE(@ESICApplicable, ESICApplicable),        
            ShiftID = COALESCE(@ShiftId, ShiftID),      
            [From] = COALESCE(@ContractStartDate, [From]),        
            [To] = COALESCE(@ContractEndDate, [To]),        
            -- ESICNO = COALESCE(NULLIF(@ESICNO, ''), ESICNO),        
            [Husband Name] = COALESCE(NULLIF(@HusbandName, ''), [Husband Name]),        
            -- Salary fields
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
END;
