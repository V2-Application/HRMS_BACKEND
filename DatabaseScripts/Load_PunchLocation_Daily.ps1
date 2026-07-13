<#
    Load_PunchLocation_Daily.ps1
    ------------------------------------------------------------------------------
    DEV-ONLY daily loader for dbo.tblAttendancePunchLocation (on DEV / KARMA).

    Punch device locations live in the SmartOffice biometric log ATTLOG, reachable
    ONLY via the linked server that exists on PROD. DEV has no linked server, so this
    script bridges: it RESOLVES the locations on PROD (read-only) and INSERTS the
    result into the DEV table.

    Safety:
      * PROD is READ-ONLY (a single SELECT; ATTLOG pulled into a session #temp).
      * DEV write is ADDITIVE ONLY — INSERT ... WHERE NOT EXISTS. No UPDATE/DELETE/TRUNCATE.
      * Idempotent: re-running for the same day inserts nothing new.
      * Rolling window (default 3 days) self-heals punches whose ATTLOG row synced late.

    Schedule: SQL Server Agent -> New Job -> Step type "PowerShell" (or Windows Task
    Scheduler) -> run daily, e.g. 02:00. Command:
        powershell -ExecutionPolicy Bypass -File "D:\Jobs\Load_PunchLocation_Daily.ps1"
    Optional backfill of N days:
        powershell -ExecutionPolicy Bypass -File "...\Load_PunchLocation_Daily.ps1" -Days 30
    ------------------------------------------------------------------------------
#>
param(
    [int]$Days = 3
)

$ErrorActionPreference = 'Stop'

# PROD: read-only source (ATTLOG linked server + punch-range + Biomax map all live here).
$prod = "Data Source=192.168.151.28\hrms;Initial Catalog=HRMS;User ID=sa_hrms;Password=CIHTY5pBmRRwjAw;TrustServerCertificate=True"
# DEV (KARMA): destination table lives here.
$dev  = "Data Source=192.168.151.27\KARMA;Initial Catalog=HRMS;User ID=nikhil;Password=Vrl@12345;TrustServerCertificate=True"

# Rolling window [Start, End) — End is exclusive (through end of yesterday).
$start = (Get-Date).Date.AddDays(-$Days).ToString('yyyy-MM-dd')
$end   = (Get-Date).Date.ToString('yyyy-MM-dd')
Write-Output "[$(Get-Date -Format s)] Punch-location load window: $start (incl) .. $end (excl)"

# ---- Resolve on PROD (read-only): unpivot 12 punches, match to ATTLOG, map to ST Code ----
$resolveSql = @'
SET NOCOUNT ON;

SELECT Employeecode, Logdatetime, Location
INTO #att
FROM [192.168.151.25].[SmartOfficedb].[dbo].ATTLOG
WHERE Logdatetime >= @Start AND Logdatetime < @End;
CREATE CLUSTERED INDEX IX_att ON #att(Employeecode, Logdatetime);

;WITH src AS (
    SELECT * FROM dbo.tbl_fn_GetMonthlyPunchesRange_productionnewnick_test WITH (NOLOCK)
    WHERE AttendanceDate >= @Start AND AttendanceDate < @End
),
punches AS (
    SELECT s.ECode, s.EmployeeName, CAST(s.AttendanceDate AS date) AS AttendanceDate, v.PunchNo, v.PunchTime
    FROM src s
    CROSS APPLY (VALUES
        ('Punch1',s.Punch1),('Punch2',s.Punch2),('Punch3',s.Punch3),('Punch4',s.Punch4),
        ('Punch5',s.Punch5),('Punch6',s.Punch6),('Punch7',s.Punch7),('Punch8',s.Punch8),
        ('Punch9',s.Punch9),('Punch10',s.Punch10),('Punch11',s.Punch11),('Punch12',s.Punch12)
    ) v(PunchNo, PunchTime)
    WHERE v.PunchTime IS NOT NULL AND v.PunchTime <> '' AND v.PunchTime <> '00:00:00'
),
resolved AS (
    SELECT pn.ECode, pn.EmployeeName, pn.AttendanceDate, pn.PunchNo, pn.PunchTime, a.Location AS PunchLocation
    FROM punches pn
    INNER JOIN #att a
        ON a.Employeecode COLLATE DATABASE_DEFAULT = pn.ECode COLLATE DATABASE_DEFAULT
       AND CAST(a.Logdatetime AS date) = pn.AttendanceDate
       AND CONVERT(varchar(8), a.Logdatetime, 108) = pn.PunchTime
)
SELECT r.ECode, r.EmployeeName, r.AttendanceDate, r.PunchNo, r.PunchTime, r.PunchLocation,
       bm.STCode AS PunchSTCode
FROM resolved r
LEFT JOIN dbo.tblBiomaxAttendanceLocationMap bm
    ON bm.DeviceLocation COLLATE DATABASE_DEFAULT = r.PunchLocation COLLATE DATABASE_DEFAULT
   AND bm.IsDeleted = 0;

DROP TABLE #att;
'@

$devCn = $null; $prodCn = $null
try {
    # Open DEV, build a session staging table.
    $devCn = New-Object System.Data.SqlClient.SqlConnection $dev
    $devCn.Open()
    $stgCmd = $devCn.CreateCommand()
    $stgCmd.CommandText = @'
IF OBJECT_ID('tempdb..#stg') IS NOT NULL DROP TABLE #stg;
CREATE TABLE #stg(
    ECode nvarchar(100) NULL, EmployeeName nvarchar(510) NULL, AttendanceDate date NULL,
    PunchNo varchar(10) NULL, PunchTime varchar(20) NULL,
    PunchLocation nvarchar(400) NULL, PunchSTCode nvarchar(100) NULL);
'@
    [void]$stgCmd.ExecuteNonQuery()

    # Open PROD, stream the resolved rows straight into DEV #stg via bulk copy.
    $prodCn = New-Object System.Data.SqlClient.SqlConnection $prod
    $prodCn.Open()
    $rCmd = $prodCn.CreateCommand()
    $rCmd.CommandText = $resolveSql
    $rCmd.CommandTimeout = 1800
    [void]$rCmd.Parameters.AddWithValue('@Start', $start)
    [void]$rCmd.Parameters.AddWithValue('@End',   $end)
    $reader = $rCmd.ExecuteReader()

    $bulk = New-Object System.Data.SqlClient.SqlBulkCopy($devCn)
    $bulk.DestinationTableName = '#stg'
    $bulk.BulkCopyTimeout = 0
    $bulk.BatchSize = 10000
    foreach ($col in 'ECode','EmployeeName','AttendanceDate','PunchNo','PunchTime','PunchLocation','PunchSTCode') {
        [void]$bulk.ColumnMappings.Add($col, $col)
    }
    $bulk.WriteToServer($reader)
    $reader.Close(); $prodCn.Close()

    # Idempotent, additive insert into the real DEV table (same session -> #stg is visible).
    $insCmd = $devCn.CreateCommand()
    $insCmd.CommandTimeout = 1800
    $insCmd.CommandText = @'
INSERT INTO dbo.tblAttendancePunchLocation
    (ECode, EmployeeName, AttendanceDate, PunchNo, PunchTime, PunchLocation, PunchSTCode, CreatedOn)
SELECT s.ECode, s.EmployeeName, s.AttendanceDate, s.PunchNo, s.PunchTime, s.PunchLocation, s.PunchSTCode, GETDATE()
FROM #stg s
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.tblAttendancePunchLocation t
    WHERE t.ECode = s.ECode AND t.AttendanceDate = s.AttendanceDate AND t.PunchNo = s.PunchNo
);
SELECT (SELECT COUNT(*) FROM #stg) AS staged, @@ROWCOUNT AS inserted;
'@
    $rd = $insCmd.ExecuteReader()
    $staged = 0; $inserted = 0
    if ($rd.Read()) { $staged = $rd.GetValue(0); $inserted = $rd.GetValue(1) }
    $rd.Close()
    Write-Output "[$(Get-Date -Format s)] Staged=$staged  Inserted(new)=$inserted  (skipped existing=$($staged - $inserted))"
    Write-Output "DONE OK"
}
finally {
    if ($prodCn -and $prodCn.State -eq 'Open') { $prodCn.Close() }
    if ($devCn  -and $devCn.State  -eq 'Open') { $devCn.Close()  }
}
