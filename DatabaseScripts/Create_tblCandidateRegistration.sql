/*
    Candidate Registration Form (V2 Retail Graduate Academy) — storage table.
    Public pre-login form on the login page saves one row per submission here.
    Additive only: creates the table if missing. No data touched elsewhere.
*/
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID('dbo.tblCandidateRegistration', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblCandidateRegistration
    (
        Id                    INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tblCandidateRegistration PRIMARY KEY,

        -- Link 1 + Link 2
        ProgramApplyingFor    NVARCHAR(100)  NULL,   -- Retail Foundation / Store Management / Leadership Dev / Corporate Functional
        ModeOfTraining        NVARCHAR(50)   NULL,   -- Online / Offline-Classroom

        -- Personal Details
        FullName              NVARCHAR(200)  NOT NULL,
        MobileNumber          NVARCHAR(20)   NULL,
        WhatsAppNumber        NVARCHAR(20)   NULL,
        Email                 NVARCHAR(200)  NULL,
        DateOfBirth           DATE           NULL,
        Gender                NVARCHAR(20)   NULL,

        -- Educational Details
        HighestQualification  NVARCHAR(100)  NULL,   -- 12th / Diploma / Graduate / Post Graduate / Other
        Specialization        NVARCHAR(200)  NULL,
        CollegeUniversity     NVARCHAR(300)  NULL,
        PassingYear           NVARCHAR(10)   NULL,

        -- Training Preference
        PreferredLearningMode NVARCHAR(50)   NULL,   -- Online / Classroom

        -- Uploaded documents (relative paths under wwwroot)
        PhotoPath             NVARCHAR(500)  NULL,
        ResumePath            NVARCHAR(500)  NULL,
        AadhaarPath           NVARCHAR(500)  NULL,
        MarksheetPath         NVARCHAR(500)  NULL,

        -- Declaration
        AgreedToTerms         BIT            NOT NULL CONSTRAINT DF_tblCandReg_Agreed DEFAULT(0),

        CreatedOn             DATETIME       NOT NULL CONSTRAINT DF_tblCandReg_CreatedOn DEFAULT(GETDATE()),
        CreatedIp             NVARCHAR(50)   NULL
    );

    PRINT 'Created dbo.tblCandidateRegistration.';
END
ELSE
    PRINT 'dbo.tblCandidateRegistration already exists — no change.';
