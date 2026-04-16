-- Updated stored procedure with optional Ecode parameter
CREATE PROCEDURE [dbo].[usp_InsertVendorEmployee]            
(            
    @ContractorCode NVARCHAR(200) = NULL,            
    @FirstName NVARCHAR(100),            
    @MiddleName NVARCHAR(100),            
    @LastName NVARCHAR(100),            
    @FATHER_S_NAME NVARCHAR(50) = NULL,            
    @EMAIL_ADDRESS NVARCHAR(100),            
    @MOBILE NVARCHAR(20),            
    @DepartmentId INT = NULL,            
    @DesignationId INT = NULL,            
    @LocationId INT = NULL,            
    @DOJ DATETIME = NULL,            
    @PasswordHash NVARCHAR(255) = NULL,        
    @Password VARCHAR(255) = NULL,            
    @DOB DATE = NULL,            
    @GENDER NVARCHAR(10) = NULL,            
   -- @UAN_NO NVARCHAR(50) = NULL,            
    @PAN_NO NVARCHAR(50) = NULL,            
    @AADHAR_NO NVARCHAR(50) = NULL,            
    @PERMANENT_ADDRESS NVARCHAR(255) = NULL,            
    @PERMANENT_ADDRESS_PIN_CODE NVARCHAR(10) = NULL,            
    @CreatedOn DATETIME = NULL,            
    @CreatedBy NVARCHAR(100),            
    @IsActive BIT = 1,            
    @IsDeleted BIT = 0,            
   -- @PFApplicable BIT = 1,            
   -- @ESICApplicable BIT = 1,            
    --@ESICNO NVARCHAR(100) = NULL,            
    @HusbandName NVARCHAR(100) = NULL,            
    @ShiftId INT = NULL,            
    @CompanyId INT = 4,            
    @ContractStartDate DATE = NULL,         
    @ContractEndDate DATE = NULL,           
    @Ecode NVARCHAR(20) = NULL, -- New optional Ecode parameter
    @NewEcode NVARCHAR(20) OUTPUT            
)            
AS            
BEGIN            
    SET NOCOUNT ON;            
          
    SET @ShiftId = COALESCE(NULLIF(@ShiftId,0),1);          
          
    DECLARE @Prefix NVARCHAR(10),            
            @NextNumber INT,            
            @FinalEcode NVARCHAR(20); -- Variable to hold the Ecode that will be used
          
    -- Check if Ecode is provided (not null, not empty, not just whitespace)
    IF @Ecode IS NOT NULL AND LTRIM(RTRIM(@Ecode)) != ''            
    BEGIN            
        SET @FinalEcode = LTRIM(RTRIM(@Ecode)); -- Use provided Ecode
            
        -- Check if the provided Ecode already exists
        IF EXISTS (SELECT 1 FROM tblEmployee WHERE Ecode = @FinalEcode AND IsDeleted = 0)            
        BEGIN            
            THROW 50002, 'Provided Ecode already exists. Please use a different Ecode or leave it empty for auto-generation.', 1;            
        END            
    END            
    ELSE            
    BEGIN            
        -- Generate new Ecode using existing logic
        SET @Prefix = CASE @CompanyId            
                        WHEN 1 THEN 'V'            
                        WHEN 2 THEN 'V2S'            
                        WHEN 3 THEN 'PT'            
                        WHEN 4 THEN 'CT'            
                      END;            
          
        /* 🔒 Strong lock to avoid duplicate generation */          
        SELECT @NextNumber =           
               ISNULL(MAX(          
                   TRY_CAST(          
                     SUBSTRING(Ecode, LEN(@Prefix)+1, 10) AS INT          
                   )          
               ),0) + 1          
        FROM tblEmployee WITH (UPDLOCK, HOLDLOCK)          
        WHERE Ecode LIKE @Prefix + '%'          
          AND CompanyId = @CompanyId;          
          
        /* Generate new Ecode */          
        SET @FinalEcode = @Prefix           
                           + RIGHT('00000' + CAST(@NextNumber AS VARCHAR),5);          
          
        /* Extra safety check */          
        IF EXISTS (SELECT 1 FROM tblEmployee WHERE Ecode = @FinalEcode)          
        BEGIN          
            THROW 50001, 'Duplicate Ecode detected. Please retry.', 1;          
        END            
    END            
          
    BEGIN TRANSACTION;            
          
    BEGIN TRY            
          
        /* Full name */          
        DECLARE @FULL_NAME NVARCHAR(255) = LTRIM(RTRIM(          
            COALESCE(@FirstName + ' ','') +          
            COALESCE(@MiddleName + ' ','') +          
            COALESCE(@LastName,'')          
        ));          
          
        /* Insert Employee */          
        INSERT INTO tblEmployee          
        (          
            [FULL NAME], FirstName, MiddleName, LastName,           
            [EMAIL ADDRESS], MOBILE,          
            DepartmentId, DesignationId, LocationId, DOJ,          
            Ecode, PasswordHash, [Password],          
            [FATHER'S NAME], DOB, GENDER,           
           -- [UAN NO],   
            [PAN NO],  
            [AADHAR NO],          
            [PRESENT ADDRESS], [PRESENT ADDRESS PIN CODE],          
            CreatedOn, CreatedBy, IsActive, IsDeleted,          
           -- PFApplicable, ESICApplicable,          
            ShiftID,  
           -- ESICNO,  
            [Husband Name],          
            ContractorCode, CompanyId,      
            [From], [To]   -- New columns      
        )          
        VALUES          
        (          
            @FULL_NAME, @FirstName, @MiddleName, @LastName,          
     @EMAIL_ADDRESS, @MOBILE,          
            @DepartmentId, @DesignationId, @LocationId,          
            ISNULL(@DOJ,GETDATE()),          
            @FinalEcode, @PasswordHash, @Password,          
            @FATHER_S_NAME, @DOB, @GENDER,          
           -- @UAN_NO,  
            @PAN_NO,  
            @AADHAR_NO,          
            @PERMANENT_ADDRESS, @PERMANENT_ADDRESS_PIN_CODE,          
            ISNULL(@CreatedOn,GETDATE()), @CreatedBy,          
            @IsActive, @IsDeleted,          
            --@PFApplicable, @ESICApplicable,          
            @ShiftId, --@ESICNO,  
            @HusbandName,          
            @ContractorCode, @CompanyId,      
            @ContractStartDate, @ContractEndDate             
        );          
          
        DECLARE @NewEmployeeId BIGINT = SCOPE_IDENTITY();          
          
        /* Assign default role */          
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
          
        -- Set the output parameter to the Ecode that was used
        SET @NewEcode = @FinalEcode;
          
        COMMIT TRANSACTION;          
          
    END TRY          
    BEGIN CATCH          
          
        IF @@TRANCOUNT > 0          
            ROLLBACK TRANSACTION;          
          
        THROW;          
          
    END CATCH            
END
