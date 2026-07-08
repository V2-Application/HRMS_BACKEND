param(
  [string]$Month = 'Jun-26',
  [string]$OutPath = "C:\Users\v41797\Desktop\LOC_EMP_Salary_Report_$($Month).xlsx"
)

$cs = "Data Source=192.168.151.28\hrms;Initial Catalog=HRMS;User ID=sa_hrms;Password=CIHTY5pBmRRwjAw;TrustServerCertificate=True"

# Latest run per employee for the month + employee-master fields. READ-ONLY (SELECT only).
$sql = @"
WITH latest AS (
  SELECT *, ROW_NUMBER() OVER (PARTITION BY Ecode ORDER BY RunAt DESC, ID DESC) AS rn
  FROM EmpAttendanceViewSnapshot WHERE [MONTH] = @m
)
SELECT s.*, e.GENDER, e.DOB, e.DOJ, e.MOBILE,
       e.[BANK NAME] AS BANK_NAME, e.[BANK IFSC CODE] AS BANK_IFSC_CODE, e.[A/C NO] AS A_C_NO,
       e.[UAN NO] AS UAN_NO, e.ESICNO, e.[PAN NO] AS PAN_NO, e.[AADHAR NO] AS AADHAR_NO,
       e.PFApplicable, e.ESICApplicable, e.BonusApplicable
FROM latest s
LEFT JOIN tblEmployee e ON e.Ecode = s.Ecode
WHERE s.rn = 1
ORDER BY s.Location_Code, s.Ecode
"@

$conn = New-Object System.Data.SqlClient.SqlConnection $cs
$conn.Open()
$cmd = $conn.CreateCommand(); $cmd.CommandText = $sql; $cmd.CommandTimeout = 300
[void]$cmd.Parameters.AddWithValue("@m", $Month)
$da = New-Object System.Data.SqlClient.SqlDataAdapter $cmd
$dt = New-Object System.Data.DataTable
[void]$da.Fill($dt)
$conn.Close()
Write-Host "Rows fetched: $($dt.Rows.Count)"

function Num($v) {
  if ($null -eq $v -or $v -is [System.DBNull]) { return $null }
  $s = "$v".Trim(); if ($s -eq '') { return $null }
  $d = 0.0; if ([double]::TryParse($s, [ref]$d)) { return $d } else { return $s }
}
function Val($v) { if ($null -eq $v -or $v -is [System.DBNull]) { return $null } else { return $v } }
function YN($v)  { if ($null -eq $v -or $v -is [System.DBNull]) { return $null } elseif ([bool]$v) { return 'Yes' } else { return 'No' } }

$labels = @(
 "SR NO","LOC CD","LOCATION","LOC-TYPE","STATE","E.CODE","NAME","GENDER","D.O.B","JOINING DATE",
 "MOBILE NO.","LEAVING DT","DEPARTMENT","SUB-DEPT.","DESIGNATION","STATUS","Name of Bank","IFSC Code","A/c No.","U.A.N NO",
 "P.F.NO.","E.S.I NO","PAN NO","AADHAR NO","P.F. Applicable?","E.S.I. Applicable?","F.P.F. Applicable?","O.T. Applicable?","P.TAX. Applicable?","BONUS TYPE",
 "BONUS Applicable?","WK-OFF PAY APPLICABLE?","AUTO REMARKS","LP REMARKS","DEPT. REMARKS","HR REMARKS","MTH","",
 "Basic Salary Rate","BASIC SALARY","H.R.A. Rate","H.R.A.","D.A. RATE","D.A","C.C.A. Rate","C.C.A.","SPECIAL ALLOWANCE RATE","SPECIAL ALLOWANCE","REIMB RATE","REIMB",
 "BONUS EARNED RATE","BONUS EARNED","GRATUITY EARNED RATE","GRATUITY EARNED","RETENTION GROSS SALARY","RET. BONUS %","RET. BONUS RATE","RET. BONUS EARNED",
 "Fuel and Maintenance","Fuel and Maintenance (REIMB)","Books and Periodicals","Books and Periodicals (REIMB)","Professional Attire","Professional Attire (REIMB)","Driver Wages","Driver Wages (REIMB)","Mobile Bill","Mobile Bill (REIMB)","Meal Voucher","Meal Voucher (REIMB)",
 "OVERTIME","INCENTIVE AMT","FOODING ALL","MOBILE BILL","ARRERS","EXTRA DAYS ALLOWANCE",
 "CTC-MTH-AS-PER-OFFER-LETTER- SALARY RATE","MTH-AS-PER-ACTUAL- SALARY","CTC SALARY","GROSS SALARY","","TTL GROSS-EARNING","PAYABLE (WITH REIMBUS)","PAYABLE","",
 "TOTAL DEDUCTION","PF","ESI","TDS","P-TAX","CASH SHORT","DIESEL","PENALTY","LOAN","MONTHLY ADVANCE","LWF","","PF (EMPLOYER)","ESIC (EMPLOYER)","LWF (EMPLOYER)","",
 "TOTAL LEAVE","","","","EL LEAVE OP_BAL","EL LEAVE CLS_BAL","EL LEAVE EARN","EL LEAVE AVAILED","CL LEAVE OP_BAL","CL LEAVE CLS_BAL","CL LEAVE EARN","CL LEAVE AVAILED","CO LEAVE OP_BAL","CO LEAVE CLS_BAL","CO LEAVE EARN","CO LEAVE AVAILED","SL LEAVE OP_BAL","SL LEAVE CLS_BAL","SL LEAVE EARN","SL LEAVE AVAILED","",
 "TTL PRESENT DAYS","MACHINE PRESENT DAYS","MANUAL PRESENT DAYS","GEO FENCE ATTEND-DAYS","BGT-PAYABLE DAYS","PAYABLE DAYS","TTL PAYABLE DAYS","ABSENT","EXTRA DAY CNT","BGT DAYS WKL-OF","ACT DAYS WK-OFF (WO LEAVE)","HLD","POW","TOTAL LEAVE AVAILED","EL","CL","CO","SL","",
 "SALARY PAID-1","REIMB","INCENTIVE","SALARY PAID-2","SALARY PAID-TTL","DIFF",""
)
$cols = $labels.Count

# Group banners placed at their start column (1-based).
$bannerStart = @{ 1="PART-1 (LOC & EMP DETAIL)"; 39="SALARY (BGT VS ACT)"; 51="BONUS EARNED RATE VS ACT"; 59="REIM-BREKUP DETAIL"; 71="EXTRA-GROSS-DETAIL"; 77="SALARY PAYABLE"; 86="PART-8 DEDUCTION ( PF/ESIC/TDS/LOAN/BONUS)"; 102="PART 4 ( LEAVE REPORT)"; 123="PART-7 : PAYABLE DAYS WORKING"; 142="SALARY PAID" }
$bannerRanges = @(@(1,38),@(39,50),@(51,58),@(59,70),@(71,76),@(77,85),@(86,101),@(102,122),@(123,141),@(142,148))

function CsvField($v) {
  if ($null -eq $v -or $v -is [System.DBNull]) { return '""' }
  if ($v -is [datetime]) { return '"' + $v.ToString('dd-MMM-yyyy') + '"' }
  $s = "$v" -replace '"','""'
  return '"' + $s + '"'
}

$csvPath = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "payroll_$([System.Guid]::NewGuid().ToString('N')).csv")
$sw = New-Object System.IO.StreamWriter($csvPath, $false, [System.Text.Encoding]::UTF8)

# Row 1: banners
$line = for ($c=1; $c -le $cols; $c++) { if ($bannerStart.ContainsKey($c)) { CsvField $bannerStart[$c] } else { '""' } }
$sw.WriteLine([string]::Join(',', $line))
# Row 2: labels
$line = for ($c=0; $c -lt $cols; $c++) { CsvField $labels[$c] }
$sw.WriteLine([string]::Join(',', $line))

# Data rows
$sr = 0
foreach ($row in $dt.Rows) {
  $vals = @(
    ($sr+1),
    (Val $row['Location_Code']), (Val $row['Location Name']), $null, $null,
    (Val $row['Ecode']), (Val $row['Employee Name']), (Val $row['GENDER']), (Val $row['DOB']), (Val $row['DOJ']),
    (Val $row['MOBILE']), $null, (Val $row['department']), $null, (Val $row['designation']), (Val $row['Status']),
    (Val $row['BANK_NAME']), (Val $row['BANK_IFSC_CODE']), (Val $row['A_C_NO']), (Val $row['UAN_NO']),
    $null, (Val $row['ESICNO']), (Val $row['PAN_NO']), (Val $row['AADHAR_NO']),
    (YN $row['PFApplicable']), (YN $row['ESICApplicable']), $null, $null, $null, (Val $row['BonusApplicable']),
    $null, $null, $null, $null, $null, $null, (Val $row['MONTH']), $null,
    (Val $row['BasicSalary(Bud.)']), (Val $row['BasicSalary(Actual)']), (Val $row['HRA(Bud.)']), (Val $row['HRA(Actual)']),
    (Val $row['DA(Bud.)']), (Val $row['DA(Actual)']), (Val $row['CCA(Bud.)']), (Val $row['CCA(Actual)']),
    (Val $row['SpecialAllowance(Bud.)']), (Val $row['SpecialAllowance(Actual)']), (Val $row['Reimbersment(Bud.)']), (Val $row['Reimbersment(Actual)']),
    $null, $null, $null, $null, $null, $null, $null, $null,
    (Val $row['Fuel and Maintenance(Bud.)']), (Val $row['Fuel and Maintenance(Actual)']), (Val $row['Books and Periodicals(Bud.)']), (Val $row['Books and Periodicals(Actual)']),
    (Val $row['Professional Attire(Bud.)']), (Val $row['Professional Attire(Actual)']), (Val $row['Driver Wages(Bud.)']), (Val $row['Driver Wages(Actual)']),
    (Val $row['Mobile Bill(Bud.)']), (Val $row['Mobile Bill(Actual)']), (Val $row['Meal Voucher(Bud.)']), (Val $row['Meal Voucher(Actual)']),
    (Val $row['Overtime']), (Num $row['Incentive']), (Val $row['Fooding_Allowance']), (Val $row['Mobile_Bill']), (Num $row['ARREAR']), (Num $row['ExtraDayAllowance']),
    (Val $row['Monthly Gross CTC(Bud.)']), (Val $row['Monthly Gross CTC(Actual)']), $null, (Val $row['Monthly Gross CTC(Actual)']), $null,
    (Val $row['Monthly Gross CTC(Actual)']), (Val $row['Monthly Gross CTC(Actual After Deduction AND AddONS)']), (Val $row['Monthly Gross CTC(Actual After Deduction AND AddONS)']), $null,
    (Val $row['TotalDeductions']), (Val $row['PF(Employee)']), (Val $row['ESIC(Employee)']), (Num $row['TDS']), (Num $row['PTax']),
    (Num $row['CashShort']), (Num $row['DieselDeduction']), (Num $row['Penality']), (Num $row['Loan']), $null, (Num $row['Lwf']), $null,
    (Val $row['PF(Employeer)']), (Val $row['ESIC(Employeer)']), $null, $null,
    (Val $row['Leave-Used']), $null, $null, $null,
    (Val $row['Opening EL']), (Val $row['EarnedLeaveBalance']), (Val $row['EarnedLeaveAcquired']), (Val $row['EarnedLeaveUsed']),
    (Val $row['Opening CL']), (Val $row['CasualLeaveBalance']), (Val $row['CasualLeaveAcquired']), (Val $row['CasualLeaveUsed']),
    (Val $row['Opening CompoOff']), (Val $row['CompoOffBalance']), (Val $row['CompoOffAcquired']), (Val $row['CompoOffUsed']),
    $null, $null, $null, $null, $null,
    (Val $row['actualttl days']), (Num $row['Machine']), (Num $row['MANUAL']), (Val $row['GF']), (Val $row['ttl bgt days']),
    (Val $row['paybledays']), (Val $row['Payble_Days']), (Val $row['Absent']), (Val $row['extradays']), $null,
    (Val $row['actualweekly']), (Val $row['HolidayOff']), (Val $row['presentweeklyoff']), (Val $row['Leave-Used']),
    (Val $row['EarnedLeaveUsed']), (Val $row['CasualLeaveUsed']), (Val $row['CompoOffUsed']), $null, $null,
    $null, $null, $null, $null, $null, $null, $null
  )
  $line = for ($c=0; $c -lt $cols; $c++) { CsvField $vals[$c] }
  $sw.WriteLine([string]::Join(',', $line))
  $sr++
}
$sw.Flush(); $sw.Close()
Write-Host "CSV written ($sr rows): $csvPath"

# Open CSV in Excel, add merged banners + styling, save as .xlsx.
$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false; $excel.DisplayAlerts = $false
$wb = $excel.Workbooks.Open($csvPath, 0, $true)   # readonly open of CSV
$ws = $wb.Worksheets.Item(1)

foreach ($br in $bannerRanges) {
  $rng = $ws.Range($ws.Cells.Item(1,$br[0]), $ws.Cells.Item(1,$br[1]))
  $rng.Merge() | Out-Null
  $rng.HorizontalAlignment = -4108
  $rng.Font.Bold = $true
  $rng.Interior.Color = 15649023
}
$lr = $ws.Range($ws.Cells.Item(2,1), $ws.Cells.Item(2,$cols))
$lr.Font.Bold = $true; $lr.WrapText = $true; $lr.Interior.Color = 15921906
$ws.Application.ActiveWindow.SplitRow = 2
$ws.Application.ActiveWindow.FreezePanes = $true

$wb.SaveAs($OutPath, 51)   # xlsx
$wb.Close($false); $excel.Quit()
[System.Runtime.InteropServices.Marshal]::ReleaseComObject($ws) | Out-Null
[System.Runtime.InteropServices.Marshal]::ReleaseComObject($wb) | Out-Null
[System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel) | Out-Null
Remove-Item $csvPath -Force -ErrorAction SilentlyContinue
Write-Host "SAVED: $OutPath"
