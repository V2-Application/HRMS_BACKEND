CREATE OR ALTER PROCEDURE dbo.usp_RMRegularizationApprovalPendingGapReport
    @AsOfDate DATE = NULL    -- defaults to YESTERDAY ("today's" report uses data through yesterday)
AS
BEGIN
    SET NOCOUNT ON;

    /*
      LOC_TD/MTD RM REGULARIZATION APPROVAL PENDING GAP REPORT  (read-only)
      One row per Reporting Manager (RM) who has attendance-regularization requests still PENDING
      at the RM (manager) level.  Source = dbo.tblAttendanceRegularizationRequest where
      ManagerApprovalStatusId = 4 ('Pending')  -- manager-approval-pending only.
      RM = tblAttendanceRegularizationRequest.ReportingManagerId (the manager who must approve).
        LOC CD / LOC NM / LOC TYPE / LOC STATUS  -> the RM's location
        RM EMP. CODE / RM EMP NM / DEPT. / SUB.-DEPT. / DESGN. -> the RM (manager)
        TD   = pending RM approvals for RequestDate = @AsOfDate (yesterday)
        MTD  = pending RM approvals for the current pay cycle through @AsOfDate
        RCA / ATR / HR REMARKS -> left blank (manual follow-up columns, per the template)
      Pay cycle = 26th of prev month .. 25th of current; cycle start = most recent 26th on/before @AsOfDate.
      "Today's" download uses data THROUGH YESTERDAY (@AsOfDate defaults to GETDATE()-1).
      Marks/changes nothing.
    */

    DECLARE @ToDate     DATE = ISNULL(@AsOfDate, DATEADD(DAY, -1, CAST(GETDATE() AS DATE)));
    DECLARE @CycleStart DATE =
        CASE WHEN DAY(@ToDate) >= 26
             THEN DATEFROMPARTS(YEAR(@ToDate), MONTH(@ToDate), 26)
             ELSE DATEADD(MONTH, -1, DATEFROMPARTS(YEAR(@ToDate), MONTH(@ToDate), 26))
        END;

    ;WITH pend AS (
        -- pending RM-level regularization requests in the current cycle, by Reporting Manager
        SELECT r.ReportingManagerId AS RMEmployeeId,
               SUM(CASE WHEN CAST(r.RequestDate AS date) = @ToDate THEN 1 ELSE 0 END)                          AS TD,
               SUM(CASE WHEN CAST(r.RequestDate AS date) BETWEEN @CycleStart AND @ToDate THEN 1 ELSE 0 END)    AS MTD
        FROM dbo.tblAttendanceRegularizationRequest r WITH (NOLOCK)
        WHERE r.ManagerApprovalStatusId = 4                                  -- Pending RM approval
          AND CAST(r.RequestDate AS date) BETWEEN @CycleStart AND @ToDate
          AND r.ReportingManagerId IS NOT NULL
        GROUP BY r.ReportingManagerId
    )
    SELECT
        l.STCode       AS [LOC CD],
        l.LocationName AS [LOC NM],
        CASE
            WHEN l.STCode = 'RH01' OR UPPER(LTRIM(RTRIM(l.LocationName))) LIKE 'HO-NEW%' THEN 'HO new'
            WHEN l.STCode = 'RD04' OR UPPER(LTRIM(RTRIM(l.LocationName))) LIKE 'HO-OLD%' THEN 'Old HO'
            WHEN l.STCode = 'RH02' OR UPPER(LTRIM(RTRIM(l.LocationName))) LIKE '%CENTRAL%' THEN 'Central'
            WHEN l.STCode IN ('DW01','DH24') THEN 'DC'
            WHEN UPPER(LTRIM(RTRIM(l.LocationName))) LIKE '%DC%' OR UPPER(LTRIM(RTRIM(l.LocationName))) LIKE '%HUB%' THEN 'Hub'
            ELSE 'Store'
        END            AS [LOC TYPE],
        CASE WHEN l.IsActive = 1 THEN 'Active' ELSE 'UPC' END AS [LOC STATUS],
        rm.ECode              AS [RM EMP. CODE],
        rm.[FULL NAME]        AS [RM EMP NM],
        d.DepartmentName      AS [DEPT.],
        sd1.SubDepartmentName AS [SUB.-DEPT.],
        dg.DesignationName    AS [DESGN.],
        p.TD                  AS [TD],
        p.MTD                 AS [MTD],
        CAST(NULL AS varchar(200)) AS [RCA],
        CAST(NULL AS varchar(200)) AS [ATR],
        CAST(NULL AS varchar(200)) AS [HR REMARKS]
    FROM pend p
    LEFT JOIN dbo.tblEmployee rm WITH (NOLOCK) ON rm.EmployeeId = p.RMEmployeeId
    LEFT JOIN dbo.tblLocation    l   WITH (NOLOCK) ON l.LocationId     = rm.LocationId
    LEFT JOIN dbo.tblDepartment  d   WITH (NOLOCK) ON d.DepartmentId   = rm.DepartmentId
    LEFT JOIN dbo.tblDesignation dg  WITH (NOLOCK) ON dg.DesignationId = rm.DesignationId
    LEFT JOIN dbo.tblSubDepartment sd1 WITH (NOLOCK) ON sd1.SubDepartmentId = rm.SubDepartmentId1
    WHERE p.MTD > 0
    ORDER BY l.STCode, rm.ECode;

    SET NOCOUNT OFF;
END;
