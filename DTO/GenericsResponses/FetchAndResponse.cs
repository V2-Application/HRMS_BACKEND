using Roomsy.Abstract;

namespace Roomsy.DTOS.GenericsResponses
{
    public class FetchAndResponse:GenericResponseAbstract
    {
        public dynamic Data { get; set; } = String.Empty;

        // Total number of matching rows (server-side pagination). 0 when not used.
        public int TotalCount { get; set; }
    }
}
