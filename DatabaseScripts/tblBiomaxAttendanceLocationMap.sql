-- Biomax Attendance Location -> Store (STCode) mapping.
-- Maps a biometric DEVICE LOCATION (device name from the Biomax export) to a store ST-CODE.
-- Managed via the "Biomax Attendance Location Mapping" page (list / add / edit / delete / Excel upload).
-- Additive only. Delete is a SOFT delete (IsDeleted=1). Never delete/truncate.
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'tblBiomaxAttendanceLocationMap' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.tblBiomaxAttendanceLocationMap (
        Id             INT IDENTITY(1,1) PRIMARY KEY,
        DeviceLocation NVARCHAR(200) NOT NULL,   -- Excel "Device Name" (the device/location label)
        STCode         NVARCHAR(50)  NOT NULL,   -- Excel "ST-CODE"
        IsActive       BIT           NOT NULL DEFAULT(1),
        IsDeleted      BIT           NOT NULL DEFAULT(0),
        CreatedBy      NVARCHAR(100) NULL,
        CreatedOn      DATETIME      NOT NULL DEFAULT(GETDATE()),
        UpdatedBy      NVARCHAR(100) NULL,
        UpdatedOn      DATETIME      NULL
    );

    -- One active mapping per device location (upsert key).
    CREATE UNIQUE INDEX UX_BiomaxAttLoc_Device
        ON dbo.tblBiomaxAttendanceLocationMap (DeviceLocation)
        WHERE IsDeleted = 0;

    -- Fast lookup by store code.
    CREATE INDEX IX_BiomaxAttLoc_STCode
        ON dbo.tblBiomaxAttendanceLocationMap (STCode)
        INCLUDE (DeviceLocation, IsActive, IsDeleted);
END
