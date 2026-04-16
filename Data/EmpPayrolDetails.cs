using System;
using System.Collections.Generic;

namespace HRMSAPI.Data
{
    public partial class EmpPayrolDetails
    {
        public int bigid { get; set; }

        public string E_Code { get; set; }

        public string Acc_Number { get; set; }

        public string Amount { get; set; }
        public string UTR { get; set; }
        public string createdBy { get; set; }
        public string updatedBy { get; set; }
        public string updatedOn { get; set; }
    }
}
