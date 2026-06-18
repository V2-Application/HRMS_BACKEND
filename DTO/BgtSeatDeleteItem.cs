namespace HRMSAPI.DTO
{
    // Identifies a specific BGT seat entry to delete (LOC_CODE + DEPT_SNO + DESG_SNO + SEAT_MASTER_NO).
    public class BgtSeatDeleteItem
    {
        public string StCode { get; set; }
        public string DeptSno { get; set; }
        public string DesgSno { get; set; }
        public string SeatNo { get; set; }
    }
}
