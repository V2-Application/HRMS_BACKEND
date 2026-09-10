#nullable disable
using System;
using System.Collections.Generic;

namespace HRMSAPI.Data;

/// <summary>
/// HR-maintained role list (Masters -> Role Master), picked by the "Role" field
/// on the employee profile and the candidate page.
///
/// NOT the V2 Parivar portal role: tblRole holds the portal/RBAC roles that
/// drive page access and the approval layers. These two are deliberately
/// separate lists and appear as separate columns in the employee master export.
/// </summary>
public partial class tblRoleMaster
{
    public int RoleMasterId { get; set; }

    public string RoleName { get; set; }

    public string Description { get; set; }

    public bool IsActive { get; set; }

    public string CreatedBy { get; set; }

    public DateTime CreatedOn { get; set; }

    public string LastUpdatedBy { get; set; }

    public DateTime? LastUpdatedOn { get; set; }

    // NO navigation collection here on purpose.
    //
    // An ICollection<tblEmployee> without an explicit relationship makes EF
    // invent a shadow foreign key ("tblRoleMasterRoleMasterId") on tblEmployee
    // and select it in EVERY employee query -> "Invalid column name
    // 'tblRoleMasterRoleMasterId'", which blanks the employee profile and any
    // other screen that loads an employee.
    //
    // tblEmployee.RoleMasterId is read directly, and the Role Master page counts
    // holders with explicit queries, so no navigation is needed.
}
