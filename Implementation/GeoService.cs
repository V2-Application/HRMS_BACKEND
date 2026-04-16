using HRMSAPI.Data;
using HRMSAPI.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HRMSAPI.Implementation
{
    public class GeoService : IGeoService
    {
        private readonly HRMSContext _db;
        private readonly IConfiguration _cfg;
        public GeoService(HRMSContext db, IConfiguration cfg) { _db = db; _cfg = cfg; }


        // Haversine
        public double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000; // meters
            double dLat = ToRad(lat2 - lat1);
            double dLon = ToRad(lon2 - lon1);
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
            Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
            Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
            static double ToRad(double deg) => deg * Math.PI / 180;
        }


        public async Task<(tblLocation office, int radiusMeters)?> GetActiveOfficeAsync(tblEmployee emp)
        {
            // Get employee's assigned location
            var employeeOffice = await _db.tblLocations
                .Where(el => el.LocationId == emp.LocationId && (bool)el.IsActive)
                .FirstOrDefaultAsync();

            if (employeeOffice == null) return null;

            int defaultRadius = _cfg.GetValue<int>("Attendance:AllowedRadiusMeters", 150);
            int radius = employeeOffice.AllowedRadiusMeters ?? defaultRadius;

            return (employeeOffice, radius);
        }

    }
}
