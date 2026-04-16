namespace HRMSAPI.DTO
{
    public class RolePermissionPost
    {
        public int RoleId { get; set; }
        public string? RoleName { get; set; }
        public List<ModuleRolePermissionPostDto> Modules { get; set; } = new List<ModuleRolePermissionPostDto>();
    }
    public class ModuleRolePermissionPostDto
    {
        public int ModuleId { get; set; }
        public int? ModuleRefId { get; set; }
        public string? ModuleName { get; set; }
        public bool ModuleStatus { get; set; }
        public List<SubModuleRolePermissionPostDto> SubModules { get; set; } = new List<SubModuleRolePermissionPostDto>();
    }

    public class SubModuleRolePermissionPostDto
    {
        public int SubModuleId { get; set; }
        public int? SubModuleRefId { get; set; }
        public string? SubModuleName { get; set; }
        public bool SubModuleStatus { get; set; }
        public List<ActionRolePermissionPostDto> Actions { get; set; } = new List<ActionRolePermissionPostDto>();
    }

    public class ActionRolePermissionPostDto
    {
        public int ActionId { get; set; }
        public int? ActionRefId { get; set; }
        public string? ActionName { get; set; }
        public bool ActionStatus { get; set; }
        public List<FurtherPartRolePermissionPostDto> FurtherParts { get; set; } = new List<FurtherPartRolePermissionPostDto>();
    }

    public class FurtherPartRolePermissionPostDto
    {
        public int ActionFurtherPartId { get; set; }
        public int? ActionFurtherPartRefId { get; set; }
        public string? ActionFurtherPartName { get; set; }
        public bool FurtherPartStatus { get; set; }
    }
}
