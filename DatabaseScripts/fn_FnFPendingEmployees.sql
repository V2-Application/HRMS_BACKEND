/* =====================================================================
   dbo.fn_FnFPendingEmployees  —  the ONE definition of "F&F pending".

   WHY THIS EXISTS
   The five Separation gap reports each carried their own copy-pasted F&F
   predicate, and every copy had drifted from what the FNF module itself
   means by "pending". The copies excluded only Paid / FNF DONE, so 10,328
   employees whose F&F was already TRANSFERRED were still being reported as
   F&F pending. Keeping the rule in one function is what stops that
   happening again — a change here reaches every report at once.

   DEFINITION — deliberately mirrors the FNF screen's own Pending branch
   (Services/FnfService.cs, "Pending branch (employees with no FNF yet)"),
   so a row in these reports means the same thing as a row on that screen:

       IsActive   = 0            separated
       IsStore    = 0            not a store-login account
       Ecode      LIKE 'V%'      real employee code
       DateOfLeft IS NOT NULL    leaving date recorded
       NO row in FNF_Header      F&F never started
       DateOfLeft within 12 months of @AsOfDate

   Note "no FNF_Header row at all" subsumes the old payment-status test: an
   employee with no F&F cannot have a completed payment. That is why the
   procs no longer need the FNF_Payment sub-query.

   @AsOfDate drives the 12-month window so a report run "as of" an earlier
   date gets the window that applied THEN, not the window that applies today.
   NULL falls back to today, matching the procs' own default.

   Inline table-valued (single RETURN, no BEGIN/END) so SQL Server folds it
   into the calling plan instead of running it per row.
   ===================================================================== */
CREATE OR ALTER FUNCTION dbo.fn_FnFPendingEmployees (@AsOfDate date)
RETURNS TABLE
AS
RETURN
(
    SELECT e.EmployeeId
    FROM dbo.tblEmployee e WITH (NOLOCK)
    WHERE e.IsActive = 0
      AND ISNULL(e.IsStore, 0) = 0
      AND e.Ecode LIKE 'V%'
      AND e.DateOfLeft IS NOT NULL
      AND TRY_CONVERT(date, e.DateOfLeft) >= DATEADD(YEAR, -1, ISNULL(@AsOfDate, CAST(GETDATE() AS date)))
      AND NOT EXISTS (SELECT 1 FROM dbo.FNF_Header h WITH (NOLOCK) WHERE h.EmployeeId = e.EmployeeId)
);
