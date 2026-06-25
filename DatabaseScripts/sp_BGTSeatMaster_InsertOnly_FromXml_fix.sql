-- Seat-number uniqueness fix: when DepartmentCode/DesignationCode is blank, fall back to the
-- numeric Dept/Desig Id so each (LOC, Dept, Desig) gets a distinct prefix + independent running
-- series (prevents duplicate "RH01---0001"). Coded designations are unchanged.
CREATE OR ALTER PROCEDURE dbo.sp_BGTSeatMaster_InsertOnly_FromXml
  @RowsXml xml
AS
BEGIN
  SET NOCOUNT ON;
  SET XACT_ABORT ON;

  IF OBJECT_ID('tempdb..#Assigned') IS NOT NULL DROP TABLE #Assigned;

  BEGIN TRY
    DECLARE @Rows TABLE
    (
        idx int NOT NULL, LOC_CODE nvarchar(50) NOT NULL, DEPT_ID int NOT NULL, DESIG_ID int NOT NULL,
        REP_MGR_DESIG_ID int NULL, SALARY_BGT decimal(18,2) NULL, ORG_CHART nvarchar(200) NULL, ACTIVE bit NULL,
        SUB1 nvarchar(255) NULL, SUB2 nvarchar(255) NULL, SUB3 nvarchar(255) NULL
    );

    INSERT INTO @Rows (idx, LOC_CODE, DEPT_ID, DESIG_ID, REP_MGR_DESIG_ID, SALARY_BGT, ORG_CHART, ACTIVE, SUB1, SUB2, SUB3)
    SELECT
        R.value('@idx','int'), R.value('@loc','nvarchar(50)'), R.value('@dept_id','int'), R.value('@desig_id','int'),
        NULLIF(R.value('@rep_mgr_desig_id','int'), 0),
        CASE WHEN NULLIF(R.value('@salary','nvarchar(50)'),'') IS NULL THEN NULL ELSE TRY_CONVERT(decimal(18,2), R.value('@salary','nvarchar(50)')) END,
        NULLIF(R.value('@org','nvarchar(200)'), ''),
        CASE UPPER(R.value('@active','nvarchar(10)')) WHEN 'ACTIVE' THEN CAST(1 AS bit) WHEN 'INACTIVE' THEN CAST(0 AS bit) ELSE NULL END,
        NULLIF(R.value('@sub1','nvarchar(255)'), ''), NULLIF(R.value('@sub2','nvarchar(255)'), ''), NULLIF(R.value('@sub3','nvarchar(255)'), '')
    FROM @RowsXml.nodes('/rows/row') AS T(R);

    IF NOT EXISTS (SELECT 1 FROM @Rows) RETURN;

    BEGIN TRANSACTION;
    SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;

    ;WITH RC AS (
        SELECT r.*,
               D.DepartmentName,
               G.DesignationName,
               ISNULL(NULLIF(LTRIM(RTRIM(D.DepartmentCode)),''), CONVERT(varchar(20), r.DEPT_ID))  AS DeptCodeEff,
               ISNULL(NULLIF(LTRIM(RTRIM(G.DesignationCode)),''), CONVERT(varchar(20), r.DESIG_ID)) AS DesigCodeEff,
               CASE WHEN D.DepartmentId IS NULL OR G.DesignationId IS NULL THEN 1 ELSE 0 END AS Unknown
        FROM @Rows r
        LEFT JOIN dbo.tblDepartment D ON D.DepartmentId = r.DEPT_ID
        LEFT JOIN dbo.tblDesignation G ON G.DesignationId = r.DESIG_ID
    ),
    WithPrefix AS (
        SELECT rc.*, CONCAT(rc.LOC_CODE, '-', rc.DeptCodeEff, '-', rc.DesigCodeEff, '-') AS Prefix FROM RC rc
    ),
    Prefixes AS ( SELECT DISTINCT Prefix FROM WithPrefix ),
    Existing AS (
        SELECT p.Prefix, MAX(TRY_CONVERT(int, RIGHT(m.SEAT_MASTER_NO,4))) AS MaxSeries
        FROM Prefixes p
        LEFT JOIN dbo.BGTSEATMaster m WITH (UPDLOCK, HOLDLOCK)
          ON m.SEAT_MASTER_NO LIKE REPLACE(REPLACE(p.Prefix,'[','[[]'),'_','[_]') + '%'
        GROUP BY p.Prefix
    ),
    Ranked AS ( SELECT w.*, ROW_NUMBER() OVER (PARTITION BY w.Prefix ORDER BY w.idx) AS rn FROM WithPrefix w )
    SELECT R.idx, R.LOC_CODE, R.DEPT_ID, R.DESIG_ID, R.REP_MGR_DESIG_ID, R.SALARY_BGT, R.ORG_CHART, R.ACTIVE,
           R.SUB1, R.SUB2, R.SUB3, R.DepartmentName, R.DesignationName, R.Unknown, R.Prefix,
           ISNULL(E.MaxSeries, 0) + R.rn AS FinalSeries
    INTO #Assigned
    FROM Ranked R JOIN Existing E ON E.Prefix = R.Prefix;

    DECLARE @Errors TABLE ( idx int NULL, err nvarchar(300) NOT NULL );
    INSERT INTO @Errors(idx, err) SELECT idx, 'Unknown DepartmentId or DesignationId in payload.' FROM #Assigned WHERE Unknown = 1;
    INSERT INTO @Errors(idx, err) SELECT idx, 'Series would exceed 9999 for this (LOC,DEPT,DESIG) combo.' FROM #Assigned WHERE FinalSeries > 9999;
    INSERT INTO @Errors(idx, err) SELECT idx, 'ACTIVE must be Active/Inactive.' FROM @Rows WHERE ACTIVE IS NULL;

    ;WITH SeatNos AS (
      SELECT A.idx, CONCAT(A.Prefix, RIGHT(CONCAT('0000', A.FinalSeries), 4)) AS SeatMasterNo FROM #Assigned A
    )
    INSERT INTO @Errors(idx, err)
    SELECT s.idx, 'SEAT_MASTER_NO already exists: ' + s.SeatMasterNo
    FROM SeatNos s JOIN dbo.BGTSEATMaster t WITH (UPDLOCK, HOLDLOCK) ON t.SEAT_MASTER_NO = s.SeatMasterNo;

    IF EXISTS (SELECT 1 FROM @Errors)
    BEGIN
      IF OBJECT_ID('tempdb..#Assigned') IS NOT NULL DROP TABLE #Assigned;
      ROLLBACK TRANSACTION;
      SELECT CONVERT(nvarchar(20), idx) AS idx, err FROM @Errors ORDER BY TRY_CONVERT(int, idx), err;
      THROW 51000, 'Validation failed. All changes rolled back.', 1;
    END

    INSERT INTO dbo.BGTSEATMaster
    ( LOC_CODE, DEPT_SNO, DEPARTMENT, DESG_SNO, DESIGNATION, SEAT_MASTER_NO, SALARY_BGT, ORG_CHART, REPORTING_MANAGER, ACTIVE, SubDepartment1, SubDepartment2, SubDepartment3 )
    SELECT A.LOC_CODE, CONVERT(varchar(20), A.DEPT_ID), A.DepartmentName, CONVERT(varchar(20), A.DESIG_ID), A.DesignationName,
           CONCAT(A.Prefix, RIGHT(CONCAT('0000', A.FinalSeries), 4)), A.SALARY_BGT, A.ORG_CHART, CONVERT(varchar(20), A.REP_MGR_DESIG_ID), A.ACTIVE, A.SUB1, A.SUB2, A.SUB3
    FROM #Assigned A;

    ;WITH Final AS (
      SELECT A.idx, CONCAT(A.Prefix, RIGHT(CONCAT('0000', A.FinalSeries), 4)) AS SEAT_MASTER_NO FROM #Assigned A
    )
    SELECT CONVERT(nvarchar(20), idx) AS idx, SEAT_MASTER_NO FROM Final ORDER BY TRY_CONVERT(int, idx);

    DROP TABLE #Assigned;
    COMMIT TRANSACTION;
  END TRY
  BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    IF OBJECT_ID('tempdb..#Assigned') IS NOT NULL DROP TABLE #Assigned;
    THROW;
  END CATCH
END
