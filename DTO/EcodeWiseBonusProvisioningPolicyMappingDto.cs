using System;

namespace HRMSAPI.DTO
{
    public class EcodeWiseBonusProvisioningPolicyMappingUpsertDto
    {
        public string Ecode { get; set; }
        public Guid? BonusProvisioningPolicyMaster { get; set; }
    }

    public class EcodeWiseBonusProvisioningPolicyMappingDeleteDto
    {
        public Guid Id { get; set; }
    }

    public class EcodeWiseBonusProvisioningPolicyMappingResponseDto
    {
        public Guid Id { get; set; }
        public string Ecode { get; set; }
        public string FullName { get; set; }
        public Guid? BonusProvisioningPolicyMaster { get; set; }
        public string PolicyName { get; set; }
        public string Freq { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? CreatedOn { get; set; }
        public DateTime? UpdatedOn { get; set; }
        public string UpdatedBy { get; set; }
        public bool? IsActive { get; set; }
        public bool? IsDeleted { get; set; }
    }

    public class BonusProvisioningPolicyResponseDto
    {
        public Guid Id { get; set; }
        public string PolicyName { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? CreatedOn { get; set; }
        public DateTime? UpdatedOn { get; set; }
        public string UpdatedBy { get; set; }
        public bool? IsActive { get; set; }
        public bool? IsDeleted { get; set; }
    }
}

