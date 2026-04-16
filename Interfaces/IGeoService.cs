using HRMSAPI.Data;

namespace HRMSAPI.Interfaces
{
    public interface IGeoService
    {
        double DistanceMeters(double lat1, double lon1, double lat2, double lon2);
        Task<(tblLocation office, int radiusMeters)?> GetActiveOfficeAsync(tblEmployee emp);

    }
}
