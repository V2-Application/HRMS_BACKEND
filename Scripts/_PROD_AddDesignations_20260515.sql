-- =============================================================================
-- Add 4 new designations. Idempotent — skips rows where DesignationName already
-- exists (case-insensitive).
-- =============================================================================
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

INSERT INTO dbo.tblDesignation (DesignationName)
SELECT v.Name
FROM (VALUES
    (N'Assistant of Recruitment Head'),
    (N'Audit Head'),
    (N'HR Manager'),
    (N'Recruitment Head')
) v(Name)
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.tblDesignation d
    WHERE LOWER(d.DesignationName) = LOWER(v.Name)
);

SELECT DesignationId, DesignationName, CreatedOn
FROM dbo.tblDesignation
WHERE DesignationName IN (
    N'Assistant of Recruitment Head',
    N'Audit Head',
    N'HR Manager',
    N'Recruitment Head'
)
ORDER BY DesignationId;
GO
