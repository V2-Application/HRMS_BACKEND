namespace HRMSAPI.DTO
{
    public sealed class LocationGeoUpdateRequest
    {
        public int? LocationId { get; set; }               // must be provided

        public decimal? StoreLong { get; set; }            // DECIMAL(9,6)
        public decimal? StoreLat { get; set; }             // DECIMAL(9,6)
        public string? ADDRESS { get; set; }
        public int? AllowedRadiusMeters { get; set; }
        public bool? IsGeofenceEnabled { get; set; }
    }


    public sealed class LocationforgeoDto
    {
        public int? LocationId { get; set; }
        public string? STCode { get; set; }
        public string? LocationName { get; set; }
        public int? StateId { get; set; }
        public DateTime? CreatedOn { get; set; }
        public int? LocationCategoryId { get; set; }
        public int? ZoneId { get; set; }
        public int? RegionId { get; set; }
        public int? ClusterId { get; set; }
        public bool? IsActive { get; set; }
        public decimal? StoreLong { get; set; }
        public decimal? StoreLat { get; set; }
        public int? AllowedRadiusMeters { get; set; }
        public bool? IsDeleted { get; set; }
        public string? OpeningDate { get; set; }
        public bool? IsAllowOvertimePayment { get; set; }
        public string? ADDRESS { get; set; }
        public bool? IsGeofenceEnabled { get; set; }
    }
}
