/* =====================================================================
   Force UPPERCASE on Candidate (candidates + applicants) and tblEmployee
   Generated 2026-09-03. UPDATE-only + additive SELECT INTO backups.
   NOTHING is dropped, truncated or altered.
   EXCLUDED: passwords/hashes (BCrypt, case-sensitive -> would lock out all
   users), file paths / URLs / photo paths (case-sensitive on web server ->
   would break document links), FaceData (biometric vector), audit columns.
   INCLUDED: EMAIL ADDRESS (explicitly requested).
   ===================================================================== */
SET NOCOUNT ON;
BEGIN TRY
BEGIN TRAN;

/* ---------- dbo.Candidate ---------- */
IF OBJECT_ID('dbo.bk_Candidate_UpperCase_20260903') IS NULL
    SELECT * INTO dbo.bk_Candidate_UpperCase_20260903 FROM dbo.Candidate;   -- full row-level backup (new table)

UPDATE dbo.Candidate SET
    [TITLE] = UPPER([TITLE]),
    [FIRST NAME] = UPPER([FIRST NAME]),
    [MIDDLE NAME] = UPPER([MIDDLE NAME]),
    [LAST NAME] = UPPER([LAST NAME]),
    [HUSBAND NAME] = UPPER([HUSBAND NAME]),
    [FATHER NAME] = UPPER([FATHER NAME]),
    [MOTHER NAME] = UPPER([MOTHER NAME]),
    [DESIGNATION] = UPPER([DESIGNATION]),
    [LOCATION] = UPPER([LOCATION]),
    [GENDER] = UPPER([GENDER]),
    [DEPARTMENT] = UPPER([DEPARTMENT]),
    [UAN NO] = UPPER([UAN NO]),
    [PAN NO] = UPPER([PAN NO]),
    [AADHAR NO] = UPPER([AADHAR NO]),
    [NAME ON AADHAR] = UPPER([NAME ON AADHAR]),
    [udf1] = UPPER([udf1]),
    [PLACE OF BIRTH] = UPPER([PLACE OF BIRTH]),
    [PRESENT ADDRESS] = UPPER([PRESENT ADDRESS]),
    [PRESENT ADDRESS PIN CODE] = UPPER([PRESENT ADDRESS PIN CODE]),
    [PERMANENT ADDRESS] = UPPER([PERMANENT ADDRESS]),
    [[PERMANENT ADDRESS PIN CODE] = UPPER([[PERMANENT ADDRESS PIN CODE]),
    [EMP CODE] = UPPER([EMP CODE]),
    [APPLICANT CODE] = UPPER([APPLICANT CODE]),
    [WEEKLY OFF] = UPPER([WEEKLY OFF]),
    [MARITIAL STATUS] = UPPER([MARITIAL STATUS]),
    [MOBILE] = UPPER([MOBILE]),
    [EMAIL ADDRESS] = UPPER([EMAIL ADDRESS]),
    [NATIONALITY] = UPPER([NATIONALITY]),
    [RELIGION] = UPPER([RELIGION]),
    [BANK NAME] = UPPER([BANK NAME]),
    [A/C NO] = UPPER([A/C NO]),
    [BANK IFSC CODE] = UPPER([BANK IFSC CODE]),
    [REFERENCE1  OF LAST 3 COMPANY] = UPPER([REFERENCE1  OF LAST 3 COMPANY]),
    [CONTACT1 OF LAST 3 COMPANY] = UPPER([CONTACT1 OF LAST 3 COMPANY]),
    [REFERENCE2  OF LAST 3 COMPANY] = UPPER([REFERENCE2  OF LAST 3 COMPANY]),
    [CONTACT2 OF LAST 3 COMPANY] = UPPER([CONTACT2 OF LAST 3 COMPANY]),
    [REFERENCE3  OF LAST 3 COMPANY] = UPPER([REFERENCE3  OF LAST 3 COMPANY]),
    [CONTACT3 OF LAST 3 COMPANY] = UPPER([CONTACT3 OF LAST 3 COMPANY]),
    [REFERENCE4  OF LAST 3 COMPANY] = UPPER([REFERENCE4  OF LAST 3 COMPANY]),
    [CONTACT4 OF LAST 3 COMPANY] = UPPER([CONTACT4 OF LAST 3 COMPANY]),
    [REFERENCE5  OF LAST 3 COMPANY] = UPPER([REFERENCE5  OF LAST 3 COMPANY]),
    [CONTACT5 OF LAST 3 COMPANY] = UPPER([CONTACT5 OF LAST 3 COMPANY]),
    [FAMILY MEMBER Name] = UPPER([FAMILY MEMBER Name]),
    [FAMILY MEMBER Relation] = UPPER([FAMILY MEMBER Relation]),
    [COMPANY 1] = UPPER([COMPANY 1]),
    [COMPANY 2] = UPPER([COMPANY 2]),
    [COMPANY 3] = UPPER([COMPANY 3]),
    [WORK LOCATION] = UPPER([WORK LOCATION]),
    [POSITION HELD IN PREVIOUS COMPANY] = UPPER([POSITION HELD IN PREVIOUS COMPANY]),
    [From] = UPPER([From]),
    [To] = UPPER([To]),
    [In Hand Salary] = UPPER([In Hand Salary]),
    [LAST CTC(ANNUAL)] = UPPER([LAST CTC(ANNUAL)]),
    [HIGHEST QUALIFICATION] = UPPER([HIGHEST QUALIFICATION]),
    [BENEFICIARY ADDRESS] = UPPER([BENEFICIARY ADDRESS]),
    [PREV. EST NO.] = UPPER([PREV. EST NO.]),
    [REFERENCE] = UPPER([REFERENCE]),
    [ApplicantId] = UPPER([ApplicantId]),
    [AdditionalInfoApplicant] = UPPER([AdditionalInfoApplicant]),
    [ReportHeadEcode] = UPPER([ReportHeadEcode]),
    [Place] = UPPER([Place]),
    [SkillType] = UPPER([SkillType]),
    [DifferentlyAbledRemarks] = UPPER([DifferentlyAbledRemarks]),
    [DifferentlyAbledReason] = UPPER([DifferentlyAbledReason]),
    [Source] = UPPER([Source]),
    [ReferenceEmployee] = UPPER([ReferenceEmployee]),
    [PreferredLocation] = UPPER([PreferredLocation]),
    [CurrentLocation] = UPPER([CurrentLocation]),
    [AOCode] = UPPER([AOCode]),
    [StoreId] = UPPER([StoreId]),
    [BonusApplicable] = UPPER([BonusApplicable])
;
PRINT 'dbo.Candidate uppercased: ' + CAST(@@ROWCOUNT AS varchar(20)) + ' rows, 71 columns';

/* ---------- dbo.tblEmployee ---------- */
IF OBJECT_ID('dbo.bk_tblEmployee_UpperCase_20260903') IS NULL
    SELECT * INTO dbo.bk_tblEmployee_UpperCase_20260903 FROM dbo.tblEmployee;   -- full row-level backup (new table)

UPDATE dbo.tblEmployee SET
    [TITLE] = UPPER([TITLE]),
    [FULL NAME] = UPPER([FULL NAME]),
    [FATHER'S NAME] = UPPER([FATHER'S NAME]),
    [MOTHER'S NAME] = UPPER([MOTHER'S NAME]),
    [GENDER] = UPPER([GENDER]),
    [PAN NO] = UPPER([PAN NO]),
    [AADHAR NO] = UPPER([AADHAR NO]),
    [NAME ON ADHAR] = UPPER([NAME ON ADHAR]),
    [PLACE OF BIRTH] = UPPER([PLACE OF BIRTH]),
    [PRESENT ADDRESS] = UPPER([PRESENT ADDRESS]),
    [PRESENT ADDRESS PIN CODE] = UPPER([PRESENT ADDRESS PIN CODE]),
    [PERMANENT ADDRESS] = UPPER([PERMANENT ADDRESS]),
    [MARITIAL STATUS] = UPPER([MARITIAL STATUS]),
    [MOBILE] = UPPER([MOBILE]),
    [EMAIL ADDRESS] = UPPER([EMAIL ADDRESS]),
    [NATIONALITY] = UPPER([NATIONALITY]),
    [RELIGION] = UPPER([RELIGION]),
    [BANK NAME] = UPPER([BANK NAME]),
    [A/C NO] = UPPER([A/C NO]),
    [BANK IFSC CODE] = UPPER([BANK IFSC CODE]),
    [REFERENCE1  OF LAST 3 COMPANY] = UPPER([REFERENCE1  OF LAST 3 COMPANY]),
    [CONTACT1 OF LAST 3 COMPANY] = UPPER([CONTACT1 OF LAST 3 COMPANY]),
    [REFERENCE2  OF LAST 3 COMPANY1] = UPPER([REFERENCE2  OF LAST 3 COMPANY1]),
    [CONTACT2 OF LAST 3 COMPANY1] = UPPER([CONTACT2 OF LAST 3 COMPANY1]),
    [REFERENCE3  OF LAST 3 COMPANY11] = UPPER([REFERENCE3  OF LAST 3 COMPANY11]),
    [CONTACT3 OF LAST 3 COMPANY11] = UPPER([CONTACT3 OF LAST 3 COMPANY11]),
    [REFERENCE4  OF LAST 3 COMPANY11] = UPPER([REFERENCE4  OF LAST 3 COMPANY11]),
    [CONTACT4 OF LAST 3 COMPANY11] = UPPER([CONTACT4 OF LAST 3 COMPANY11]),
    [REFERENCE5  OF LAST 3 COMPANY111] = UPPER([REFERENCE5  OF LAST 3 COMPANY111]),
    [CONTACT5 OF LAST 3 COMPANY111] = UPPER([CONTACT5 OF LAST 3 COMPANY111]),
    [FAMILY MEMBER Name] = UPPER([FAMILY MEMBER Name]),
    [FAMILY MEMBER Relation] = UPPER([FAMILY MEMBER Relation]),
    [COMPANY 1] = UPPER([COMPANY 1]),
    [COMPANY 2] = UPPER([COMPANY 2]),
    [COMPANY 3] = UPPER([COMPANY 3]),
    [WORK LOCATION] = UPPER([WORK LOCATION]),
    [POSITION HELD IN PREVIOUS COMPANY] = UPPER([POSITION HELD IN PREVIOUS COMPANY]),
    [HIGHEST QUALIFICATION] = UPPER([HIGHEST QUALIFICATION]),
    [UDF1] = UPPER([UDF1]),
    [UDF2] = UPPER([UDF2]),
    [UDF3] = UPPER([UDF3]),
    [UDF4] = UPPER([UDF4]),
    [UDF5] = UPPER([UDF5]),
    [UDF6] = UPPER([UDF6]),
    [UDF7] = UPPER([UDF7]),
    [Ecode] = UPPER([Ecode]),
    [FirstName] = UPPER([FirstName]),
    [MiddleName] = UPPER([MiddleName]),
    [LastName] = UPPER([LastName]),
    [ReportHeadEcode] = UPPER([ReportHeadEcode]),
    [MOBILE2] = UPPER([MOBILE2]),
    [UAN NO] = UPPER([UAN NO]),
    [PERMANENT ADDRESS PIN CODE] = UPPER([PERMANENT ADDRESS PIN CODE]),
    [EMP CODE] = UPPER([EMP CODE]),
    [APPLICANT CODE] = UPPER([APPLICANT CODE]),
    [WEEKLY OFF] = UPPER([WEEKLY OFF]),
    [REFERENCE] = UPPER([REFERENCE]),
    [ApplicantId] = UPPER([ApplicantId]),
    [AdditionalInfoApplicant] = UPPER([AdditionalInfoApplicant]),
    [BENEFICIARY_ADDRESS] = UPPER([BENEFICIARY_ADDRESS]),
    [ActiveInActiveRemarks] = UPPER([ActiveInActiveRemarks]),
    [ESICNO] = UPPER([ESICNO]),
    [Husband Name] = UPPER([Husband Name]),
    [AttendanceType] = UPPER([AttendanceType]),
    [PreferredLocation] = UPPER([PreferredLocation]),
    [AOCode] = UPPER([AOCode]),
    [ContractorCode] = UPPER([ContractorCode]),
    [BonusApplicable] = UPPER([BonusApplicable])
;
PRINT 'dbo.tblEmployee uppercased: ' + CAST(@@ROWCOUNT AS varchar(20)) + ' rows, 68 columns';

COMMIT TRAN;
PRINT 'DONE - committed';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRAN;
    PRINT 'ROLLED BACK: ' + ERROR_MESSAGE();
    THROW;
END CATCH

