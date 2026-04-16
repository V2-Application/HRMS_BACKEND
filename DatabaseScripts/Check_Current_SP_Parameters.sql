-- Check current stored procedure parameters
SELECT 
    p.name AS ParameterName,
    t.name AS DataType,
    p.max_length AS MaxLength,
    p.is_nullable AS IsNullable
FROM sys.parameters p
INNER JOIN sys.types t ON p.user_type_id = t.user_type_id
WHERE p.object_id = OBJECT_ID('[dbo].[sp_FNF_GetEmployeesByCode]')
ORDER BY p.parameter_id;
