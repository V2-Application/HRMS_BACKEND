-- Script to update existing UTC data to Indian Standard Time (IST)
-- This script converts all existing PunchTimeUtc and LastUpdatedOn values from UTC to IST
-- IST is UTC+5:30

-- Update PunchTimeUtc from UTC to IST (UTC+5:30)
UPDATE AttendanceRecords 
SET PunchTimeUtc = DATEADD(HOUR, 5, DATEADD(MINUTE, 30, PunchTimeUtc))
WHERE PunchTimeUtc IS NOT NULL;

-- Update LastUpdatedOn from UTC to IST (UTC+5:30) 
UPDATE AttendanceRecords 
SET LastUpdatedOn = DATEADD(HOUR, 5, DATEADD(MINUTE, 30, LastUpdatedOn))
WHERE LastUpdatedOn IS NOT NULL;

-- Optional: Add a comment to track this conversion
-- You can add a comment column or log this conversion for audit purposes

-- Verification query to check the conversion
-- Run this to see sample records after conversion
SELECT TOP 10 
    Id,
    EmployeeId,
    PunchTimeUtc,
    PunchType,
    LastUpdatedOn,
    'Converted to IST' as Conversion_Status
FROM AttendanceRecords 
ORDER BY PunchTimeUtc DESC;

-- Count of records updated
SELECT 
    COUNT(*) as TotalRecords,
    COUNT(PunchTimeUtc) as RecordsWithPunchTime,
    COUNT(LastUpdatedOn) as RecordsWithLastUpdated
FROM AttendanceRecords;

