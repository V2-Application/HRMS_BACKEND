namespace HRMSAPI.Interfaces
{
    public interface IDDCAttendanceService
    {
        Task<List<long>> InsertAttendanceAsync(List<DCAttendanceDTO> attendances);
    }
}


