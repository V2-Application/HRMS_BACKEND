namespace HRMSAPI.DTO
{
    public class PagedEmployeeChangeLogDto
    {
        public List<EmployeeChangeLogDto> Data { get; set; } = new List<EmployeeChangeLogDto>();
        /// <summary>Total record count; null when not requested (returnTotal=false) for faster response.</summary>
        public int? TotalRecords { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int? TotalPages => TotalRecords.HasValue && PageSize > 0 ? (int)Math.Ceiling((double)TotalRecords.Value / PageSize) : null;
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => TotalPages.HasValue && CurrentPage < TotalPages.Value;
    }
}
