-- ============================================================================
-- HUB/DC EMPLOYEE LOCATION REMAP - 2026-05-21
--
-- Context: today's 15:31 IST upload replaced tblLocation. Each old hub/DC
-- LocationId now has a corresponding "new" LocationId in the live table
-- with the same STCode. Employees still reference the old (orphan)
-- LocationIds, so the HUB/DC tab shows ~0 employees instead of ~1,400.
--
-- This script remaps tblEmployee.LocationId for every orphan hub/DC
-- LocationId to its current live counterpart with the matching STCode.
-- Only employees pointing at the listed orphan IDs are touched.
--
-- A fresh snapshot table is created first as the safety net.
-- ============================================================================
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

-- Snapshot (idempotent: only created if missing)
IF OBJECT_ID('dbo.tblEmployee_PreHubDcRemap_20260521') IS NULL
BEGIN
    SELECT * INTO dbo.tblEmployee_PreHubDcRemap_20260521 FROM dbo.tblEmployee;
    PRINT 'Snapshot created: dbo.tblEmployee_PreHubDcRemap_20260521';
END
ELSE
BEGIN
    PRINT 'Snapshot already exists; skipping snapshot step.';
END;
GO

BEGIN TRANSACTION;

-- Map orphan hub/DC LocationIds -> current live LocationIds (same STCode).
-- Pairs derived from this query (run at restore time):
--   ;WITH HubHist AS (SELECT DISTINCT LocationId, FIRST_VALUE(STCode) OVER (...)
--                     FROM tblLocation_History WHERE LocationName LIKE hub-pattern),
--        Live    AS (SELECT LocationId, STCode FROM tblLocation
--                     WHERE LocationName LIKE hub-pattern)
--   SELECT ... orphan->live pairs;

DECLARE @map TABLE (OrphanLocId INT, LiveLocId INT, STCode NVARCHAR(20));
INSERT INTO @map (OrphanLocId, LiveLocId, STCode) VALUES
    (5,    2459, 'DW01'),
    (3,    2438, 'DH24'),
    (335,  2444, 'DJ02'),
    (590,  2454, 'DP01'),
    (330,  2434, 'DB03'),
    (643,  2458, 'DU07'),
    (331,  2456, 'DU05'),
    (604,  2461, 'DX01'),
    (646,  2451, 'DN02'),
    (334,  2457, 'DU06'),
    (605,  2455, 'DR01'),
    (591,  2435, 'DB05'),
    (333,  2450, 'DN01'),
    (336,  2449, 'DM02'),
    (329,  2452, 'DO01'),
    (414,  2446, 'DK02'),
    (415,  2448, 'DM01'),
    (416,  2460, 'DW02'),
    (2,    2433, 'DB01'),
    (268,  2440, 'DH26'),
    (566,  2441, 'DH27');

UPDATE e
   SET e.LocationId = m.LiveLocId
FROM dbo.tblEmployee e
INNER JOIN @map m ON m.OrphanLocId = e.LocationId;

DECLARE @rows INT = @@ROWCOUNT;
PRINT CONCAT('Employees remapped: ', @rows);

COMMIT TRANSACTION;
GO

-- Verification
SELECT
    (SELECT COUNT(*) FROM dbo.tblEmployee e
     INNER JOIN dbo.tblLocation l ON l.LocationId = e.LocationId
     WHERE e.IsActive = 1
       AND (l.LocationName LIKE '%-HUB' OR l.LocationName LIKE '% HUB' OR l.LocationName LIKE '%-HUB-%'
         OR l.LocationName LIKE '%-RDC' OR l.LocationName LIKE '% RDC' OR l.LocationName LIKE '%-RDC-%'
         OR l.LocationName LIKE '%-DC'  OR l.LocationName LIKE '% DC'  OR l.LocationName LIKE '%-DC-%')
    ) AS ActiveHubDcEmployees_PostRemap;
GO
