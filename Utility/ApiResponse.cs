using System.Net;

public class ApiResponse<T>
{
    public HttpStatusCode StatusCode { get; set; }
    public bool Status { get; set; }
    public string Message { get; set; }
    public T Data { get; set; }

    public ApiResponse(HttpStatusCode statusCode, bool status, string message, T data)
    {
        StatusCode = statusCode;
        Status = status;
        Message = message;
        Data = data;
    }
}
