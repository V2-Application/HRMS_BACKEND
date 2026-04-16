# EcodeWise Bonus Provisioning Policy Mapping API Test Cases

This directory contains test cases for the EcodeWise Bonus Provisioning Policy Mapping APIs.

## API Endpoints

### 1. GET /api/EcodeWiseBonusProvisioningPolicyMapping
Retrieves all active mappings (IsActive = 1 and IsDeleted = 0) with employee full names.

**Test Cases:**
- `get-all-mappings/A_get_all_active_mappings.json` - Get all active mappings

### 2. POST /api/EcodeWiseBonusProvisioningPolicyMapping/upsert
Creates or updates a mapping based on Ecode.

**Test Cases:**
- `upsert-mapping/A_create_new_mapping.json` - Create new mapping
- `upsert-mapping/B_update_existing_mapping.json` - Update existing mapping
- `upsert-mapping/C_upsert_with_missing_ecode.json` - Validation error for missing Ecode
- `upsert-mapping/D_upsert_with_null_body.json` - Validation error for null body
- `upsert-mapping/E_upsert_without_authentication.json` - Unauthorized access

### 3. POST /api/EcodeWiseBonusProvisioningPolicyMapping/delete
Soft deletes a mapping by setting IsActive = 0 and IsDeleted = 1.

**Test Cases:**
- `delete-mapping/A_delete_existing_mapping.json` - Delete existing mapping
- `delete-mapping/B_delete_with_invalid_id.json` - Validation error for invalid ID
- `delete-mapping/C_delete_nonexistent_mapping.json` - Not found error
- `delete-mapping/D_delete_with_null_body.json` - Validation error for null body

## Business Logic

### Upsert Logic
1. **If Ecode exists** with IsActive = 1 and IsDeleted = 0:
   - Updates `BonusProvisioningPolicyMaster`
   - Sets `UpdatedBy` from JWT EmployeeId claim
   - Sets `UpdatedOn` to current UTC timestamp

2. **If Ecode doesn't exist**:
   - Creates new record with generated GUID
   - Sets `CreatedBy` from JWT EmployeeId claim
   - Sets `CreatedOn` to current UTC timestamp
   - Sets `IsActive = true` and `IsDeleted = false`

### Delete Logic
- Performs soft delete (not physical delete)
- Sets `IsActive = false`
- Sets `IsDeleted = true`
- Sets `UpdatedOn` to current UTC timestamp

### GET Logic
- Filters records where IsActive = 1 and IsDeleted = 0
- Joins with `tblEmployee` table on Ecode
- Returns employee FullName (uses FULL_NAME if available, otherwise FirstName + LastName)
- Uses AsNoTracking() and AsQueryable() for performance

## Authentication

All endpoints require JWT authentication. The JWT token must contain:
- `EmployeeId` claim (used for CreatedBy and UpdatedBy)

## Testing Instructions

1. **Setup:**
   - Ensure the API is running
   - Obtain a valid JWT token with EmployeeId claim
   - Replace `{your_jwt_token}` in test case files with actual token

2. **Using Postman/Insomnia:**
   - Import the JSON test cases
   - Update the JWT token in headers
   - Update GUIDs and Ecode values as needed
   - Execute test cases in order

3. **Using HTTP Client (VS Code REST Client):**
   - Create `.http` file with test requests
   - Use variables for base URL and token
   - Execute requests directly from editor

4. **Using cURL:**
   ```bash
   # Example: Get all mappings
   curl -X GET "http://localhost:5151/api/EcodeWiseBonusProvisioningPolicyMapping" \
     -H "Authorization: Bearer YOUR_JWT_TOKEN" \
     -H "Content-Type: application/json"
   
   # Example: Upsert mapping
   curl -X POST "http://localhost:5151/api/EcodeWiseBonusProvisioningPolicyMapping/upsert" \
     -H "Authorization: Bearer YOUR_JWT_TOKEN" \
     -H "Content-Type: application/json" \
     -d '{"ecode":"E-001","bonusProvisioningPolicyMaster":"550e8400-e29b-41d4-a716-446655440000"}'
   
   # Example: Delete mapping
   curl -X POST "http://localhost:5151/api/EcodeWiseBonusProvisioningPolicyMapping/delete" \
     -H "Authorization: Bearer YOUR_JWT_TOKEN" \
     -H "Content-Type: application/json" \
     -d '{"id":"550e8400-e29b-41d4-a716-446655440000"}'
   ```

## Test Data Requirements

Before running test cases, ensure:
1. Database has `EcodeWiseBonusProvisioningPolicyMapping` table
2. Database has `tblEmployee` table with sample employees
3. Sample Ecode values exist in `tblEmployee` table (e.g., "E-001")
4. Valid `BonusProvisioningPolicyMaster` GUIDs exist (if foreign key constraint exists)

## Expected Test Flow

1. **Create Test:**
   - Run `A_create_new_mapping.json`
   - Verify record is created with correct CreatedBy

2. **Get Test:**
   - Run `A_get_all_active_mappings.json`
   - Verify created record appears with FullName

3. **Update Test:**
   - Run `B_update_existing_mapping.json` with same Ecode
   - Verify BonusProvisioningPolicyMaster is updated
   - Verify UpdatedBy and UpdatedOn are set

4. **Delete Test:**
   - Run `A_delete_existing_mapping.json`
   - Verify record is soft deleted (IsActive = false, IsDeleted = true)
   - Run GET again to verify record doesn't appear

5. **Validation Tests:**
   - Run all validation error test cases
   - Verify appropriate error messages and status codes

## Notes

- All timestamps are in UTC
- GUIDs should be replaced with actual values from your database
- Ecode values should match existing employees in tblEmployee table
- The API uses soft delete, so deleted records remain in database but are filtered out in GET requests

