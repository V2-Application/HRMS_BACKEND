$ErrorActionPreference = 'Stop'

$out = "$env:USERPROFILE\Desktop\PF_ESI_Configuration_HRMS.docx"
$stage = Join-Path $env:TEMP ("docx_pfesi_" + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $stage -Force
$null = New-Item -ItemType Directory -Path (Join-Path $stage "_rels") -Force
$null = New-Item -ItemType Directory -Path (Join-Path $stage "word") -Force
$null = New-Item -ItemType Directory -Path (Join-Path $stage "word\_rels") -Force

$contentTypes = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
  <Default Extension="xml" ContentType="application/xml"/>
  <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
</Types>
'@

$rels = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
</Relationships>
'@

$docRels = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"></Relationships>
'@

# --- Helpers to build OOXML paragraphs ---
function H1($text) {
@"
<w:p><w:pPr><w:pStyle w:val="Heading1"/></w:pPr><w:r><w:rPr><w:b/><w:sz w:val="32"/></w:rPr><w:t xml:space="preserve">$text</w:t></w:r></w:p>
"@
}
function H2($text) {
@"
<w:p><w:pPr><w:pStyle w:val="Heading2"/></w:pPr><w:r><w:rPr><w:b/><w:sz w:val="26"/></w:rPr><w:t xml:space="preserve">$text</w:t></w:r></w:p>
"@
}
function P($text) {
  $safe = [System.Security.SecurityElement]::Escape($text)
@"
<w:p><w:r><w:rPr><w:sz w:val="22"/></w:rPr><w:t xml:space="preserve">$safe</w:t></w:r></w:p>
"@
}
function PBold($text) {
  $safe = [System.Security.SecurityElement]::Escape($text)
@"
<w:p><w:r><w:rPr><w:b/><w:sz w:val="22"/></w:rPr><w:t xml:space="preserve">$safe</w:t></w:r></w:p>
"@
}
function Bullet($text) {
  $safe = [System.Security.SecurityElement]::Escape($text)
@"
<w:p><w:pPr><w:numPr><w:ilvl w:val="0"/><w:numId w:val="1"/></w:numPr><w:ind w:left="360" w:hanging="360"/></w:pPr><w:r><w:rPr><w:sz w:val="22"/></w:rPr><w:t xml:space="preserve">$safe</w:t></w:r></w:p>
"@
}
function Mono($text) {
  $safe = [System.Security.SecurityElement]::Escape($text)
@"
<w:p><w:r><w:rPr><w:rFonts w:ascii="Consolas" w:hAnsi="Consolas"/><w:sz w:val="20"/></w:rPr><w:t xml:space="preserve">$safe</w:t></w:r></w:p>
"@
}

$body = @()
$body += H1 "PF and ESI Configuration in HRMS Backend"
$body += P "Audit date: 2026-05-26. Scope: HRMSAPI (.NET 8) at C:\Users\v41797\Desktop\HRMS_BACKEND. Author: engineering review."

$body += H2 "Executive Summary"
$body += PBold "The HRMS backend does NOT compute PF or ESI amounts and does NOT enforce the Rs.1800 PF cap. It is a deduction-passthrough: pre-calculated values are uploaded via Excel and processed as-is."
$body += P "Per-employee toggles exist (PFApplicable / ESICApplicable), but no rate constants (12%, 0.75%, 3.25%) and no statutory ceilings (Rs.15000 PF wage, Rs.21000 ESI gross, Rs.1800 PF cap) are present anywhere in the C# code, appsettings.json, or stored procedures. The Rs.1800 cap is assumed to be applied upstream, in whatever spreadsheet prepares the deduction file before upload."

$body += H2 "Per-Employee Toggles"
$body += Bullet "tblEmployee.PFApplicable (bool)  - Data/tblEmployee.cs, line 236"
$body += Bullet "tblEmployee.ESICApplicable (bool) - Data/tblEmployee.cs, line 240"
$body += P "These flags decide whether a given employee participates in PF / ESI. They do not influence amount calculation."

$body += H2 "Where PF and ESI Amounts Are Stored"
$body += Bullet "tblEmpPFDatum.EmpPF, EmprPF (decimal) - employee and employer PF portions"
$body += Bullet "tblEmpESICDatum.EmpESIC, EmprESIC (decimal) - employee and employer ESI portions"
$body += Bullet "tblEmployeeDeduction.PF and tblEmployeeDeduction.ESIC - stored as strings (no validation)"
$body += P "All four are written by the upload flow; nothing in code derives them from Basic or Gross salary."

$body += H2 "Upload Flow (Excel -> DB)"
$body += Bullet "Implementation/UploaderService.cs - UploadESICFromExcelAsync (line 2525). Reads pre-calculated EmpESIC and EmprESIC from Excel columns 9 and 10 (line 2705-2706)."
$body += Bullet "Implementation/EmployeeDeductionService.cs - UploadEmployeeDeductionExcel (line 24). Reads PF and ESIC as raw strings (line 46-47) and stores to tblEmployeeDeductions without any rate-based check."
$body += Bullet "PF upload follows the same pattern (referenced from Implementation/PayrollService.cs around line 573)."

$body += H2 "Payroll Consumption"
$body += Bullet "Scripts/SPs_Payroll.sql - sp_CalculateEmployeePayroll, lines 665-799."
$body += Bullet "The SP pulls PF and ESIC values from tblEmployeeDeductions (line 665-668) and merges them into tbl_Month_salary (line 730-799) verbatim. No cap, no recalculation."

$body += H2 "Search Performed for Rate / Ceiling Constants"
$body += P "Greps run against the codebase (C# and SQL) and appsettings.json for: 12, 0.12, 1800, 15000, 21000, 0.0075, 0.0325. No matches in any business-logic context."
$body += Mono "Result: zero hardcoded statutory constants in the backend."

$body += H2 "Operational Risk"
$body += Bullet "If the source Excel sends an incorrect PF (e.g., Rs.2400 against a Basic of Rs.10000), payroll will pay out Rs.2400. There is no backend sanity check."
$body += Bullet "Switching the PF toggle on for an employee does not auto-populate any amount. A blank row in tblEmpPFDatum will simply yield Rs.0 deduction."
$body += Bullet "ESI gross-ceiling logic (employees earning above Rs.21000 are exempt) is also upstream-only."

$body += H2 "If You Want the Backend to Enforce This"
$body += P "Two design options, listed in increasing impact:"
$body += Bullet "Lightweight: add validation in the upload services to reject rows where PF > Rs.1800 or PF > 12% of Basic, and similarly for ESI. Keeps the Excel workflow."
$body += Bullet "Full rate engine: introduce a tblStatutoryRates table (PFRatePct, PFWageCeiling, ESIEmployeePct, ESIEmployerPct, ESIGrossCeiling, EffectiveFrom). The payroll service computes EmpPF / EmprPF / EmpESIC / EmprESIC from Basic and Gross when the per-employee toggles are on, capped to ceilings. Replaces the upload entirely."

$body += H2 "Files Referenced"
$body += Mono "Data/tblEmployee.cs"
$body += Mono "Data/tblEmployeeDeduction.cs"
$body += Mono "Data/tblEmpPFDatum.cs"
$body += Mono "Data/tblEmpESICDatum.cs"
$body += Mono "Implementation/UploaderService.cs"
$body += Mono "Implementation/EmployeeDeductionService.cs"
$body += Mono "Implementation/PayrollService.cs"
$body += Mono "Scripts/SPs_Payroll.sql"
$body += Mono "appsettings.json"

$bodyXml = ($body -join "`n")
$document = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
  <w:body>
    $bodyXml
    <w:sectPr><w:pgSz w:w="12240" w:h="15840"/><w:pgMar w:top="1440" w:right="1440" w:bottom="1440" w:left="1440" w:header="720" w:footer="720" w:gutter="0"/></w:sectPr>
  </w:body>
</w:document>
"@

# Write all 4 OOXML parts (UTF-8, no BOM)
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText((Join-Path $stage "[Content_Types].xml"), $contentTypes, $utf8NoBom)
[System.IO.File]::WriteAllText((Join-Path $stage "_rels\.rels"), $rels, $utf8NoBom)
[System.IO.File]::WriteAllText((Join-Path $stage "word\_rels\document.xml.rels"), $docRels, $utf8NoBom)
[System.IO.File]::WriteAllText((Join-Path $stage "word\document.xml"), $document, $utf8NoBom)

# Zip into .docx
if (Test-Path $out) { Remove-Item $out -Force }
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($stage, $out, [System.IO.Compression.CompressionLevel]::Optimal, $false)

Remove-Item $stage -Recurse -Force

"Wrote: $out"
"Size : $((Get-Item $out).Length) bytes"
