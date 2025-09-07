using ApiGateway.Helpers;
using Polly;
using Polly.Extensions.Http;
using Yarp.ReverseProxy.LoadBalancing;

var builder = WebApplication.CreateBuilder(args);

// Define the circuit breaker policy
var circuitBreakerPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .Or<TaskCanceledException>()
    .CircuitBreakerAsync(
        handledEventsAllowedBeforeBreaking: 2,
        durationOfBreak: TimeSpan.FromSeconds(10),
        onBreak: (result, breakDelay) =>
            Console.WriteLine($"Circuit opened for {breakDelay.TotalSeconds} seconds"),
        onReset: () => Console.WriteLine("Circuit closed"),
        onHalfOpen: () => Console.WriteLine("Circuit half-open")
    );


var retryPolicy = HttpPolicyExtensions
    .HandleTransientHttpError() // retry on 5xx and network failures
    .Or<TaskCanceledException>()
    .WaitAndRetryAsync(
        retryCount: 3, // retry 3 times
        sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)), // exponential backoff
        onRetry: (outcome, timespan, retryAttempt, context) =>
        {
            Console.WriteLine($"Retry {retryAttempt} after {timespan.TotalSeconds}s due to {outcome.Exception?.Message ?? outcome.Result.StatusCode.ToString()}");
        });


// Register HttpClient with Polly policy
builder.Services.AddHttpClient("mycluster")
        .AddPolicyHandler(retryPolicy)
        .AddPolicyHandler(circuitBreakerPolicy);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.WebHost.UseKestrel()
               .UseUrls("http://+:80");

// Add Reverse Proxy
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

//builder.Services.AddSingleton<ILoadBalancingPolicy, WeightedRoundRobinPolicy>();

var app = builder.Build();

app.MapReverseProxy();

app.MapGet("/", () => "Apigateway is running!");

app.Run();
