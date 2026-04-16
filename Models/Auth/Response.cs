using System.Net;

namespace HRMSAPI.Models.Auth
{
    public class Response
    {
        public bool Status { get; set; }
        public  string Message { get; set; } = string.Empty;
        public HttpStatusCode StatusCode { get; set; }
        public dynamic Data { get; set; }
    }

}
