using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Stylora.Application;
using Stylora.Infrastructure;
using Stylora.Infrastructure.Data;

// Load .env file from project root if it exists
var envPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", ".env");
if (File.Exists(envPath))
{
    foreach (var line in File.ReadAllLines(envPath))
    {
        var trimmed = line.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#')) continue;
        var idx = trimmed.IndexOf('=');
        if (idx <= 0) continue;
        var key = trimmed[..idx].Trim();
        var value = trimmed[(idx + 1)..].Trim();
        Environment.SetEnvironmentVariable(key, value);
    }
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddMemoryCache();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Stylora API",
        Version = "v1",
        Description = "AI-powered wardrobe and outfit management API"
    });
});

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

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "Stylora.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() 
            ? CookieSecurePolicy.None 
            : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
        options.LoginPath = "/api/auth/login";
        options.LogoutPath = "/api/auth/logout";
        options.AccessDeniedPath = "/api/auth/access-denied";
        
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var geminiApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? 
                   builder.Configuration["GeminiApiKey"] ?? 
                   throw new InvalidOperationException(
                       "Gemini API key not found. Set the GEMINI_API_KEY environment variable or add it to .env file.");

var rapidApiKey = Environment.GetEnvironmentVariable("RAPIDAPI_KEY") ?? 
                  builder.Configuration["RapidApiKey"] ?? 
                  throw new InvalidOperationException(
                      "RapidAPI key not found. Set the RAPIDAPI_KEY environment variable or add it to .env file.");

var connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING") ??
                       builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Database connection string not found. Set DATABASE_CONNECTION_STRING in your environment or root .env file.");
}

var clothingValidationSettings = new Stylora.Application.Models.ClothingValidationSettings();
builder.Configuration.GetSection("ClothingValidation").Bind(clothingValidationSettings);
clothingValidationSettings.SeedDirectoryPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, clothingValidationSettings.SeedDirectoryPath));

var outfitChatModelSettings = new Stylora.Application.Models.OutfitChatModelSettings();
builder.Configuration.GetSection("OutfitChatModel").Bind(outfitChatModelSettings);
outfitChatModelSettings.ModelId = Environment.GetEnvironmentVariable("GEMMA_MODEL_ID")
    ?? builder.Configuration["OutfitChatModel:ModelId"]
    ?? outfitChatModelSettings.ModelId;

var weatherApiSettings = new Stylora.Application.Models.WeatherApiSettings();
builder.Configuration.GetSection("WeatherApi").Bind(weatherApiSettings);
weatherApiSettings.ApiKey = Environment.GetEnvironmentVariable("OPENWEATHER_API_KEY")
    ?? builder.Configuration["WeatherApi:ApiKey"]
    ?? weatherApiSettings.ApiKey;

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(
    geminiApiKey,
    connectionString,
    rapidApiKey,
    clothingValidationSettings,
    outfitChatModelSettings,
    weatherApiSettings);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<StyloraDbContext>();
    try
    {
        await dbContext.Database.MigrateAsync();
    }
    catch (PostgresException ex) when (ex.MessageText.Contains("extension \"vector\" is not available", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            "PostgreSQL pgvector is required for clothing validation. Install the pgvector extension on the database server and restart the API.",
            ex);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database.");
        await dbContext.Database.EnsureCreatedAsync();
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Stylora API v1");
        options.RoutePrefix = "swagger";
    });
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowAngular");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
