var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var app = builder.Build();


app.MapControllers();


app.MapGet("/", () => "MyService is running!");
app.MapGet("/fail", () => Results.StatusCode(500));

app.Run();