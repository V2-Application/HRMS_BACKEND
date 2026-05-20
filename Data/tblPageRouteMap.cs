#nullable disable
using System;

namespace HRMSAPI.Data;

public partial class tblPageRouteMap
{
    public int PageRouteId { get; set; }
    public string RoutePath { get; set; }
    public int? SubModuleId { get; set; }
    public bool IsActive { get; set; }
    public string Notes { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime? LastUpdatedOn { get; set; }
}
