using System;

namespace HRMSAPI.DTO
{
    public class GroupUpsertDto
    {
        public int? Id {get;set;}
        public string? GroupName { get; set; }
    }

    public class GroupResponseDto
    {
        public int Id { get; set; }
        public string GroupName { get; set; }
        public bool? IsActive { get; set; }
        public bool? IsDeleted { get; set; }
        public DateTime? CreatedOn { get; set; }
        public string CreatedBy { get; set; }
        public string UpdatedBy { get; set; }
        public DateTime? UpdatedOn { get; set; }
    }
}
