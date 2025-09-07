using Microsoft.AspNetCore.Mvc;

namespace MyService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Sunny", "Cloudy", "Rainy", "Windy", "Snowy", "Foggy"
        };

        private readonly ILogger<WeatherController> _logger;

        public WeatherController(ILogger<WeatherController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Get()
        {
            var rng = new Random();
            var result = Enumerable.Range(1, 3).Select(_ =>
                Summaries[rng.Next(Summaries.Length)]
            );
          
            // Include container name
            var instanceName = Environment.GetEnvironmentVariable("InstanceName") ?? Environment.MachineName;

            return Ok(new
            {
                Instance = instanceName,
                Weather = result
            });
        }
    }
}
