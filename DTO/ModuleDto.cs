namespace HRMSAPI.DTO
{
    public class ModuleDto
    {
        public int Id { get; set; }
        public  string? ModuleName { get; set; }
        public string? CreatedBy { get; set; }
        public List<SubModuleDto>? SubModules { get; set; } = new List<SubModuleDto>();
    }

    public class SubModuleDto
    {
        public int Id { get; set; }
        public  string? SubModuleName { get; set; }
        public string? CreatedBy { get; set; }
        public List<ActionDto>? Actions { get; set; } = new List<ActionDto>();
    }

    public class ActionDto
    {
        public int Id { get; set; }
        public  string? ActionName { get; set; }
        public string? CreatedBy { get; set; }
        public List<FurtherPartDto>? FurtherParts { get; set; } = new List<FurtherPartDto>();
    }

    public class FurtherPartDto
    {
        public int Id { get; set; }
        public  string? ActionFurtherPartName { get; set; }
        public string? CreatedBy { get; set; }
    }

}
