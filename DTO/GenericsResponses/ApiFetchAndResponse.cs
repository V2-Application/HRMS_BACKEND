using Roomsy.Abstract;

namespace Roomsy.DTOS.GenericsResponses
{
    public class ApiFetchAndResponse:ApiGenericResponseAbstract
    {
        public dynamic Data { get; set; } = String.Empty;
    }
}
