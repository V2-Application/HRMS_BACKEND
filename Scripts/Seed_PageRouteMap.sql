-- =============================================================================
-- Category: RBAC (route-level page access enforcement)
-- =============================================================================
-- Seeds dbo.tblPageRouteMap with the route → SubModuleId mapping.
-- All rows inserted with IsActive = 0 so nothing is enforced yet — rollout
-- is done by flipping IsActive = 1 per route (or in batches).
--
-- Re-runnable: WHERE NOT EXISTS guards prevent duplicates.
-- =============================================================================
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

;WITH RouteMap (RoutePath, SubModuleId, Notes) AS
(
    SELECT * FROM (VALUES
        -- Applicant / Candidate
        (N'/applicant/list',                                  CONVERT(int, 8),    N'Applicant List'),
        (N'/applicant/add',                                   CONVERT(int, 9),    N'Add Applicant'),
        (N'/applicant/update/:id',                            CONVERT(int, 9),    N'Add Applicant (edit)'),
        (N'/candidate/form_list',                             CONVERT(int, 10),   N'Candidate List'),

        -- Employee
        (N'/employees/list',                                  CONVERT(int, 1),    N'Employees Master'),
        (N'/employees/emp-transfer',                          CONVERT(int, 2),    N'Employees Transfer'),
        (N'/employee/add_new',                                CONVERT(int, 1),    N'Employees Master (add)'),
        (N'/employee/add_new/:id',                            CONVERT(int, 1),    N'Employees Master'),
        (N'/employee/update/:id',                             CONVERT(int, 1),    N'Employees Master (update)'),
        (N'/employee/details',                                CONVERT(int, 5),    N'Details'),
        (N'/employee/update/view/:id',                        CONVERT(int, 1),    N'Employees Master (view)'),
        (N'/employee/add_new/view/:id',                       CONVERT(int, 1),    N'Employees Master (view)'),
        (N'/employees/document_generate',                     CONVERT(int, 4),    N'Document Generate'),
        (N'/profile/profile-update-applications',             CONVERT(int, 3),    N'Profile Update Application'),

        -- Attendance
        (N'/attandance/track',                                CONVERT(int, 17),   N'View Attendance'),
        (N'/emp-attandance-list',                             CONVERT(int, 18),   N'Team Attendance'),
        (N'/regularize-request',                              CONVERT(int, 19),   N'Regularize Request'),
        (N'/geofence-request',                                CONVERT(int, 88),   N'Geofence Request'),
        (N'/attendance-regularization',                       CONVERT(int, 111),  N'Attendance Regularization'),
        (N'/overall-shift-master',                            CONVERT(int, 110),  N'Shift Master'),
        (N'/emp-shift-alignment',                             CONVERT(int, 115),  N'Emp Shift Alignment'),
        (N'/attendance-add-weekly-off',                       CONVERT(int, 119),  N'Add Weekly-Off'),

        -- Leave
        (N'/apply-leave',                                     CONVERT(int, 12),   N'My Leaves'),
        (N'/employee-leave-list',                             CONVERT(int, 11),   N'Requested Leaves'),
        (N'/emp-leave-status',                                CONVERT(int, 13),   N'Leaves Status'),

        -- Payroll
        (N'/payroll-summary',                                 CONVERT(int, 55),   N'Summary'),
        (N'/payroll',                                         CONVERT(int, 56),   N'Payroll'),
        (N'/process-salary',                                  CONVERT(int, 90),   N'Process Salary'),
        (N'/processed-salary',                                CONVERT(int, 101),  N'Processed Salary (Finance bucket)'),
        (N'/processed-salary-request',                        CONVERT(int, 109),  N'Processed Salary Request'),
        (N'/salary_recal',                                    CONVERT(int, 77),   N'Salary Recalculate'),
        (N'/salary_summery',                                  CONVERT(int, 55),   N'Salary Summary'),
        (N'/bank-paid',                                       CONVERT(int, 57),   N'Paid By Bank'),
        (N'/given-to-bank',                                   CONVERT(int, 58),   N'Given To Bank'),
        (N'/return-by-bank',                                  CONVERT(int, 59),   N'Return By Bank'),
        (N'/paid-by-cash',                                    CONVERT(int, 60),   N'Paid By Cash'),
        (N'/weekly-off-holiday',                              CONVERT(int, 61),   N'Weekly-Off Holiday'),
        (N'/weekly-off-policy',                               CONVERT(int, 62),   N'Weekly-Off Policy'),
        (N'/sldetails-view-downlasf1oad-salary-slips',        CONVERT(int, 63),   N'Salary Slips'),
        (N'/last-month-salary',                               CONVERT(int, 118),  N'Last-Month Salary'),

        -- Finance
        (N'/finance/process-salary',                          CONVERT(int, 101),  N'Processed Salary (Finance)'),
        (N'/finance/given-to-bank',                           CONVERT(int, 102),  N'Given To Bank (Finance)'),
        (N'/finance/paid-by-cash',                            CONVERT(int, 103),  N'Paid By Cash (Finance)'),
        (N'/finance/paid-by-bank',                            CONVERT(int, 104),  N'Paid By Bank (Finance)'),
        (N'/finance/return-by-bank',                          CONVERT(int, 105),  N'Return By Bank (Finance)'),

        -- Salary Master
        (N'/salary',                                          CONVERT(int, 47),   N'Salary'),
        (N'/payable-days',                                    CONVERT(int, 49),   N'Payable Days'),
        (N'/month',                                           CONVERT(int, 48),   N'Net Payable'),
        (N'/leave-l',                                         CONVERT(int, 50),   N'Leave'),
        (N'/gross-earning',                                   CONVERT(int, 51),   N'Gross Earning'),
        (N'/deduction',                                       CONVERT(int, 52),   N'Deduction'),
        (N'/pf',                                              CONVERT(int, 53),   N'PF'),
        (N'/esi',                                             CONVERT(int, 54),   N'ESI'),
        (N'/emp-final-data',                                  CONVERT(int, 92),   N'Emp Final Data'),
        (N'/salary/min-wages',                                CONVERT(int, 106),  N'Min Wages'),

        -- Incentive
        (N'/incentive/create',                                CONVERT(int, 81),   N'Create'),
        (N'/incentive/requests',                              CONVERT(int, 82),   N'My Requests'),
        (N'/incentive/cmd',                                   CONVERT(int, 83),   N'CMD Approvals'),
        (N'/incentive/hr',                                    CONVERT(int, 84),   N'HR Approvals'),

        -- Masters
        (N'/master/designations',                             CONVERT(int, 22),   N'Designations'),
        (N'/master/departments',                              CONVERT(int, 23),   N'Departments'),
        (N'/master/pf',                                       CONVERT(int, 93),   N'PF (Masters)'),
        (N'/master/lwf',                                      CONVERT(int, 94),   N'LWF'),
        (N'/master/pt',                                       CONVERT(int, 95),   N'PT'),
        (N'/master/esic-emp',                                 CONVERT(int, 96),   N'ESIC Emp'),
        (N'/master/gratuity',                                 CONVERT(int, 97),   N'Gratuity'),
        (N'/master/shift',                                    CONVERT(int, 98),   N'Shift'),
        (N'/master/machine',                                  CONVERT(int, 99),   N'Machine'),
        (N'/master/leave',                                    CONVERT(int, 100),  N'Leave Master'),
        (N'/master/seat',                                     CONVERT(int, 24),   N'Seat'),
        (N'/leave-master',                                    CONVERT(int, 100),  N'Leave Master'),

        -- Uploaders
        (N'/emp-zone-region-cluster-map-uploader',            CONVERT(int, 86),   N'Emp-Zone-Region-Cluster'),
        (N'/location-uploader',                               CONVERT(int, 26),   N'Location Master'),
        (N'/uploader/store-state_linking',                    CONVERT(int, 76),   N'Store-State Linking'),
        (N'/emp-store-assignment',                            CONVERT(int, 80),   N'Emp-Store Assignment'),
        (N'/bgt-seat-uploader',                               CONVERT(int, 27),   N'Bgt Seat Master'),
        (N'/ecode-seat-uploader',                             CONVERT(int, 28),   N'Emp Code Seat Master'),
        (N'/emp-attendance-uploader',                         CONVERT(int, 29),   N'Emp Attendance'),
        (N'/emp-tds-uploader',                                CONVERT(int, 30),   N'Emp Deduction'),
        (N'/applicability-uploader',                          CONVERT(int, 31),   N'Applicability'),
        (N'/salary-structure-uploader',                       CONVERT(int, 32),   N'Emp Salary Structure'),
        (N'/leave-opening-balance-uploader',                  CONVERT(int, 33),   N'Leave Opening Balance'),
        (N'/emp-personal-details-uploader',                   CONVERT(int, 34),   N'Emp Personal Details'),
        (N'/emp-statutory-details-uploader',                  CONVERT(int, 35),   N'Emp Statutory Details'),
        (N'/emp-degree-qualifications-uploader',              CONVERT(int, 36),   N'Emp Degree Qualifications'),
        (N'/emp-past-experience-uploader',                    CONVERT(int, 37),   N'Emp Past Experience'),
        (N'/emp-joining-releaving-uploader',                  CONVERT(int, 38),   N'Emp Joining Releaving'),
        (N'/emp-revised-dept-desg-loc-uploader',              CONVERT(int, 39),   N'Emp Revised Dept-Desg-Loc'),
        (N'/shift-alignment-uploader',                        CONVERT(int, 43),   N'Shift Alignment'),
        (N'/comp-off-uploader',                               CONVERT(int, 42),   N'Comp Off'),
        (N'/comp-off',                                        CONVERT(int, 42),   N'Comp Off'),
        (N'/payment-uploader',                                CONVERT(int, 25),   N'Additional Payment'),
        (N'/emp-bonus-uploader',                              CONVERT(int, 113),  N'Bonus'),
        (N'/uploaders/retention-bonus',                       CONVERT(int, 117),  N'Retention Bonus'),
        (N'/grauity-bonus-uploader',                          CONVERT(int, 40),   N'Gratuity & Bonus'),
        (N'/gratuity-bonus',                                  CONVERT(int, 40),   N'Gratuity & Bonus'),
        (N'/gratuitybonus',                                   CONVERT(int, 40),   N'Gratuity & Bonus'),
        (N'/emp-salary-status-uploader',                      CONVERT(int, 41),   N'Emp Salary Status'),
        (N'/salary-status',                                   CONVERT(int, 41),   N'Emp Salary Status'),

        -- Views
        (N'/location-master-view',                            CONVERT(int, 72),   N'Location Master View'),
        (N'/bgt_seat-master-view',                            CONVERT(int, 73),   N'Bgt Seat Master View'),
        (N'/emp_code-seat_master-view',                       CONVERT(int, 46),   N'Emp Code Seat Master'),

        -- Holiday Master
        (N'/holiday-master/groups',                           CONVERT(int, 74),   N'Groups'),
        (N'/holiday-master/holidays',                         CONVERT(int, 75),   N'Holidays'),

        -- Separation
        (N'/sepration/record_resignation',                    CONVERT(int, 65),   N'Resignation (Self)'),
        (N'/sepration/record_resignation/:id',                CONVERT(int, 65),   N'Resignation (Self)'),
        (N'/sepration/record_resignation_others',             CONVERT(int, 66),   N'Resignation (Others)'),
        (N'/sepration/resignation_status',                    CONVERT(int, 67),   N'Resignation Status'),
        (N'/sepration/resignation_applications',              CONVERT(int, 68),   N'Resignation Applications'),

        -- F&F
        (N'/fnf',                                             CONVERT(int, 79),   N'F&F'),

        -- Openings
        (N'/openingslistView',                                CONVERT(int, 7),    N'Openings List View'),
        (N'/jd-list',                                         CONVERT(int, 78),   N'JD'),

        -- NSO Routing
        (N'/new-stores',                                      CONVERT(int, 21),   N'New Stores'),

        -- Locations / Geofence
        (N'/Geo-fence',                                       CONVERT(int, 85),   N'Geofence'),

        -- BGV
        (N'/BGV',                                             CONVERT(int, 120),  N'BGV'),

        -- Vendor
        (N'/vendor/master-list',                              CONVERT(int, 116),  N'Vendor List'),

        -- Settings / RBAC
        (N'/rbac-panel',                                      CONVERT(int, 70),   N'RBAC Panel'),
        (N'/role-assign',                                     CONVERT(int, 91),   N'Role Assignment'),
        (N'/employee-role_list',                              CONVERT(int, 91),   N'Role Assignment'),
        (N'/settings/modules-catalog',                        CONVERT(int, 71),   N'Modules Catalog'),
        (N'/employee-logs',                                   CONVERT(int, 108),  N'Employee Logs'),
        (N'/attendance-logs',                                 CONVERT(int, 112),  N'Attendance Logs')
    ) AS v(RoutePath, SubModuleId, Notes)
)
INSERT INTO dbo.tblPageRouteMap (RoutePath, SubModuleId, IsActive, Notes)
SELECT rm.RoutePath, rm.SubModuleId, 0, rm.Notes
FROM RouteMap rm
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.tblPageRouteMap pm WHERE pm.RoutePath = rm.RoutePath
);

PRINT CONCAT('Seed insert complete. Rows inserted: ', @@ROWCOUNT);
GO
