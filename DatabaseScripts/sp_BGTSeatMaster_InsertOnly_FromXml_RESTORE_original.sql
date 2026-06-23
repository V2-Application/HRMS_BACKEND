-- Restore the ORIGINAL sp_BGTSeatMaster_InsertOnly_FromXml (reverts the seat-number fix).
CREATE OR ALTER PROCEDURE dbo.sp_BGTSeatMaster_InsertOnly_FromXml
  @RowsXml xml
AS
BEGIN
  SET NOCOUNT ON;
  SET XACT_ABORT ON;

  IF OBJECT_ID('tempdb..#Assigned') IS NOT NULL DROP TABLE #Assigned;
  IF OBJECT_ID('tempdb..#AssignedWithCodes') IS NOT NULL DROP TABLE #AssignedWithCodes;

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

    ;WITH Combos AS ( SELECT DISTINCT LOC_CODE, DEPT_ID, DESIG_ID FROM @Rows ),
    Existing AS (
        SELECT c.LOC_CODE, c.DEPT_ID, c.DESIG_ID, MAX(TRY_CONVERT(int, RIGHT(m.SEAT_MASTER_NO,4))) AS MaxSeries
        FROM Combos c
        LEFT JOIN dbo.BGTSEATMaster m WITH (UPDLOCK, HOLDLOCK)
          ON m.LOC_CODE = c.LOC_CODE AND m.DEPT_SNO = CONVERT(varchar(20), c.DEPT_ID) AND m.DESG_SNO = CONVERT(varchar(20), c.DESIG_ID)
        GROUP BY c.LOC_CODE, c.DEPT_ID, c.DESIG_ID
    ),
    Ranked AS ( SELECT r.*, ROW_NUMBER() OVER (PARTITION BY r.LOC_CODE, r.DEPT_ID, r.DESIG_ID ORDER BY r.idx) AS rn FROM @Rows r ),
    AssignedCTE AS (
        SELECT R.idx, R.LOC_CODE, R.DEPT_ID, R.DESIG_ID, R.REP_MGR_DESIG_ID, R.SALARY_BGT, R.ORG_CHART, R.ACTIVE,
               R.SUB1, R.SUB2, R.SUB3, ISNULL(E.MaxSeries, 0) + R.rn AS FinalSeries
        FROM Ranked R JOIN Existing E ON E.LOC_CODE = R.LOC_CODE AND E.DEPT_ID = R.DEPT_ID AND E.DESIG_ID = R.DESIG_ID
    )
    SELECT * INTO #Assigned FROM AssignedCTE;

    DECLARE @Errors TABLE ( idx int NULL, err nvarchar(300) NOT NULL );
    INSERT INTO @Errors(idx, err)
    SELECT A.idx, 'Unknown DepartmentId or DesignationId in payload.'
    FROM #Assigned A LEFT JOIN dbo.tblDepartment D ON D.DepartmentId = A.DEPT_ID LEFT JOIN dbo.tblDesignation G ON G.DesignationId = A.DESIG_ID
    WHERE D.DepartmentId IS NULL OR G.DesignationId IS NULL;
    INSERT INTO @Errors(idx, err) SELECT idx, 'Series would exceed 9999 for this (LOC,DEPT,DESIG) combo.' FROM #Assigned WHERE FinalSeries > 9999;
    INSERT INTO @Errors(idx, err) SELECT idx, 'ACTIVE must be Active/Inactive.' FROM @Rows WHERE ACTIVE IS NULL;

    SELECT A.*, D.DepartmentCode, G.DesignationCode INTO #AssignedWithCodes
    FROM #Assigned A Left JOIN dbo.tblDepartment D ON D.DepartmentId = A.DEPT_ID Left JOIN dbo.tblDesignation G ON G.DesignationId = A.DESIG_ID;

    ;with SeatNos AS (
      SELECT A.idx, CONCAT(A.LOC_CODE,'-',A.DepartmentCode,'-',A.DesignationCode,'-',RIGHT(CONCAT('0000',A.FinalSeries),4)) AS SeatMasterNo FROM #AssignedWithCodes A
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

    DECLARE @Out TABLE ( idx nvarchar(20) NOT NULL, SEAT_MASTER_NO nvarchar(100) NOT NULL );

    ;WITH InsertRows AS (
     SELECT A.*, CONCAT(A.LOC_CODE,'-',A.DepartmentCode,'-',A.DesignationCode,'-',RIGHT(CONCAT('0000',A.FinalSeries),4)) AS SeatMasterNo FROM #AssignedWithCodes A
    )
    INSERT INTO dbo.BGTSEATMaster
    ( LOC_CODE, DEPT_SNO, DEPARTMENT, DESG_SNO, DESIGNATION, SEAT_MASTER_NO, SALARY_BGT, ORG_CHART, REPORTING_MANAGER, ACTIVE, SubDepartment1, SubDepartment2, SubDepartment3 )
    SELECT ir.LOC_CODE, CONVERT(varchar(20), ir.DEPT_ID), D.DepartmentName, CONVERT(varchar(20), ir.DESIG_ID), G.DesignationName,
           ir.SeatMasterNo, ir.SALARY_BGT, ir.ORG_CHART, CONVERT(varchar(20), ir.REP_MGR_DESIG_ID), ir.ACTIVE, ir.SUB1, ir.SUB2, ir.SUB3
    FROM InsertRows ir JOIN dbo.tblDepartment D ON D.DepartmentId = ir.DEPT_ID JOIN dbo.tblDesignation G ON G.DesignationId = ir.DESIG_ID;

    DROP TABLE #Assigned; Drop Table #AssignedWithCodes;
    COMMIT TRANSACTION;
    SELECT idx, SEAT_MASTER_NO FROM @Out ORDER BY TRY_CONVERT(int, idx);
  END TRY
  BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    IF OBJECT_ID('tempdb..#Assigned') IS NOT NULL DROP TABLE #Assigned;
    IF OBJECT_ID('tempdb..#AssignedWithCodes') IS NOT NULL DROP TABLE #AssignedWithCodes;
    THROW;
  END CATCH
END
