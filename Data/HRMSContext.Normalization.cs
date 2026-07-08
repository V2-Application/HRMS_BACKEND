using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace HRMSAPI.Data
{
    /// <summary>
    /// Normalizes identity/bank fields to UPPERCASE on every insert/update so they are always
    /// stored in caps regardless of which path saves them (candidate form / employee master form /
    /// updates / bulk / approval). Applies to Candidate, tblEmployee and tempTblEmployee.
    /// </summary>
    public partial class HRMSContext
    {
        private static string ToUpperTrim(string value) =>
            string.IsNullOrWhiteSpace(value) ? value : value.Trim().ToUpperInvariant();

        private void NormalizeUpperCaseFields()
        {
            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.State != EntityState.Added && entry.State != EntityState.Modified)
                    continue;

                switch (entry.Entity)
                {
                    case tblEmployee e:
                        e.PAN_NO = ToUpperTrim(e.PAN_NO);
                        e.BANK_NAME = ToUpperTrim(e.BANK_NAME);
                        e.BANK_IFSC_CODE = ToUpperTrim(e.BANK_IFSC_CODE);
                        break;
                    case Candidate c:
                        c.PAN_NO = ToUpperTrim(c.PAN_NO);
                        c.BANK_NAME = ToUpperTrim(c.BANK_NAME);
                        c.BANK_IFSC_CODE = ToUpperTrim(c.BANK_IFSC_CODE);
                        break;
                    case tempTblEmployee t:
                        t.PAN_NO = ToUpperTrim(t.PAN_NO);
                        t.BANK_NAME = ToUpperTrim(t.BANK_NAME);
                        t.BANK_IFSC_CODE = ToUpperTrim(t.BANK_IFSC_CODE);
                        break;
                }
            }
        }

        public override int SaveChanges()
        {
            NormalizeUpperCaseFields();
            return base.SaveChanges();
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            NormalizeUpperCaseFields();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            NormalizeUpperCaseFields();
            return base.SaveChangesAsync(cancellationToken);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            NormalizeUpperCaseFields();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
    }
}
