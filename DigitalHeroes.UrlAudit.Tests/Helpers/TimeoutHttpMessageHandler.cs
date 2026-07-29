using System.Net;

namespace DigitalHeroes.UrlAudit.Tests.Helpers
{
    public class TimeoutHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            throw new TaskCanceledException();
        }
    }
}