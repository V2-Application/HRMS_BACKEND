# Attendance Count Approval System - Implementation Guide

## Overview
This system implements a two-level approval workflow for attendance count with CM (Cluster Manager) and RM (Regional Manager) approvals. The key feature is that RM is the upper-level authority and can override CM's decisions.

## Architecture

### Status Calculation (Dynamic)
Status is **calculated dynamically** based on `IsCMApproved` and `IsRMApproved` flags (no separate StatusId column):

| IsCMApproved | IsRMApproved | Status | Description |
|--------------|--------------|--------|-------------|
| NULL | NULL | Pending CM | Initial state, waiting for CM review |
| true | NULL | Pending RM | CM approved, waiting for RM review |
| false | NULL | Pending RM | CM rejected, RM can override |
| Any | true | **Approved** | RM approved (FINAL) - even if CM rejected |
| Any | false | **Rejected** | RM rejected (FINAL) - even if CM approved |

### Key Features
1. **Unique Constraint**: One request per (ECode, MonthYear)
2. **MonthYear Format**: Stored as "MMM-YY" format (e.g., Jan-25, Feb-25)
3. **RM Authority**: RM can override CM's decision as they are upper level
4. **Dynamic Status**: No StatusId column, calculated on-the-fly
5. **Multiple Attachments**: Support for proof documents
6. **Audit Trail**: Tracks who approved/rejected and when
7. **Remarks**: Both CM and RM can add remarks while approving/rejecting

## Database Setup

### Step 1: Run SQL Script
Execute the SQL script to create tables:
```sql
-- Located at: DatabaseScripts/AttendanceCountApproval.sql
```

Tables created:
- `tblAttendanceCountApproval` - Main approval table
- `tblAttendanceCountAttachments` - Attachments/proof documents

### Step 2: Update DbContext
Already updated in `Data/HRMSContext.cs`:
```csharp
public virtual DbSet<tblAttendanceCountApproval> tblAttendanceCountApprovals { get; set; }
public virtual DbSet<tblAttendanceCountAttachment> tblAttendanceCountAttachments { get; set; }
```

## Service Implementation

### IMPORTANT: Manual Step Required
**You need to manually copy the service methods** from:
- Source: `Implementation/EmpAttendanceService_AttendanceCountApproval.cs` (lines 4-362)
- Destination: `Implementation/EmpAttendanceService.cs` (before the closing braces at line ~1545)

This step is required because the main service file is too large for automatic editing.

### Service Methods

1. **CreateAttendanceCountApprovalAsync** - Create new approval request
2. **CMApproveAttendanceCountAsync** - CM approves/rejects
3. **RMApproveAttendanceCountAsync** - RM approves/rejects (can override CM)
4. **GetAttendanceCountApprovalsAsync** - Get paginated list with filters
5. **GetAttendanceCountApprovalByIdAsync** - Get single approval by ID

## API Endpoints

All endpoints are in `EmpAttendanceController.cs`:

### 1. Create Attendance Count Approval Request
```
POST /api/EmpAttendance/attendance-count-approval
Authorization: Bearer {token}

Body:
{
  "eCode": "EMP001",
  "monthYear": "Jan-25",
  "attendanceCount": 25,
  "employeeRemarks": "Please approve my attendance count",
  "attachments": [
    {
      "fileUrl": "https://example.com/proof.pdf",
      "fileName": "proof.pdf",
      "fileSize": 1024000
    }
  ]
}

Response:
{
  "success": true,
  "message": "Attendance count approval request created successfully",
  "approvalId": 1
}
```

### 2. CM Approval/Rejection
```
POST /api/EmpAttendance/attendance-count-approval/cm-approve
Authorization: Bearer {token}

Body:
{
  "attendanceCountApprovalId": 1,
  "isApproved": true,
  "cmRemarks": "Approved after verification"
}

Response:
{
  "success": true,
  "message": "Attendance count approved by CM successfully"
}
```

### 3. RM Approval/Rejection (Can Override CM)
```
POST /api/EmpAttendance/attendance-count-approval/rm-approve
Authorization: Bearer {token}

Body:
{
  "attendanceCountApprovalId": 1,
  "isApproved": true,
  "rmRemarks": "Final approval given"
}

Response:
{
  "success": true,
  "message": "Attendance count approved by RM successfully"
}
```

### 4. Get Attendance Count Approvals (Paginated)
```
GET /api/EmpAttendance/attendance-count-approval?pageNumber=1&pageSize=10&approverRole=cm
Authorization: Bearer {token}

Query Parameters:
- pageNumber: Page number (default: 1)
- pageSize: Items per page (default: 10)
- searchTerm: Search in ecode, remarks
- ecode: Filter by employee code
- approverRole: Filter by role (cm/rm) - shows pending items for that role

Response:
{
  "success": true,
  "data": {
    "data": [...],
    "totalRecords": 50,
    "pageNumber": 1,
    "pageSize": 10,
    "totalPages": 5
  }
}
```

### 5. Get Attendance Count Approval by ID
```
GET /api/EmpAttendance/attendance-count-approval/{approvalId}
Authorization: Bearer {token}

Response:
{
  "success": true,
  "data": {
    "attendanceCountApprovalId": 1,
    "eCode": "EMP001",
    "employeeName": "John Doe",
    "monthYear": "Jan-25",
    "attendanceCount": 25,
    "employeeRemarks": "Please approve",
    "isCMApproved": true,
    "cmApprovedBy": "MGR001",
    "cmApprovedOn": "2025-01-15T10:00:00Z",
    "cmRemarks": "Approved",
    "isRMApproved": null,
    "status": "Pending RM",
    "statusDescription": "CM Approved, Pending RM Approval",
    "attachments": [...]
  }
}
```

## Business Rules

### Approval Flow
1. **Employee submits** attendance count with proof
2. **CM Reviews** - Can approve or reject
3. **RM Reviews** - Final decision
   - If RM approves → **Final Status: Approved** (even if CM rejected)
   - If RM rejects → **Final Status: Rejected** (even if CM approved)

### Validation Rules
- **Unique Constraint**: Only one request per (ECode, MonthYear)
- **MonthYear Format**: Must be "MMM-YY" format (e.g., Jan-25, Feb-25, Dec-24)
  - Allowed months: Jan, Feb, Mar, Apr, May, Jun, Jul, Aug, Sep, Oct, Nov, Dec
  - Year must be 2 digits
- **Attendance Count**: 0-31
- **Remarks Length**: Max 1000 characters
- **Re-submission**: Cannot resubmit once CM or RM has reviewed

### Authorization
- **Employee**: Can create request and view their own requests
- **CM**: Can approve/reject requests assigned to them
- **RM**: Can approve/reject any request (override CM decision)
- **SuperAdmin**: Can view all requests

## Files Created/Modified

### New Files:
1. `DatabaseScripts/AttendanceCountApproval.sql` - Database schema
2. `DTO/AttendanceCountApprovalDto.cs` - DTOs and helper
3. `Data/tblAttendanceCountApproval.cs` - Entity model
4. `Data/tblAttendanceCountAttachment.cs` - Attachment entity
5. `Implementation/EmpAttendanceService_AttendanceCountApproval.cs` - Service methods (TO BE COPIED)

### Modified Files:
1. `Data/HRMSContext.cs` - Added DbSets
2. `Interfaces/IEmpAttendanceService.cs` - Added method signatures
3. `Controllers/EmpAttendanceController.cs` - Added API endpoints

## Testing Checklist

- [ ] Run database script successfully
- [ ] Copy service methods to main service file
- [ ] Build project without errors
- [ ] Test create attendance count approval
- [ ] Test CM approval
- [ ] Test CM rejection
- [ ] Test RM approval (with CM approved)
- [ ] Test RM approval (overriding CM rejection)
- [ ] Test RM rejection (overriding CM approval)
- [ ] Test unique constraint (duplicate ECode/Month/Year)
- [ ] Test pagination and filtering
- [ ] Test status calculation for all scenarios
- [ ] Test attachment upload

## Status Scenarios Examples

### Scenario 1: Normal Approval Flow
1. Employee submits → Status: "Pending CM"
2. CM approves → Status: "Pending RM"  
3. RM approves → Status: "Approved" ✓

### Scenario 2: RM Overrides CM Rejection
1. Employee submits → Status: "Pending CM"
2. CM rejects → Status: "Pending RM" (RM can override)
3. RM approves → Status: "Approved" ✓ (RM override)

### Scenario 3: RM Rejects Despite CM Approval
1. Employee submits → Status: "Pending CM"
2. CM approves → Status: "Pending RM"
3. RM rejects → Status: "Rejected" ✗ (RM final authority)

### Scenario 4: Both Reject
1. Employee submits → Status: "Pending CM"
2. CM rejects → Status: "Pending RM"
3. RM rejects → Status: "Rejected" ✗

## Troubleshooting

### Common Issues:

1. **"An attendance count approval request already exists"**
   - One request per ECode/Month/Year
   - Check if request already submitted for that month

2. **"This request has already been processed by CM/RM"**
   - Cannot resubmit after approval/rejection
   - CM/RM can only review once

3. **Build errors after implementation**
   - Ensure service methods are copied correctly
   - Check all using statements are present
   - Rebuild solution

## Future Enhancements

- [ ] Email notifications on approval/rejection
- [ ] Bulk approval feature for CM/RM
- [ ] Export to Excel functionality
- [ ] Dashboard with approval statistics
- [ ] Mobile app support
- [ ] Auto-approval based on certain criteria

