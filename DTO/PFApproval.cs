using System.ComponentModel.DataAnnotations.Schema;

namespace HRMSAPI.DTO
{
    public class PFApprovalRequest
    {
        public string E_Code { get; set; }
        public string _Month { get; set; }          // "MMM-yy"
        public string Challan_No { get; set; }
        public IFormFile Attachment { get; set; }
       // public string CreatedBy { get; set; }
    }

}
