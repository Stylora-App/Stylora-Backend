namespace Stylora.Infrastructure.Http;

internal sealed class Http11ConnectionCloseHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.Headers.ConnectionClose = true;
        return base.SendAsync(request, cancellationToken);
    }
}
