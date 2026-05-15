# Production Apply Runbook

**Branch:** `pulkit_changes1`
**Source DB:** dev `192.168.151.27\KARMA` / `HRMS`
**Target DB:** prod `192.168.151.28\hrms` / `HRMS`
**All scripts use `CREATE OR ALTER`** — safe to re-run.

---

## 0. Pre-flight checks (run on PROD, do NOT skip)

### 0.1 Verify dependency table exists

10+ SPs in `SPs_BulkInactivate.sql`, `SPs_FNF.sql`, `SPs_InactiveReports.sql` reference
`dbo.tbl_fn_GetMonthlyPunchesRange_productionnewnick_test`. If it doesn't exist on prod, those SPs will compile but fail at runtime.

```sql
SELECT COUNT(*) AS exists_count
FROM sys.objects
WHERE name = 'tbl_fn_GetMonthlyPunchesRange_productionnewnick_test' AND type = 'U';
-- Expected: 1.  If 0, STOP and decide (see options below).
```

If the table does not exist on prod, options are:
1. **Create it on prod** with the same structure (30+ punch-aggregation columns — confirm with the team this is what prod actually wants).
2. **Rename references** in the script files to a prod equivalent before applying.
3. **Apply only the scripts that don't reference it** (SPs_Payroll, SPs_Bonus, SPs_Regularize).

### 0.2 Back up SPs you're about to overwrite

```sql
-- Per SP you're about to overwrite, dump its current body so you can roll back:
SELECT OBJECT_DEFINITION(OBJECT_ID('dbo.sp_CalculateEmployeePayroll'));
-- repeat for any SP listed in section 2.
```

Or, easier: take a full DB backup just before applying.

### 0.3 Note dev-named SPs in the bundle

These SPs have `_Dev` / `_nik` / `_nikhil` / `_MonthWise_Dev` in the name. They're included because the C# code references them, but confirm with the team they belong on prod:
- `sp_CalculateEmployeePayroll_PT_LWF_Dev` (SPs_Payroll.sql)
- `usp_ProcessBonusAndPayments_MonthWise_Dev` (SPs_Bonus.sql)

---

## 1. Apply order (low-risk → highest blast radius)

Run each file via SSMS or `sqlcmd`:

```bash
sqlcmd -S 192.168.151.28\hrms -d HRMS -U <user> -P <pwd> -C -b -i Scripts\<file>.sql
```

`-b` makes sqlcmd exit on the first error so you don't silently apply a half-broken file.

| # | File | Risk | Why this order |
|---|---|---|---|
| 1 | `SPs_Regularize.sql` | Low | 3 SPs, no dev-named deps, smallest blast radius |
| 2 | `SPs_BulkInactivate.sql` | Medium | 1 SP, but requires dep table (0.1) |
| 3 | `SPs_Payroll.sql` | Medium | 3 SPs, includes `_Dev` variant |
| 4 | `SPs_Bonus.sql` | Medium | 9 objects, includes `_MonthWise_Dev` |
| 5 | `SPs_InactiveReports.sql` | High | 8 SPs, multiple deps on punch table |
| 6 | `SPs_FNF.sql` | Highest | 19 objects (SPs + views), heaviest dep on punch table |

Between each step, verify the result and smoke-test the affected feature in the app before continuing.

---

## 2. Post-apply smoke tests

```sql
-- All target SPs/views compiled and present on prod?
SELECT s.name + '.' + o.name AS [Object], o.type_desc, o.modify_date
FROM sys.objects o JOIN sys.schemas s ON s.schema_id = o.schema_id
WHERE o.name IN (
    'sp_CalculateEmployeePayroll','sp_CalculateEmployeePayroll_PT_LWF_Dev','sp_GetPayrollSummary',
    'usp_ProcessBonusAndPayments','usp_ProcessBonusAndPayments_MonthWise_Dev','usp_ExportEmployeeBonusGratuity',
    'USP_GENERATE_EMP_GRATUITY_BONUS','usp_GetEmployeeFinalBonus','usp_GetEmployeeBonus','GETEMPBONUSLIST',
    'usp_ProcessRetentionBonus','vw_Bonus_Gratuity',
    'sp_GetRegularizeRequests','sp_GetRegularizeRequestsBulk','usp_GetAttendanceRegularization',
    'sp_FNF_BulkUpload','sp_FNF_GetAccountsList','sp_FNF_GetAccountsList_Paid','sp_FNF_GetAccountsList_Unpaid',
    'vw_FNF_AccountsList','vw_FNF_AccountsList_Paid','vw_FNF_AccountsList_Unpaid',
    'sp_FNF_GetEmployeesByCode','sp_FNF_GetEmployeesByCodeForExport',
    'sp_FNF_GetFnfDetailsByEcode','sp_FNF_GetFnfDetailsByEcodeByGautam',
    'sp_FnfPendingToProcessing','sp_ReportFnfMultipleRequest',
    'SaveFNFPaymentData','usp_GetFNFDetailsByCreatedOnAman','usp_UpdateFNFPaymentStatus',
    'sp_Report_InactiveEmployees_NoDuesNotSubmitted','sp_ReportInactiveEmployeesWithFNF',
    'sp_ReportActiveInEmpMasterinActiveHRMS','sp_ReportActiveInHRMSinActiveEmpMaster',
    'sp_ReportNoResignationApprovalStillInactive','sp_ReportInactiveStillWorking',
    'sp_GetInactiveEmployees_LastPunch_LastUpdate','sp_GetInactiveEmployeesWithLastPunch',
    'sp_GetEmployeeEffectiveLeavingDate'
)
ORDER BY o.modify_date DESC;
-- Expected: 40 rows, all with modify_date = today.
```

App-level smoke tests after deploy:
1. **BulkInactivateEmployees endpoint** (`POST /api/EmployeeNew/BulkInactivateEmployees`) — inactivate 1 test employee.
2. **Pending Regularization tab** — open and verify only current attendance cycle (26th prev → 25th current) shows.
3. **Approved / Rejected Regularization tabs** — verify only acted-by-me rows show (except true SuperAdmin).
4. **Run payroll for one employee** — verify `sp_CalculateEmployeePayroll` output is unchanged.
5. **Bonus export** — call the bonus/gratuity export feature.

---

## 3. Rollback

Because every file uses `CREATE OR ALTER`, no DROP is required to roll back — just re-apply the previous SP body. Either:
- Restore from the section 0.2 backup, or
- Restore the DB from the pre-deploy full backup.
