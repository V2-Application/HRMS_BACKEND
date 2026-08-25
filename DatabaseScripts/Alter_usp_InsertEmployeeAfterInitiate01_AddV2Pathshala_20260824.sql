/*
    Alter_usp_InsertEmployeeAfterInitiate01_AddV2Pathshala_20260824.sql
    PROD: 192.168.151.28\hrms, database HRMS

    Requirement
    -----------
    New company "V2 Pathshala" in the candidate form's company dropdown, and any
    joining under it gets an Ecode starting with 'F', series F00001 onwards.

    Part 1 (already applied separately): company master row
        INSERT INTO dbo.tblCompany (CompanyName) VALUES ('V2 Pathshala');
        -> CompanyId = 7
    The dropdown needs no code change: DropDownService.GetCompany() returns every
    row of tblCompany unfiltered (Implementation/DropDownService.cs:126-135).

    Part 2 (this script): Ecode generation.
    usp_InsertEmployeeAfterInitiate01 is the live joining proc
    (CandidateService.cs:1592). Two additions only:

        prefix map      WHEN 7 THEN 'F'
        first-of-series ELSE IF @CompanyId = 7 SET @NextNumber = 1

    Padding is the default 5 digits, so the first joining is F00001, then F00002...
    (@PadLength = 5; only Aquatica overrides it to 4, and only CompanyId 2 skips
    zero-padding.) Existing prefixes are untouched: 1='V', 2='V2S', 3='PT',
    4='CT', 6='E'.

    Series continuation is read as
        SELECT TOP 1 @LastEcode ... WHERE Ecode LIKE @Prefix + '%' AND CompanyId = @CompanyId
        ORDER BY EmployeeId DESC
    so 'F%' is scoped to CompanyId 7 and cannot collide with any other company's codes.

    Not changed (flag if these matter):
      * usp_InsertEmployeeAfterInitiate / ...New / usp_InsertVendorEmployee* keep the
        old 1/2/3-only or vendor-specific prefix maps. Only the 01 proc is wired to
        the candidate joining flow today.
      * GetEmployeeDetailsforexcel_Ishu builds [LocBasedECode] with a CompanyId CASE
        (1/2/3 strip 'V'/'V2S'/'PT'); CompanyId 7 falls to ELSE and shows the raw
        Ecode. Cosmetic, export-only.

    Rollback: BACKUP_usp_InsertEmployeeAfterInitiate01_Original_20260824.sql
              (and DELETE FROM tblCompany WHERE CompanyId = 7, only while unused)
*/


-- =========================================================================
-- SP 2: usp_InsertEmployeeAfterInitiate01
-- =========================================================================
ALTER PROCEDURE [dbo].[usp_InsertEmployeeAfterInitiate01]
      @CandidateId bigint,
      @FirstName NVARCHAR(100),
      @MiddleName NVARCHAR(100),
      @LastName NVARCHAR(100),
      @EMAIL_ADDRESS NVARCHAR(100),
      @MOBILE NVARCHAR(20),
      @DepartmentId INT = NULL,
      @DesignationId INT = NULL,
      @LocationId INT = NULL,
      @DOJ DATETIME = NULL,
      @PasswordHash NVARCHAR(255),
      @UpdatedBy NVARCHAR(100) = NULL,
      @TITLE NVARCHAR(50) = NULL,
      @FATHER_S_NAME NVARCHAR(100) = NULL,
      @MOTHER_S_NAME NVARCHAR(100) = NULL,
      @DOB DATE = NULL,
      @GENDER NVARCHAR(10) = NULL,
      @GROSS_SALARY DECIMAL(18, 2) = NULL,
      @UAN_NO NVARCHAR(50) = NULL,
      @PAN_NO NVARCHAR(50) = NULL,
      @AADHAR_NO NVARCHAR(50) = NULL,
      @NAME_ON_ADHAR NVARCHAR(100) = NULL,
      @PLACE_OF_BIRTH NVARCHAR(100) = NULL,
      @PRESENT_ADDRESS NVARCHAR(255) = NULL,
      @PRESENT_ADDRESS_PIN_CODE NVARCHAR(10) = NULL,
      @PERMANENT_ADDRESS NVARCHAR(255) = NULL,
      @PERMANENT_ADDRESS_PIN_CODE NVARCHAR(10) = NULL,
      @APPLICANT_CODE NVARCHAR(50) = NULL,
      @WEEKLY_OFF NVARCHAR(50) = NULL,
      @MARITIAL_STATUS NVARCHAR(20) = NULL,
      @ISRELATIVEINCOMPANY BIT = NULL,
      @NATIONALITY NVARCHAR(50) = NULL,
      @RELIGION NVARCHAR(50) = NULL,
      @BANK_NAME NVARCHAR(100) = NULL,
      @A_C_NO NVARCHAR(50) = NULL,
      @BANK_IFSC_CODE NVARCHAR(20) = NULL,
      @REFERENCE1__OF_LAST_3_COMPANY NVARCHAR(100) = NULL,
      @CONTACT1_OF_LAST_3_COMPANY NVARCHAR(20) = NULL,
      @REFERENCE2__OF_LAST_3_COMPANY1 NVARCHAR(100) = NULL,
      @CONTACT2_OF_LAST_3_COMPANY1 NVARCHAR(20) = NULL,
      @REFERENCE3__OF_LAST_3_COMPANY11 NVARCHAR(100) = NULL,
      @CONTACT3_OF_LAST_3_COMPANY11 NVARCHAR(20) = NULL,
      @REFERENCE4__OF_LAST_3_COMPANY11 NVARCHAR(100) = NULL,
      @CONTACT4_OF_LAST_3_COMPANY11 NVARCHAR(20) = NULL,
      @REFERENCE5__OF_LAST_3_COMPANY111 NVARCHAR(100) = NULL,
      @CONTACT5_OF_LAST_3_COMPANY111 NVARCHAR(20) = NULL,
      @HIGHEST_QUALIFICATION NVARCHAR(100) = NULL,
      @BENEFICIARY_ADDRESS NVARCHAR(255) = NULL,
      @REFERENCE NVARCHAR(255) = NULL,
      @CreatedOn DATETIME = NULL,
      @CreatedBy NVARCHAR(100),
      @IsActive BIT = 1,
      @IsDeleted BIT = 0,
      @IsSalarySlipUploaded BIT = 0,
      @IsBankStatementUploaded BIT = 0,
      @IsPrevOfferLetterUploaded BIT = 0,
      @IsPassportPhotoUploaded BIT = 0,
      @IsPanAttachmentUploaded BIT = 0,
      @IsAadharAttachmentUploaded BIT = 0,
      @IsBankPassbookAttachmentUpoaded BIT = 0,
      @IsEducationAttachmentUploaded BIT = 0,
      @StatusId INT = NULL,
      @ApplicantId NVARCHAR(50) = NULL,
      @BasicSalary DECIMAL(18, 2) = NULL,
      @HRA DECIMAL(18, 2) = NULL,
      @CCA DECIMAL(18, 2) = NULL,
      @SpecialAllowance DECIMAL(18, 2) = NULL,
      @DA DECIMAL(18, 2) = NULL,
      @ExtraAllowance DECIMAL(18, 2) = NULL,
      @monthlyGrossCTC DECIMAL(18, 2) = NULL,
      @annuallyNetCTC DECIMAL(18, 2) = NULL,
      @IsResumeUploaded BIT = 0,
      @TotalExperience DECIMAL(18, 2) = NULL,
      @SalaryExpectation DECIMAL(18, 2) = NULL,
      @AdditionalInfoApplicant NVARCHAR(MAX) = NULL,
      @Agreement BIT = 0,
      @IsApplicant BIT = 1,
      @IsApplicantApproved BIT = 0,
      @PFApplicable BIT = 1,
      @BonusApplicable NVARCHAR(10) = 'No',
      @ESICApplicable BIT = 1,
      @CompanyId INT,
      @ESICNO NVARCHAR(100),
      @MaritalStatus NVARCHAR(100),
      @HusbandName NVARCHAR(100),
      @PreferredLocation NVARCHAR(100),
      @ReportHeadEcode NVARCHAR(50) = NULL,
      @ShiftId INT = NULL,
      @NewEcode NVARCHAR(50) OUTPUT
  AS
  BEGIN
      SET NOCOUNT ON;
      SET @ShiftId = COALESCE(NULLIF(@ShiftId, 0), 1);

      -----------------------------------------------------------------
      -- VALIDATION: Check if Candidate already exists in tblEmployee
      -----------------------------------------------------------------
      IF EXISTS (
          SELECT 1
          FROM tblEmployee WITH (NOLOCK)
          WHERE CandidateId = @CandidateId
      )
      BEGIN
          RAISERROR('Candidate already initiated.', 16, 1);
          RETURN;
      END
      -----------------------------------------------------------------

      DECLARE @Prefix NVARCHAR(10), @LastEcode NVARCHAR(50), @NextNumber INT;
      DECLARE @PadLength INT = 5;  -- default 5 digits

      -- Determine prefix
      SET @Prefix = CASE @CompanyId
                      WHEN 1 THEN 'V'
                      WHEN 2 THEN 'V2S'
                      WHEN 3 THEN 'PT'
                      WHEN 4 THEN 'CT'
                      WHEN 6 THEN 'E'           -- Aquatica
                      WHEN 7 THEN 'F'           -- V2 Pathshala (F00001..)
                    END;

      -- Aquatica uses 4-digit padding (E0001..E9999)
      IF @CompanyId = 6
          SET @PadLength = 4;

      -- Get latest Ecode
      SELECT TOP 1 @LastEcode = Ecode
      FROM tblEmployee (NOLOCK)
      WHERE Ecode LIKE @Prefix + '%'
        AND CompanyId = @CompanyId
      ORDER BY EmployeeId DESC;

      -- Extract number
      IF @LastEcode IS NOT NULL
      BEGIN
          DECLARE @NumPart NVARCHAR(10) = SUBSTRING(@LastEcode, LEN(@Prefix) + 1, LEN(@LastEcode));
          SET @NextNumber = TRY_CAST(@NumPart AS INT) + 1;
      END
      ELSE
      BEGIN
          IF @CompanyId = 1
              SET @NextNumber = 1;
          ELSE IF @CompanyId = 2
              SET @NextNumber = 2701;
          ELSE IF @CompanyId = 3
              SET @NextNumber = 1;
          ELSE IF @CompanyId = 4
              SET @NextNumber = 1;
          ELSE IF @CompanyId = 6
              SET @NextNumber = 1;          -- Aquatica starts at E0001
          ELSE IF @CompanyId = 7
              SET @NextNumber = 1;          -- V2 Pathshala starts at F00001
      END;

      -- Generate new Ecode
      IF @CompanyId = 2
      BEGIN
          SET @NewEcode = @Prefix + CAST(@NextNumber AS VARCHAR(4));
      END
      ELSE
      BEGIN
          SET @NewEcode = @Prefix + RIGHT(REPLICATE('0', @PadLength) + CAST(@NextNumber AS VARCHAR), @PadLength);
      END;

      DECLARE @FULL_NAME NVARCHAR(255) = LTRIM(RTRIM(
          COALESCE(@FirstName + ' ', '') + COALESCE(@MiddleName + ' ', '') + COALESCE(@LastName, '')
      ));

      -- Carry the candidate's sub-departments (levels 1/2/3) onto the new employee record.
      DECLARE @SubDept1 INT, @SubDept2 INT, @SubDept3 INT;
      SELECT @SubDept1 = SubDepartmentId1, @SubDept2 = SubDepartmentId2, @SubDept3 = SubDepartmentId3
      FROM dbo.Candidate WITH (NOLOCK) WHERE Id = @CandidateId;

      IF NOT EXISTS (
          SELECT 1
          FROM tblEmployee (NOLOCK)
          WHERE Ecode = @NewEcode
            AND CompanyId = @CompanyId
      )
      BEGIN
          INSERT INTO tblEmployee(
              CandidateId, [FULL NAME], FirstName, MiddleName, LastName, [EMAIL ADDRESS], MOBILE,
              DepartmentId, DesignationId, LocationId, SubDepartmentId1, SubDepartmentId2, SubDepartmentId3, DOJ, Ecode, LastUpdatedBy,
              PasswordHash, PasswordSalt, UpdatedBy, UpdatedOn, TITLE, [FATHER'S NAME],
              [MOTHER'S NAME], DOB, GENDER, [GROSS SALARY], [UAN NO], [PAN NO], [AADHAR NO], [NAME ON ADHAR],
              [PLACE OF BIRTH], [PRESENT ADDRESS], [PRESENT ADDRESS PIN CODE], [PERMANENT ADDRESS],
              [PERMANENT ADDRESS PIN CODE], [APPLICANT CODE], [WEEKLY OFF], [MARITIAL STATUS], ISRELATIVEINCOMPANY,
              NATIONALITY, RELIGION, [BANK NAME], [A/C NO], [BANK IFSC CODE], [REFERENCE1  OF LAST 3 COMPANY],
              [CONTACT1 OF LAST 3 COMPANY], [REFERENCE2  OF LAST 3 COMPANY1], [CONTACT2 OF LAST 3 COMPANY1],
              [REFERENCE3  OF LAST 3 COMPANY11], [CONTACT3 OF LAST 3 COMPANY11], [REFERENCE4  OF LAST 3 COMPANY11],
              [CONTACT4 OF LAST 3 COMPANY11], [REFERENCE5  OF LAST 3 COMPANY111], [CONTACT5 OF LAST 3 COMPANY111],
              [HIGHEST QUALIFICATION], BENEFICIARY_ADDRESS, REFERENCE, CreatedOn, CreatedBy, IsActive,
              IsDeleted, IsSalarySlipUploaded, IsBankStatementUploaded, IsPrevOfferLetterUploaded,
              IsPassportPhotoUploaded, IsPanAttachmentUploaded, IsAadharAttachmentUploaded,
              IsBankPassbookAttachmentUpoaded, IsEducationAttachmentUploaded, StatusId, ApplicantId,
              BasicSalary, HRA, CCA, SpecialAllowance, DA, ExtraAllowance, monthlyGrossCTC, annuallyNetCTC,
              IsResumeUploaded, TotalExperience, SalaryExpectation, AdditionalInfoApplicant, Agreement,
              IsApplicant, IsApplicantApproved, PFApplicable, BonusApplicable, ESICApplicable,
              CompanyId, ESICNO, [Husband Name], PreferredLocation, ReportHeadEcode, ShiftID
          )
          VALUES (
              @CandidateId, @FULL_NAME, @FirstName, @MiddleName, @LastName, @EMAIL_ADDRESS, @MOBILE,
              @DepartmentId, @DesignationId, @LocationId, @SubDept1, @SubDept2, @SubDept3, ISNULL(@DOJ, GETDATE()), @NewEcode, @UpdatedBy,
              @PasswordHash, NULL, @UpdatedBy, GETDATE(), @TITLE, @FATHER_S_NAME, @MOTHER_S_NAME, @DOB, @GENDER,
              @GROSS_SALARY, @UAN_NO, @PAN_NO, @AADHAR_NO, @NAME_ON_ADHAR, @PLACE_OF_BIRTH, @PRESENT_ADDRESS,
              @PRESENT_ADDRESS_PIN_CODE, @PERMANENT_ADDRESS, @PERMANENT_ADDRESS_PIN_CODE, @APPLICANT_CODE,
              @WEEKLY_OFF, @MARITIAL_STATUS, @ISRELATIVEINCOMPANY, @NATIONALITY, @RELIGION, @BANK_NAME,
              @A_C_NO, @BANK_IFSC_CODE, @REFERENCE1__OF_LAST_3_COMPANY, @CONTACT1_OF_LAST_3_COMPANY,
              @REFERENCE2__OF_LAST_3_COMPANY1, @CONTACT2_OF_LAST_3_COMPANY1, @REFERENCE3__OF_LAST_3_COMPANY11,
              @CONTACT3_OF_LAST_3_COMPANY11, @REFERENCE4__OF_LAST_3_COMPANY11, @CONTACT4_OF_LAST_3_COMPANY11,
              @REFERENCE5__OF_LAST_3_COMPANY111, @CONTACT5_OF_LAST_3_COMPANY111, @HIGHEST_QUALIFICATION,
              @BENEFICIARY_ADDRESS, @REFERENCE, ISNULL(@CreatedOn, GETDATE()), @CreatedBy, @IsActive, @IsDeleted,
              @IsSalarySlipUploaded, @IsBankStatementUploaded, @IsPrevOfferLetterUploaded, @IsPassportPhotoUploaded,
              @IsPanAttachmentUploaded, @IsAadharAttachmentUploaded, @IsBankPassbookAttachmentUpoaded,
              @IsEducationAttachmentUploaded, @StatusId, @ApplicantId, @BasicSalary, @HRA, @CCA, @SpecialAllowance,
              @DA, @ExtraAllowance, @monthlyGrossCTC, @annuallyNetCTC, @IsResumeUploaded, @TotalExperience,
              @SalaryExpectation, @AdditionalInfoApplicant, @Agreement, @IsApplicant, @IsApplicantApproved,
              @PFApplicable, @BonusApplicable, @ESICApplicable,
              @CompanyId, @ESICNO, @HusbandName, @PreferredLocation, @ReportHeadEcode, @ShiftId
          );

          DECLARE @NewEmployeeId BIGINT;
          SET @NewEmployeeId = SCOPE_IDENTITY();

          INSERT INTO tblEmployeeRole (EmployeeId, RoleId, AssignedOn, AssignedBy, LastUpdatedBy, LastUpdatedOn)
          VALUES (@NewEmployeeId, 3, GETDATE(), 'System', 'System', GETDATE());
      END
      ELSE
      BEGIN
          SET @NewEcode = '';
      END;
  END;


