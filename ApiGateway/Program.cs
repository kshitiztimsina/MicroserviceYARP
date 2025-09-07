using Microsoft.AspNetCore.RateLimiting;
using Polly;
using Polly.Extensions.Http;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Register HttpClient for YARP with Polly policies
builder.Services.AddHttpClient("mycluster")
     .AddHttpMessageHandler<YarpRateLimitHandler>() // Check rate limit first
    .AddPolicyHandler(HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))))
    .AddPolicyHandler((sp, request) =>
    {
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("CircuitBreaker");

        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .Or<TaskCanceledException>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 2,
                durationOfBreak: TimeSpan.FromSeconds(10),
                onBreak: (outcome, breakDelay) =>
                {
                    logger.LogError(
                        "Circuit opened for {Delay}s due to {Reason}",
                        breakDelay.TotalSeconds,
                        outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString()
                    );
                },
                onReset: () => logger.LogInformation("Circuit closed"),
                onHalfOpen: () => logger.LogWarning("Circuit half-open, testing...")
            );
    })
    .AddPolicyHandler((sp, request) =>
    {
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("RetryPolicy");

        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (outcome, timespan, retryAttempt, context) =>
                {
                    logger.LogWarning(
                        "Retry {Attempt} after {Delay}s due to {Reason}",
                        retryAttempt,
                        timespan.TotalSeconds,
                        outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString()
                    );
                });
    });

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("customPolicy", opt =>
    {
        opt.PermitLimit = 3;      // max requests
        opt.Window = TimeSpan.FromSeconds(10);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
});

// Add Reverse Proxy

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.WebHost.UseKestrel()
               .UseUrls("http://+:80");


var app = builder.Build();


app.UseRateLimiter();
// Map Reverse Proxy
app.MapReverseProxy();


app.MapGet("/", () => "API Gateway with Polly is running!");

app.Run();
