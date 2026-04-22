using System.Net;

namespace Stylora.Application.Exceptions;

public class ExternalServiceException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string ServiceName { get; }

    public ExternalServiceException(string serviceName, HttpStatusCode statusCode, string message)
        : base(message)
    {
        ServiceName = serviceName;
        StatusCode = statusCode;
    }

    public ExternalServiceException(string serviceName, HttpStatusCode statusCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ServiceName = serviceName;
        StatusCode = statusCode;
    }
}
