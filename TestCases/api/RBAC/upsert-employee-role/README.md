# Employee Role Management APIs

## Overview
This document describes three API endpoints for managing employee role assignments in the HRMS system:

1. **Upsert Employee Role** - Assign or update roles for employees
2. **Get Employee Roles** - Retrieve all roles assigned to a specific employee
3. **Delete Employee Role** - Remove a specific role assignment from an employee

---

## 1. Upsert Employee Role API

### Endpoint
```
POST /api/RBAC/upsert-employee-role
```

### Request Body
```json
{
  "employeeRoleId": 0,
  "employeeId": 12345,
  "roleId": 1,
  "assignedBy": "admin@company.com",
  "lastUpdatedBy": "admin@company.com"
}
```

### Field Descriptions
- **employeeRoleId**: Set to 0 for new assignments (not used for updates)
- **employeeId**: The ID of the employee (required, must be > 0)
- **roleId**: The ID of the role to assign (required, must be > 0)
- **assignedBy**: Username/email of who is assigning the role
- **lastUpdatedBy**: Username/email of who is making the update

### Response
#### Success Response (200 OK)
```json
{
  "status": true,
  "message": "Employee role assigned successfully",
  "code": 200
}
```

#### Update Response (200 OK)
```json
{
  "status": true,
  "message": "Employee role updated successfully",
  "code": 200
}
```

---

## 2. Get Employee Roles API

### Endpoint
```
GET /api/RBAC/employee-roles/{employeeId}
```

### Path Parameters
- **employeeId**: The ID of the employee (required, must be > 0)

### Response
#### Success Response (200 OK)
```json
{
  "status": true,
  "message": "Found 2 role(s) for employee 12345",
  "code": 200,
  "data": [
    {
      "employeeRoleId": 1,
      "employeeId": 12345,
      "roleId": 1,
      "roleName": "Admin",
      "roleDescription": "Administrator role",
      "assignedOn": "2024-01-01T00:00:00",
      "assignedBy": "admin@company.com",
      "lastUpdatedOn": "2024-01-01T00:00:00",
      "lastUpdatedBy": "admin@company.com"
    }
  ]
}
```

---

## 3. Delete Employee Role API

### Endpoint
```
POST /api/RBAC/delete-employee-role
```

### Request Body
```json
{
  "employeeId": 12345,
  "roleId": 1,
  "deletedBy": "admin@company.com"
}
```

### Field Descriptions
- **employeeId**: The ID of the employee (required, must be > 0)
- **roleId**: The ID of the role to remove (required, must be > 0)
- **deletedBy**: Username/email of who is deleting the role (optional)

### Response
#### Success Response (200 OK)
```json
{
  "status": true,
  "message": "Role Admin removed from employee 12345 successfully",
  "code": 200
}
```

---

## Business Logic

### Upsert API
1. **Validation**: Checks if both employee and role exist in the system
2. **Duplicate Check**: If employee already has the role, updates the existing assignment
3. **New Assignment**: If employee doesn't have the role, creates a new assignment
4. **Audit Trail**: Automatically sets timestamps and tracks who made changes

### Get API
1. **Validation**: Checks if the employee exists
2. **Data Retrieval**: Fetches all role assignments with full role details
3. **Response**: Returns comprehensive information including role names and descriptions

### Delete API
1. **Validation**: Checks if both employee and role exist
2. **Assignment Check**: Verifies the employee has the specified role assigned
3. **Removal**: Permanently deletes the role assignment
4. **Transaction Safety**: Uses database transactions for data integrity

## Usage Examples

### New Role Assignment
```json
POST /api/RBAC/upsert-employee-role
{
  "employeeRoleId": 0,
  "employeeId": 12345,
  "roleId": 1,
  "assignedBy": "admin@company.com"
}
```

### Get Employee Roles
```
GET /api/RBAC/employee-roles/12345
```

### Remove Role Assignment
```json
POST /api/RBAC/delete-employee-role
{
  "employeeId": 12345,
  "roleId": 1,
  "deletedBy": "admin@company.com"
}
```

## Security Features
- Requires valid JWT token in Authorization header
- Validates that both employee and role exist before operations
- Uses database transactions for data integrity
- Comprehensive error handling and validation

## Error Responses
All APIs return appropriate HTTP status codes:
- **400 Bad Request**: Invalid input data or validation errors
- **200 OK**: Successful operation
- **500 Internal Server Error**: Server-side errors (handled gracefully)
