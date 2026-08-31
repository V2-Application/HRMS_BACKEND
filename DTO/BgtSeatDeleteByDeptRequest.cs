using System.Collections.Generic;

namespace HRMSAPI.DTO
{
    // Bulk delete of budget seats by department, optionally narrowed to specific stores.
    //   DeptSnos only            -> pan-India: every seat of those departments, all stores.
    //   DeptSnos + LocCodes      -> only those departments within those stores.
    // Both lists allow multiple selections; LocCodes is optional.
    public class BgtSeatDeleteByDeptRequest
    {
        public List<int> DeptSnos { get; set; } = new();
        public List<string> LocCodes { get; set; } = new();
    }

    // What a delete would remove, so the UI can confirm before anything is touched.
    public class BgtSeatDeleteByDeptPreview
    {
        public int TotalRows { get; set; }
        public bool PanIndia { get; set; }
        public int StoreCount { get; set; }
        public List<BgtSeatDeleteByDeptPreviewLine> Lines { get; set; } = new();
    }

    public class BgtSeatDeleteByDeptPreviewLine
    {
        public int DeptSno { get; set; }
        public string DepartmentName { get; set; }
        public int Rows { get; set; }
        public int Stores { get; set; }
    }
}
