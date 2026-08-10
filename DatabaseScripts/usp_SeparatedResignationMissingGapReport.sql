CREATE OR ALTER PROCEDURE dbo.usp_SeparatedResignationMissingGapReport
    @AsOfDate DATE = NULL    -- kept for signature compatibility (not used)
AS
BEGIN
    SET NOCOUNT ON;

    /*
      SEPERATED BUT RESIGNATION MISSING  (read-only)
      Driven from the EMPLOYEE MASTER (tblEmployee): every separated employee
      (tblEmployee.IsActive = 0) who has NO resignation recorded - i.e. no non-revoked
      tblEmployeeSepration row carrying a RESIGNATION DATE. These are people who left
      but whose resignation was never entered (the gap).

      Columns follow the reference template "SEPERATED BUT RESIGNATION MISSING.xlsb":
        LOC-CD / LOC-NM / EMP-CD / EMP-NM / DOJ / DEPARTMENT / DESIGNATION /
        BGT LAST-DAY (= tblEmployee.DateOfLeft, from the master) /
        NOTICEPERIOD-DAYS / RESIGNATIONTYPENAME / RESIGNATION DATE / REMARKS / TYPE
      For missing-resignation rows the resignation columns are blank (that is the gap).
      Store-login accounts (ECode = a store STCode) are excluded.
    */

    ;WITH sep AS (
        -- latest non-revoked separation record (if any) - only to surface notice/remarks
        SELECT s.EmployeeId, s.NoticePeriod, s.ResignationTypeId, s.ResignationDate, s.Remarks,
               ROW_NUMBER() OVER (PARTITION BY s.EmployeeId ORDER BY s.EmployeeSeprationId DESC) AS rn
        FROM dbo.tblEmployeeSepration s WITH (NOLOCK)
        WHERE ISNULL(s.IsRevoked, 0) = 0
    )
    SELECT
        l.STCode              AS [LOC-CD],
        l.LocationName        AS [LOC-NM],
        e.Ecode               AS [EMP-CD],
        e.[FULL NAME]         AS [EMP-NM],
        e.DOJ                 AS [DOJ],
        d.DepartmentName      AS [DEPARTMENT],
        dg.DesignationName    AS [DESIGNATION],
        e.DateOfLeft          AS [BGT LAST-DAY],
        sp.NoticePeriod       AS [NOTICEPERIOD-DAYS],
        trt.ResignationTypeName AS [RESIGNATIONTYPENAME],
        sp.ResignationDate    AS [RESIGNATION DATE],
        sp.Remarks            AS [REMARKS],
        CAST('RESIGNATION' AS varchar(20)) AS [TYPE]
    FROM dbo.tblEmployee e WITH (NOLOCK)
    LEFT JOIN dbo.tblLocation    l   WITH (NOLOCK) ON l.LocationId     = e.LocationId
    LEFT JOIN dbo.tblDepartment  d   WITH (NOLOCK) ON d.DepartmentId   = e.DepartmentId
    LEFT JOIN dbo.tblDesignation dg  WITH (NOLOCK) ON dg.DesignationId = e.DesignationId
    LEFT JOIN sep sp ON sp.EmployeeId = e.EmployeeId AND sp.rn = 1
    LEFT JOIN dbo.tblResignationType trt WITH (NOLOCK) ON trt.ResignationTypeId = sp.ResignationTypeId
    WHERE e.IsActive = 0                              -- separated (from the master)
      AND NOT EXISTS (                                -- exclude F&F Completed (Pending/Processing stay)
            SELECT 1 FROM dbo.FNF_Header h WITH (NOLOCK)
            JOIN dbo.FNF_Payment pmt WITH (NOLOCK) ON pmt.FNFId = h.FNFId
            WHERE h.EmployeeId = e.EmployeeId
              AND (pmt.Status IN ('Paid','FNF DONE') OR pmt.AmountPaid > 0)
          )
      -- RESIGNATION MISSING: no non-revoked separation that carries a resignation date
      AND NOT EXISTS (
            SELECT 1 FROM dbo.tblEmployeeSepration s2 WITH (NOLOCK)
            WHERE s2.EmployeeId = e.EmployeeId
              AND ISNULL(s2.IsRevoked, 0) = 0
              AND s2.ResignationDate IS NOT NULL)
      -- exclude store-login accounts (ECode is actually a store code, not a person)
      AND NOT EXISTS (SELECT 1 FROM dbo.tblLocation lx WITH (NOLOCK) WHERE lx.STCode = e.Ecode)
    ORDER BY l.STCode, e.Ecode;

    SET NOCOUNT OFF;
END;
