using System.Net;

namespace Roomsy.Abstract
{
    public class ApiGenericResponseAbstract
    {
        public bool Status { get; set; }
        public string Message { get; set; } = String.Empty;
    }
}
