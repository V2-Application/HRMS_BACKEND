param(
    [string]$Server = '192.168.151.27\KARMA',
    [string]$Database = 'HRMS',
    [string]$User = 'nikhil',
    [string]$Password = 'Vrl@12345'
)

$ErrorActionPreference = 'Stop'
$scriptsDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Category -> list of object names (dbo. assumed)
$groups = [ordered]@{
    'Payroll' = @(
        'sp_CalculateEmployeePayroll',
        'sp_CalculateEmployeePayroll_PT_LWF_Dev',
        'sp_GetPayrollSummary'
    )
    'Bonus' = @(
        'usp_ProcessBonusAndPayments',
        'usp_ProcessBonusAndPayments_MonthWise_Dev',
        'usp_ExportEmployeeBonusGratuity',
        'USP_GENERATE_EMP_GRATUITY_BONUS',
        'usp_GetEmployeeFinalBonus',
        'usp_GetEmployeeBonus',
        'GETEMPBONUSLIST',
        'usp_ProcessRetentionBonus',
        'vw_Bonus_Gratuity'
    )
    'Regularize' = @(
        'sp_GetRegularizeRequests',
        'sp_GetRegularizeRequestsBulk',
        'usp_GetAttendanceRegularization'
    )
    'FNF' = @(
        'sp_FNF_BulkUpload',
        'sp_FNF_GetAccountsList',
        'sp_FNF_GetAccountsList_Paid',
        'sp_FNF_GetAccountsList_Unpaid',
        'vw_FNF_AccountsList',
        'vw_FNF_AccountsList_Paid',
        'vw_FNF_AccountsList_Unpaid',
        'sp_FNF_GetEmployeesByCode',
        'sp_FNF_GetEmployeesByCodeForExport',
        'sp_FNF_GetFnfDetailsByEcode',
        'sp_FNF_GetFnfDetailsByEcodeByGautam',
        'sp_FnfPendingToProcessing',
        'sp_ReportFnfMultipleRequest',
        'SaveFNFPaymentData',
        'usp_GetFNFDetailsByCreatedOnAman',
        'usp_UpdateFNFPaymentStatus'
    )
    'InactiveReports' = @(
        'sp_Report_InactiveEmployees_NoDuesNotSubmitted',
        'sp_ReportInactiveEmployeesWithFNF',
        'sp_ReportActiveInEmpMasterinActiveHRMS',
        'sp_ReportActiveInHRMSinActiveEmpMaster',
        'sp_ReportNoResignationApprovalStillInactive',
        'sp_ReportInactiveStillWorking',
        'sp_GetInactiveEmployees_LastPunch_LastUpdate',
        'sp_GetInactiveEmployeesWithLastPunch'
    )
    'BulkInactivate' = @(
        'sp_GetEmployeeEffectiveLeavingDate'
    )
}

$connStr = "Server=$Server;Database=$Database;User Id=$User;Password=$Password;TrustServerCertificate=True;Encrypt=False;"

function Get-Definition {
    param($conn, $name)
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT OBJECT_DEFINITION(OBJECT_ID(N'dbo.' + @n))"
    $p = $cmd.Parameters.Add('@n', [System.Data.SqlDbType]::NVarChar, 200)
    $p.Value = $name
    $def = $cmd.ExecuteScalar()
    return $def
}

function Convert-ToCreateOrAlter {
    param([string]$def)
    if ([string]::IsNullOrWhiteSpace($def)) { return $null }
    # Replace first CREATE PROCEDURE / CREATE VIEW / CREATE FUNCTION with CREATE OR ALTER ...
    $patterns = @(
        @{ p = '(?im)^\s*CREATE\s+PROCEDURE\b';  r = 'CREATE OR ALTER PROCEDURE' },
        @{ p = '(?im)^\s*CREATE\s+PROC\b';       r = 'CREATE OR ALTER PROCEDURE' },
        @{ p = '(?im)^\s*CREATE\s+VIEW\b';       r = 'CREATE OR ALTER VIEW' },
        @{ p = '(?im)^\s*CREATE\s+FUNCTION\b';   r = 'CREATE OR ALTER FUNCTION' }
    )
    foreach ($pat in $patterns) {
        $rx = [regex]::new($pat.p)
        if ($rx.IsMatch($def)) {
            return $rx.Replace($def, $pat.r, 1)
        }
    }
    return $def
}

$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
$conn.Open()
try {
    foreach ($cat in $groups.Keys) {
        $outFile = Join-Path $scriptsDir ("SPs_{0}.sql" -f $cat)
        $sb = New-Object System.Text.StringBuilder
        $null = $sb.AppendLine("-- =============================================================================")
        $null = $sb.AppendLine("-- Category: $cat")
        $null = $sb.AppendLine("-- Source:   dev DB ($Server / $Database)")
        $null = $sb.AppendLine("-- Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
        $null = $sb.AppendLine("-- Objects rewritten as CREATE OR ALTER for safe re-run on production.")
        $null = $sb.AppendLine("-- =============================================================================")
        $null = $sb.AppendLine("SET ANSI_NULLS ON;")
        $null = $sb.AppendLine("SET QUOTED_IDENTIFIER ON;")
        $null = $sb.AppendLine("GO")
        $null = $sb.AppendLine()

        foreach ($obj in $groups[$cat]) {
            Write-Host "[$cat] $obj"
            try {
                $def = Get-Definition -conn $conn -name $obj
            } catch {
                $null = $sb.AppendLine("-- !! ERROR fetching definition for dbo.$obj : $($_.Exception.Message)")
                $null = $sb.AppendLine("GO")
                $null = $sb.AppendLine()
                continue
            }
            if (-not $def) {
                $null = $sb.AppendLine("-- !! dbo.$obj : object not found on dev DB (skipped)")
                $null = $sb.AppendLine("GO")
                $null = $sb.AppendLine()
                continue
            }
            $converted = Convert-ToCreateOrAlter $def
            $null = $sb.AppendLine("-- -----------------------------------------------------------------------------")
            $null = $sb.AppendLine("-- dbo.$obj")
            $null = $sb.AppendLine("-- -----------------------------------------------------------------------------")
            $null = $sb.AppendLine($converted.TrimEnd())
            $null = $sb.AppendLine("GO")
            $null = $sb.AppendLine()
        }

        [System.IO.File]::WriteAllText($outFile, $sb.ToString(), [System.Text.UTF8Encoding]::new($false))
        Write-Host "  -> wrote $outFile"
    }
} finally {
    $conn.Close()
}
