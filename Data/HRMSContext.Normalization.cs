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

                // Every text field on these entities is stored UPPERCASE, not just PAN/bank.
                // Doing it here rather than at each call site means EVERY write path is covered
                // -- web forms, employee-master uploader, applicant uploader, approval flow,
                // bulk insert -- including any future one, with no chance of missing a site.
                //
                // Passwords, document paths/URLs, FaceData, RawText, *Json payloads and audit
                // columns are skipped inside UpperCaseNormalizer; see that class for why each
                // one would break if uppercased.
                switch (entry.Entity)
                {
                    case tblEmployee:
                    case Candidate:
                    case tempTblEmployee:
                    case tblExperience:
                    case tblQualification:
                    case tempTblExperience:
                    case tempTblQualification:
                    case tblEmployee_MedicalCard:
                        Utility.UpperCaseNormalizer.Apply(entry.Entity);
                        break;
                }

                // PAN / bank are additionally TRIMMED (the normalizer only changes case), so
                // keep these explicit — they were the original reason this hook exists.
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
