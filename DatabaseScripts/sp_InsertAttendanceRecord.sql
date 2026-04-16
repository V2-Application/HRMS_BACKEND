-- Stored Procedure: sp_InsertAttendanceRecord
-- Description: Inserts a new attendance record with geo location data
-- Parameters: All fields from AttendanceRecord table
-- Returns: The ID of the inserted record

CREATE OR ALTER PROCEDURE sp_InsertAttendanceRecord
    @EmployeeId BIGINT,
    @PunchType INT,
    @PunchTimeUtc DATETIME2,
    @Latitude DECIMAL(9,6),
    @Longitude DECIMAL(9,6),
    @WithinGeofence BIT,
    @DeviceInfo NVARCHAR(255) = NULL,
    @ClientIp NVARCHAR(45) = NULL,
    @Address NVARCHAR(500) = NULL,
    @ProofPath NVARCHAR(500) = NULL,
    @StatusId INT = 1,
    @LastUpdatedBy NVARCHAR(100) = 'System',
    @LastUpdatedOn DATETIME2 = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    
    DECLARE @LastPunchTime DATETIME2;
    DECLARE @LastPunchType INT;
    DECLARE @Today DATE = CAST(GETDATE() AS DATE);

    -- Check last punch details for the employee today
    SELECT TOP 1 
        @LastPunchTime = PunchTimeUtc,
        @LastPunchType = PunchType
    FROM AttendanceRecords WITH (NOLOCK)
    WHERE EmployeeId = @EmployeeId
      AND CAST(PunchTimeUtc AS DATE) = @Today
    ORDER BY PunchTimeUtc DESC;

    -- 1. 5-Minute Cooldown Check
    -- If user punches again within 5 minutes, BLOCK IT.
    -- (Assuming this rule applies to ALL punches, or at least same type. User said "if user punch in recent dont let him again")
    IF @LastPunchTime IS NOT NULL AND DATEDIFF(MINUTE, @LastPunchTime, @PunchTimeUtc) < 5
    BEGIN
        -- THROW error 50001: Cooldown active
        ;THROW 50001, 'You have punched recently. Please wait 5 minutes before trying again.', 1;
    END

    -- 2. Punch Sequence Validation
    -- "if employee today dont have any punch then dont let him punchout direct"
    -- PunchType 1 = In, 2 = Out
    IF @PunchType = 2 -- Punch Out
    BEGIN
        IF @LastPunchType IS NULL
        BEGIN
            -- No punches today, cannot Punch Out
             ;THROW 50002, 'You cannot Punch Out without Punching In first today.', 1;
        END
    END
          
    -- Set default values      
    IF @LastUpdatedOn IS NULL      
        SET @LastUpdatedOn = GETDATE(); -- Using local time instead of UTC      
          
    -- Insert the attendance record      
    INSERT INTO AttendanceRecords (      
        EmployeeId,      
        PunchType,      
        PunchTimeUtc,      
        Latitude,      
        Longitude,      
        WithinGeofence,      
        DeviceInfo,      
        ClientIp,      
        Address,      
        ProofPath,      
        StatusId,      
        LastUpdatedBy,      
        LastUpdatedOn      
    )      
    VALUES (      
        @EmployeeId,      
        @PunchType,      
        @PunchTimeUtc,      
        @Latitude,      
        @Longitude,      
        @WithinGeofence,      
        @DeviceInfo,      
        @ClientIp,      
        @Address,      
        @ProofPath,      
        @StatusId,      
        @LastUpdatedBy,      
        @LastUpdatedOn      
    );      
          
    -- Return the ID of the inserted record      
    SELECT SCOPE_IDENTITY() AS Id;      
END
