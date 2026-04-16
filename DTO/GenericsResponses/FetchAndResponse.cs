using Roomsy.Abstract;

namespace Roomsy.DTOS.GenericsResponses
{
    public class FetchAndResponse:GenericResponseAbstract
    {
        public dynamic Data { get; set; } = String.Empty;
    }
}
