# MonthYear Column Change Summary

## Overview
Changed from separate `Month` (INT) and `Year` (INT) columns to a single `MonthYear` (NVARCHAR(10)) column in format **"MMM-YY"** (e.g., Jan-25, Feb-25).

## Changes Made

### 1. Database Schema (`DatabaseScripts/AttendanceCountApproval.sql`)

**Before:**
```sql
[Month] INT NOT NULL,
[Year] INT NOT NULL,
CONSTRAINT [UQ_AttendanceCount_ECode_Month_Year] UNIQUE([ECode], [Month], [Year])
```

**After:**
```sql
[MonthYear] NVARCHAR(10) NOT NULL, -- Format: MMM-YY (e.g., Jan-25, Feb-25)
CONSTRAINT [UQ_AttendanceCount_ECode_MonthYear] UNIQUE([ECode], [MonthYear])
```

### 2. Entity Model (`Data/tblAttendanceCountApproval.cs`)

**Before:**
```csharp
[Required]
public int Month { get; set; }

[Required]
public int Year { get; set; }
```

**After:**
```csharp
[Required]
[StringLength(10)]
public string MonthYear { get; set; } // Format: MMM-YY (e.g., Jan-25)
```

### 3. Create DTO (`DTO/AttendanceCountApprovalDto.cs`)

**Before:**
```csharp
[Required(ErrorMessage = "Month is required")]
[Range(1, 12, ErrorMessage = "Month must be between 1 and 12")]
public int Month { get; set; }

[Required(ErrorMessage = "Year is required")]
[Range(2020, 2100, ErrorMessage = "Year must be between 2020 and 2100")]
public int Year { get; set; }
```

**After:**
```csharp
[Required(ErrorMessage = "Month-Year is required")]
[StringLength(10, ErrorMessage = "Month-Year cannot exceed 10 characters")]
[RegularExpression(@"^(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)-\d{2}$", 
    ErrorMessage = "Month-Year must be in format MMM-YY (e.g., Jan-25)")]
public string MonthYear { get; set; }
```

### 4. Response DTO (`DTO/AttendanceCountApprovalDto.cs`)

**Before:**
```csharp
public int Month { get; set; }
public int Year { get; set; }
public string MonthName { get; set; }
```

**After:**
```csharp
public string MonthYear { get; set; } // Format: MMM-YY (e.g., Jan-25)
```

### 5. Service Implementation (`Implementation/EmpAttendanceService_AttendanceCountApproval.cs`)

**Before:**
```csharp
var existingRequest = await _context.tblAttendanceCountApprovals
    .FirstOrDefaultAsync(a => a.ECode == dto.ECode 
        && a.Month == dto.Month 
        && a.Year == dto.Year);

var approval = new tblAttendanceCountApproval
{
    ECode = dto.ECode,
    Month = dto.Month,
    Year = dto.Year,
    // ...
};
```

**After:**
```csharp
var existingRequest = await _context.tblAttendanceCountApprovals
    .FirstOrDefaultAsync(a => a.ECode == dto.ECode 
        && a.MonthYear == dto.MonthYear);

var approval = new tblAttendanceCountApproval
{
    ECode = dto.ECode,
    MonthYear = dto.MonthYear,
    // ...
};
```

## API Request/Response Changes

### Create Request - BEFORE
```json
{
  "eCode": "EMP001",
  "month": 10,
  "year": 2025,
  "attendanceCount": 25,
  "employeeRemarks": "Please approve"
}
```

### Create Request - AFTER
```json
{
  "eCode": "EMP001",
  "monthYear": "Jan-25",
  "attendanceCount": 25,
  "employeeRemarks": "Please approve"
}
```

### Response - BEFORE
```json
{
  "eCode": "EMP001",
  "month": 10,
  "year": 2025,
  "monthName": "October",
  "attendanceCount": 25
}
```

### Response - AFTER
```json
{
  "eCode": "EMP001",
  "monthYear": "Jan-25",
  "attendanceCount": 25
}
```

## Validation

### Format Validation
The `MonthYear` field is validated using RegEx:
```regex
^(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)-\d{2}$
```

### Valid Examples:
- ✅ Jan-25
- ✅ Feb-25
- ✅ Dec-24
- ✅ Mar-30

### Invalid Examples:
- ❌ January-25 (full month name)
- ❌ Jan-2025 (4-digit year)
- ❌ 01-25 (numeric month)
- ❌ jan-25 (lowercase)
- ❌ Jan25 (missing dash)

## Benefits of This Change

1. **Simplified Storage**: Single column instead of two
2. **Human Readable**: "Jan-25" is more intuitive than Month=1, Year=25
3. **Consistent Display**: No need to convert month number to name
4. **Easier Filtering**: Can filter by string matching
5. **Better UX**: Frontend can display directly without conversion

## Migration Notes

### If Database Already Has Data:

If you already have records with Month/Year columns, you'll need a migration script:

```sql
-- Add new column
ALTER TABLE tblAttendanceCountApproval 
ADD MonthYear NVARCHAR(10);

-- Migrate data (example for SQL Server)
UPDATE tblAttendanceCountApproval
SET MonthYear = 
    CASE Month
        WHEN 1 THEN 'Jan'
        WHEN 2 THEN 'Feb'
        WHEN 3 THEN 'Mar'
        WHEN 4 THEN 'Apr'
        WHEN 5 THEN 'May'
        WHEN 6 THEN 'Jun'
        WHEN 7 THEN 'Jul'
        WHEN 8 THEN 'Aug'
        WHEN 9 THEN 'Sep'
        WHEN 10 THEN 'Oct'
        WHEN 11 THEN 'Nov'
        WHEN 12 THEN 'Dec'
    END + '-' + RIGHT('0' + CAST(Year % 100 AS VARCHAR(2)), 2);

-- Make it required
ALTER TABLE tblAttendanceCountApproval
ALTER COLUMN MonthYear NVARCHAR(10) NOT NULL;

-- Drop old constraint
ALTER TABLE tblAttendanceCountApproval
DROP CONSTRAINT UQ_AttendanceCount_ECode_Month_Year;

-- Add new constraint
ALTER TABLE tblAttendanceCountApproval
ADD CONSTRAINT UQ_AttendanceCount_ECode_MonthYear UNIQUE(ECode, MonthYear);

-- Drop old columns
ALTER TABLE tblAttendanceCountApproval
DROP COLUMN Month, Year;

-- Recreate index
DROP INDEX IX_AttendanceCountApproval_ECode_Month_Year 
ON tblAttendanceCountApproval;

CREATE INDEX IX_AttendanceCountApproval_ECode_MonthYear 
ON tblAttendanceCountApproval(ECode, MonthYear);
```

### For Fresh Installation:
Simply run the updated `AttendanceCountApproval.sql` script.

## Files Modified

1. ✅ `DatabaseScripts/AttendanceCountApproval.sql`
2. ✅ `Data/tblAttendanceCountApproval.cs`
3. ✅ `DTO/AttendanceCountApprovalDto.cs`
4. ✅ `Implementation/EmpAttendanceService_AttendanceCountApproval.cs`
5. ✅ `DatabaseScripts/AttendanceCountApproval_README.md`

## Testing Checklist

- [ ] Create request with valid MonthYear format (e.g., "Jan-25")
- [ ] Test validation with invalid formats
- [ ] Verify unique constraint works (duplicate ECode + MonthYear)
- [ ] Test CM approval
- [ ] Test RM approval
- [ ] Verify response contains MonthYear in correct format
- [ ] Test filtering/searching by MonthYear
- [ ] Verify all existing functionality still works

## Frontend Changes Required

If you have a frontend application, update it to:
1. Send `monthYear` instead of `month` and `year`
2. Format user input to "MMM-YY" format
3. Display `monthYear` directly without conversion
4. Update form validation to match RegEx pattern

### Example Frontend Code (JavaScript/TypeScript):

```javascript
// Convert Date to MonthYear format
function formatMonthYear(date) {
  const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 
                  'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
  const month = months[date.getMonth()];
  const year = String(date.getFullYear()).slice(-2);
  return `${month}-${year}`;
}

// Example usage
const today = new Date();
const monthYear = formatMonthYear(today); // "Jan-25"

// API request
const payload = {
  eCode: "EMP001",
  monthYear: monthYear,
  attendanceCount: 25,
  employeeRemarks: "Please approve"
};
```

## Complete!

All changes have been implemented. Remember to:
1. Run the updated SQL script
2. Copy service methods to main service file
3. Build and test the application

