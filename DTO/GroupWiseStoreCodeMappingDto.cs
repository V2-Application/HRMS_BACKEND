using System;

namespace HRMSAPI.DTO
{
    public class GroupWiseStoreCodeMappingUpsertDto
    {
        //public int? Id { get; set; }
        public int? GroupId { get; set; }
        public string ST_CD { get; set; }
    }

    public class GroupWiseStoreCodeMappingResponseDto
    {
        public int Id { get; set; }
        public int? GroupId { get; set; }
        public string GroupName { get; set; }
        public string ST_CD { get; set; }
        public bool? IsActive { get; set; }
        public bool? IsDeleted { get; set; }
        public DateTime? CreatedOn { get; set; }
        public string CreatedBy { get; set; }
        public string UpdatedBy { get; set; }
        public DateTime? UpdatedOn { get; set; }
    }

    public class GroupWiseStoreCodeMappingUploadDto
    {
        public string GroupName { get; set; }
        public string ST_CD { get; set; }
    }
}
