using System.Net;
using System.Threading.RateLimiting;

public class YarpRateLimitHandler : DelegatingHandler
{
    private readonly IDictionary<string, RateLimiter> _limiters;

    public YarpRateLimitHandler(IDictionary<string, RateLimiter> limiters)
    {
        _limiters = limiters;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Extract the service name from the request URI
        var host = request.RequestUri?.Host ?? string.Empty;

        if (_limiters.TryGetValue(host, out var limiter))
        {
            var lease = await limiter.AcquireAsync(1, cancellationToken);

            if (!lease.IsAcquired)
            {
                return new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                {
                    Content = new StringContent("Too many requests! Rate limit exceeded.")
                };
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
