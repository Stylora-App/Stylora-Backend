using Stylora.Application;
using Stylora.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Add CORS for Angular frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200", "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Get Gemini API Key from configuration
var geminiApiKey = builder.Configuration["GeminiApiKey"] ?? 
                   Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? 
                   "";

// Add Application and Infrastructure services
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(geminiApiKey);

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("AllowAngular");
app.UseAuthorization();

app.MapControllers();

app.Run();
