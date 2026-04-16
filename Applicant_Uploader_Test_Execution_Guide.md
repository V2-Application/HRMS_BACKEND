# Applicant Uploader Test Cases - Execution Guide

## Overview
This document provides comprehensive test cases for the Applicant Uploader functionality. The test cases are organized by category and cover various scenarios including happy paths, validation errors, and edge cases.

## Test Files Created

1. **Applicant_Uploader_Test_Cases.csv** - Main test case document with 45 test cases
2. **Test_Data_*.csv** - Sample test data files for various scenarios

## How to Use These Test Cases

### Step 1: Open Test Cases in Excel
1. Open `Applicant_Uploader_Test_Cases.csv` in Microsoft Excel
2. Save it as `Applicant_Uploader_Test_Cases.xlsx` for better formatting
3. The file contains all test cases with descriptions and expected results

### Step 2: Prepare Test Data
1. Use the provided `Test_Data_*.csv` files as templates
2. **IMPORTANT**: Replace placeholder values with actual database values:
   - **STCodes**: Replace "RH01", "RH02" with actual STCodes from `tblLocations` table
   - **Departments**: Replace "IT", "HR", "Sales" with actual department names from `tblDepartments` table
   - **Designations**: Replace "Software Engineer", "HR Manager" with actual designation names from `tblDesignations` table
   - **Statuses**: Replace "Applied", "Shortlisted" with actual status names from `CandidateProcessStatuses` table
   - **Interviewers**: Replace "Interviewer Name 1", "Interviewer Name 2" with actual interviewer names from `Interviewers` table

### Step 3: Execute Test Cases

#### File Validation Tests (TC-001 to TC-004)
- Test file upload with different file types
- Verify error messages for invalid file types
- Verify acceptance of .xlsx and .xls files

#### Header Validation Tests (TC-005 to TC-012)
- Test with missing required headers
- Test with alternative header names (aliases)
- Verify proper error messages

#### Data Validation Tests (TC-013 to TC-024)
- Test with missing required fields
- Test with invalid database references
- Verify validation error messages

#### Data Format Tests (TC-025 to TC-029)
- Test invalid date formats
- Test invalid numeric formats
- Verify error handling

#### Interview Validation Tests (TC-030 to TC-031)
- Test incomplete interview data
- Verify validation rules

#### Happy Path Tests (TC-032 to TC-035)
- Test successful uploads with various data combinations
- Verify data is saved correctly in database

#### Edge Cases (TC-036 to TC-045)
- Test special characters, whitespace, multiple formats
- Verify robust handling of edge cases

## Test Data Files Reference

| File Name | Purpose | Test Case IDs |
|-----------|---------|---------------|
| Test_Data_Valid_Single_Required.csv | Minimal valid data | TC-032 |
| Test_Data_Valid_With_Optional.csv | Full valid data | TC-033 |
| Test_Data_Valid_With_Interview.csv | Valid data with interviews | TC-034 |
| Test_Data_Multiple_Applicants.csv | Multiple valid applicants | TC-035 |
| Test_Data_Missing_Required_Fields.csv | Missing required fields | TC-014 to TC-019 |
| Test_Data_Invalid_DB_Values.csv | Invalid database references | TC-020 to TC-024 |
| Test_Data_Invalid_Formats.csv | Invalid data formats | TC-025 to TC-029 |
| Test_Data_Invalid_Interview.csv | Invalid interview data | TC-030 to TC-031 |
| Test_Data_Edge_Cases.csv | Edge case scenarios | TC-036 to TC-040 |
| Test_Data_Multiple_DateFormats.csv | Different date formats | TC-041 |

## Required Headers (with Aliases)

### Required Headers:
1. **Job Position** (aliases: "Job Position", "JobLocation", "Location STCode", "Location")
2. **Department** (aliases: "Department", "Dept", "Department Name")
3. **Designation** (aliases: "Designation", "Designation Name", "DesignationNar")
4. **Preferred Job Location** (aliases: "Preferred Job Location", "Preferred Location", "PreferredLocation")
5. **Status** (aliases: "Status", "Current Status", "Status Name")
6. **Full Name** (aliases: "Full Name", "Name")
7. **Phone** (aliases: "Phone", "Mobile", "Contact No")

### Optional Headers:
- DOB / Date of Birth
- Email / Email Address
- Total Experience (Years) / Total Experience
- Previous Company / Last Company
- Previous Designation / Designation (Previous)
- Previous Salary / Last Drawn Salary
- Salary Expectation / Expected Salary
- Source
- Date of Joining / DOJ
- Reference
- Higher Qualification / Highest Qualification
- Passing Year / Year Of Passing
- Cover Letter / Notes / Notes
- Interview-1 ACT / Interview 1 ACT / Interview1 ACT / Interview1Date
- Interviewer-1 / Interviewer 1 / Interview1 Interviewer / Interviewer1
- Interviewer-1 Status / Interviewer 1 Status / Interview1 Status
- Interview-2 ACT / Interview 2 ACT / Interview2 ACT / Interview2Date
- Interviewer-2 / Interviewer 2 / Interview2 Interviewer / Interviewer2
- Interviewer-2 Status / Interviewer 2 Status / Interview2 Status
- Interview Final Remark / Final Interview Remark / Final Remark

## Test Execution Checklist

### Pre-Test Setup
- [ ] Database is accessible and contains test data
- [ ] Valid STCodes, Departments, Designations, Statuses, and Interviewers are identified
- [ ] Test data files are updated with actual database values
- [ ] API endpoint is accessible
- [ ] Test user credentials are available

### During Testing
- [ ] Execute each test case in order
- [ ] Record actual results in test execution log
- [ ] Take screenshots of errors (if applicable)
- [ ] Verify database records after successful uploads
- [ ] Check transaction rollback on errors

### Post-Test
- [ ] Review all test results
- [ ] Document any bugs found
- [ ] Verify data integrity in database
- [ ] Clean up test data if needed

## Expected Database Changes

After a successful upload, verify:
1. **Candidates table**: New candidate records created with ApplicantId format "AV000001", "AV000002", etc.
2. **tblExperiences table**: Experience records created (if Previous Company/Designation/Salary provided)
3. **tblQualifications table**: Qualification records created (if Higher Qualification/Passing Year provided)
4. **AssignLocationHistories table**: Location assignment history created
5. **CandidateStatus_Histories table**: Status history created
6. **tblScheduleInterviews table**: Interview schedules created (if interview data provided)
7. **tblInterviewRounds table**: Interview rounds created (if interview data provided)
8. **InterviewApprovalLogs table**: Approval logs created (if interview data provided)

## Common Issues and Solutions

### Issue: "Unknown STCode(s)"
**Solution**: Verify STCode exists in `tblLocations` table. STCode matching is case-insensitive.

### Issue: "Unknown Department(s)"
**Solution**: Verify Department name exists in `tblDepartments` table. Matching is case-insensitive.

### Issue: "Unknown Status(es)"
**Solution**: Check `CandidateProcessStatuses` table for valid status names. The error message will list all allowed statuses.

### Issue: Date parsing errors
**Solution**: Use supported date formats: dd-MM-yyyy, dd/MM/yyyy, yyyy-MM-dd, or standard DateTime format.

### Issue: Salary parsing errors
**Solution**: Currency symbols (₹, INR) are automatically stripped. Use numeric values only.

### Issue: Interview validation errors
**Solution**: If interview date is provided, interviewer name(s) are required. If interviewer name is provided, interview date is required.

## Notes

1. All string comparisons are case-insensitive
2. Whitespace is automatically trimmed from fields
3. Empty rows are skipped during processing
4. Multiple interviewers can be separated by comma (,), semicolon (;), or pipe (|)
5. If multiple interviewers have a single status value, it's applied to all
6. Transaction rollback occurs on any error during processing
7. ApplicantId is auto-generated in format "AV" + 6-digit sequence number

## Test Results Template

Create a test execution log with columns:
- Test Case ID
- Test Case Name
- Executed By
- Execution Date
- Status (Pass/Fail/Not Executed)
- Actual Result
- Notes/Comments

---

**Last Updated**: [Current Date]
**Version**: 1.0

