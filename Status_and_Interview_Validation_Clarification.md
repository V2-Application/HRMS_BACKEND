# Status and Interview Validation Clarification

## Key Points

### 1. Status Validation is Dependent on Interview Details
- **CRITICAL BUSINESS RULE**: Only the following statuses are accepted (case-insensitive). Each is mapped to a fixed StatusId:
  - "Pending" (4)
  - "OnHold" (6)
  - "Not Interested" (9)
  - "Resume Shortlisted" (12)
  - "Rejected" (2)
  - "Upcoming Interview" (14)
  - "Schedule Pending" (13)
  - "Negotitation" (11)
  - "Complete" (15)
- If interview details are provided but incomplete (missing interviewer), the row will fail validation
- Interview-related statuses (like "Upcoming Interview", "Schedule Pending", etc.) require complete interview details

### 2. Interview Details and Status Relationship
- **If NO interview details are provided**: Only these statuses are allowed: "Pending", "Rejected", "Resume Shortlisted", "OnHold", "Not Interested"
- **If interview details are provided**: Must be complete (date + interviewer Ecode required)
- Interview-related statuses (Upcoming Interview, Schedule Pending, Complete, etc.) require complete interview details

### 3. When Interview Validation Triggers
- If you provide interview data, it must be complete (date + interviewer Ecode)
- If ALL interview fields are empty, only specific statuses are allowed (Pending, Rejected, Resume Shortlisted, OnHold, Not Interested)
- Interview validation runs if you provide at least ONE of:
  - Interview date (Interview-1 ACT or Interview-2 ACT)
  - Interviewer Ecode(s) (Interviewer-1 or Interviewer-2)
  - Interviewer status (Interviewer-1 Status or Interviewer-2 Status)

## Valid Scenarios

### ✅ Scenario 1: Allowed Statuses with No Interview Details
```
Status: "Pending" / "Rejected" / "Resume Shortlisted" / "OnHold" / "Not Interested"
Interview-1 ACT: (empty)
Interviewer-1: (empty)
Interviewer-1 Status: (empty)
Result: ✅ ALLOWED
```

### ❌ Scenario 2: Non-Allowed Status with No Interview Details
```
Status: "Round 1"
Interview-1 ACT: (empty)
Interviewer-1: (empty)
Interviewer-1 Status: (empty)
Result: ❌ ERROR - "Status 'Round 1' is not allowed when no interview details are provided. Allowed statuses without interview details: Pending, Rejected, Resume Shortlisted, OnHold, Not Interested"
```

### ❌ Scenario 3: Status with Partial Interview Data
```
Status: "Scheduled"
Interview-1 ACT: "15-01-2025 10:00"
Interviewer-1: (empty)
Interviewer-1 Status: (empty)
Result: ❌ ERROR - "Interview 1 interviewer Ecode(s) are required."
```

### ✅ Scenario 4: Complete Interview Data with Any Status
```
Status: "Round 1"
Interview-1 ACT: "15-01-2025 10:00"
Interviewer-1: "John Smith"
Interviewer-1 Status: "Selected"
Result: ✅ ALLOWED
```


## Invalid Scenarios

### ❌ Invalid Status
```
Status: "Round 2"  (Not in allowed list)
Result: ❌ ERROR - "Unknown Status(es): Round 2. Allowed statuses: [list]"
```

### ❌ Incomplete Interview Data
```
Status: "Round 1"
Interview-1 ACT: "15-01-2025 10:00"
Interviewer-1: (empty)  ← Missing interviewer
Result: ❌ ERROR - "Interview 1 interviewer Ecode(s) are required."
```

```
Status: "Round 1"
Interview-1 ACT: (empty)  ← Missing date
Interviewer-1: "John Smith"
Result: ❌ ERROR - "Interview 1 date/time is required when interviewer data is provided."
```

## Allowed Statuses (custom mapping)

Only the following statuses are accepted via Applicant Upload:
- Pending (4)
- OnHold (6)
- Not Interested (9)
- Resume Shortlisted (12)
- Rejected (2)
- Upcoming Interview (14)
- Schedule Pending (13)
- Negotitation (11)
- Complete (15)

Any other status value is rejected.

## Summary

| Status | Interview Details | Result |
|--------|-------------------|--------|
| "Pending", "Rejected", "Resume Shortlisted", "OnHold", "Not Interested" | No interview details | ✅ **ALLOWED** |
| Any other status | No interview details | ❌ **ERROR** - Only specific statuses allowed without interview details |
| Any status | Partial interview data (date only, no interviewer) | ❌ **ERROR** - Missing interviewer |
| Any status | Partial interview data (interviewer only, no date) | ❌ **ERROR** - Missing date |
| Any status | Complete interview data (date + interviewer) | ✅ **ALLOWED** |
| Invalid status (e.g., "Round 2") | Any | ❌ **ERROR** - Unknown status |

## Code Reference

The validation logic is in `ApplicantUploadService.cs`:

1. **Status Validation** (lines 113-130): Checks if status exists in database
2. **Status-Interview Relationship Validation** (lines 440-448): **NEW** - Enforces business rule
3. **Interview Validation** (lines 574-626): Validates interview data completeness

```csharp
// Business rule: If no interview details are provided, only certain statuses are allowed
if (interviewEntries.Count == 0 && !string.IsNullOrWhiteSpace(statusName))
{
    var statusTrimmed = statusName.Trim();
    if (!StatusesAllowedWithoutInterview.Any(s => string.Equals(s, statusTrimmed, StringComparison.OrdinalIgnoreCase)))
    {
        errors.Add($"Row {rowNumber}: Status '{statusTrimmed}' is not allowed when no interview details are provided. Allowed statuses without interview details: {string.Join(", ", StatusesAllowedWithoutInterview)}");
    }
}
```

## Test Cases to Verify

1. ✅ Upload with status "Pending" and NO interview details → Should succeed
2. ✅ Upload with status "Rejected" and NO interview details → Should succeed
3. ✅ Upload with status "Resume Shortlisted" and NO interview details → Should succeed
4. ✅ Upload with status "OnHold" and NO interview details → Should succeed
5. ✅ Upload with status "Not Interested" and NO interview details → Should succeed
6. ❌ Upload with status "Round 1" and NO interview details → Should fail (not in allowed list)
7. ❌ Upload with status "Completed" and NO interview details → Should fail (not in allowed list)
8. ❌ Upload with status "Scheduled" and NO interview details → Should fail (not in allowed list)
9. ❌ Upload with status "Round 2" → Should fail (invalid status - not in allowed list)
10. ❌ Upload with interview date but no interviewer → Should fail (missing interviewer)
11. ❌ Upload with interviewer but no date → Should fail (missing date)
12. ✅ Upload with status "Round 1" and complete interview details → Should succeed

