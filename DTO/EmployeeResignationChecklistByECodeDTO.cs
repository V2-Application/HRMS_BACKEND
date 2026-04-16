using System.Text.Json.Serialization;

namespace HRMSAPI.DTO
{
    public class EmployeeResignationChecklistByECodeDTO
    {
        public string CheckListName { get; set; }
        public int CheckListId { get; set; }
        public bool? IsChecked { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Attachment { get; set; }
    }
    public class ResignationChecklistResponseDto
    {
        public int EmployeeResignationChecklistMasterId { get; set; }
        public string EmployeeId { get; set; }
        public bool IsAttachment { get; set; }
        public IFormFile? Attachment { get; set; } 
    }

    public class ResignationChecklistResponseListDto
    {
        public string ItemsJson { get; set; } = string.Empty; // JSON string containing list of ResignationChecklistItemDto
    }

    public class ResignationChecklistItemDto
    {
        public int EmployeeResignationChecklistMasterId { get; set; }
        public string EmployeeId { get; set; }
        public bool IsAttachment { get; set; }
    }

    public class GetEmployeeResignationChecklist
    {
        public int? CheckListId { get; set; }
        public string? CheckListName { get; set; }
        public bool? IsChecked { get; set; }
        public bool? IsAttachmentRequired { get; set; }

        public string? Attachment { get; set; }
    }
    
}
