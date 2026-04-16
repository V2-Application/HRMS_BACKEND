using System.Net;

namespace Roomsy.Abstract
{
    public class GenericResponseAbstract
    {
        public bool Status { get; set; }
        public string Message { get; set; } = String.Empty;
        public HttpStatusCode Code { get; set; }
    }
}
