IF OBJECT_ID('dbo.tblAttendancePunchLocation') IS NULL
BEGIN
    CREATE TABLE dbo.tblAttendancePunchLocation(
    [Id] bigint IDENTITY(1,1) NOT NULL,
    [ECode] nvarchar(50) NULL,
    [EmployeeName] nvarchar(255) NULL,
    [AttendanceDate] date NULL,
    [PunchNo] varchar(10) NULL,
    [PunchTime] varchar(20) NULL,
    [PunchLocation] nvarchar(200) NULL,
    [PunchSTCode] nvarchar(50) NULL,
    [CreatedOn] datetime NOT NULL,
    CONSTRAINT PK_tblAttendancePunchLocation PRIMARY KEY CLUSTERED (Id)
    );
    CREATE NONCLUSTERED INDEX IX_APL_ECode_Date ON dbo.tblAttendancePunchLocation(ECode, AttendanceDate);
    CREATE NONCLUSTERED INDEX IX_APL_Date_incl  ON dbo.tblAttendancePunchLocation(AttendanceDate) INCLUDE (ECode, PunchNo, PunchLocation);
END
