using System.Security.Claims;
using System.Threading;
using HRMSAPI.DTO;

namespace HRMSAPI.Interfaces
{
    /// <summary>
    /// Incentive service contract (single upsert with attachments, get, list, and bulk create without attachments).
    /// </summary>
    public interface IIncentiveService
    {
        /// <summary>
        /// Create or update a single incentive. Supports attachments via <paramref name="uploadsRoot"/>.
        /// </summary>
        Task<IncentiveDto?> UpsertAsync(
            IncentiveUpsertForm form,
            ClaimsIdentity? identity,
            string uploadsRoot,
            CancellationToken ct = default);

        /// <summary>
        /// Get one incentive by its ID.
        /// </summary>
        Task<IncentiveDto?> GetByIdAsync(
            long id,
            CancellationToken ct = default);

        /// <summary>
        /// Paged list of incentives with optional search and optional CreatedBy filter
        /// (used by "My Requests" to restrict rows to the calling user).
        /// </summary>
        Task<(List<IncentiveDto> Items, long TotalCount, int CurrentPageNumber)> ListAsync(
            int pageNumber,
            int pageSize,
            string? searchTerm,
            string? createdByFilter,
            CancellationToken ct = default);

        /// <summary>
        /// Bulk create incentives (no attachments). Inserts only NEW (Ecode, Month) rows.
        /// Returns inserted rows and skipped inputs (with reasons).
        /// </summary>
        Task<(List<IncentiveDto> Inserted, List<BulkSkipRow> Skipped)> BulkCreateAsync(
            IEnumerable<IncentiveUpsertForm> forms,
            ClaimsIdentity? identity,
            CancellationToken ct = default);
    }
}
