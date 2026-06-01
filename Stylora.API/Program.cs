using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Stylora.Application;
using Stylora.Infrastructure;
using Stylora.Infrastructure.Data;

var envPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", ".env");
if (File.Exists(envPath))
{
    foreach (var line in File.ReadAllLines(envPath))
    {
        var trimmed = line.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#')) continue;
        var idx = trimmed.IndexOf('=');
        if (idx <= 0) continue;
        Environment.SetEnvironmentVariable(trimmed[..idx].Trim(), trimmed[(idx + 1)..].Trim());
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

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);

    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "JWT access token. Obtain via POST /api/auth/login or /api/auth/google."
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var corsOrigins = (Environment.GetEnvironmentVariable("CORS_ORIGINS") ?? "http://localhost:4200,http://localhost:3000")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET")
    ?? throw new InvalidOperationException("JWT_SECRET environment variable is required.");

var googleClientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID")
    ?? throw new InvalidOperationException("GOOGLE_CLIENT_ID environment variable is required.");

builder.Services.AddSingleton(new GoogleClientIdSettings(googleClientId));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };
        options.Events = new JwtBearerEvents
        {
            OnChallenge = context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var geminiApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ??
                   builder.Configuration["GeminiApiKey"] ??
                   throw new InvalidOperationException("Gemini API key not found.");

var rapidApiKey = Environment.GetEnvironmentVariable("RAPIDAPI_KEY") ??
                  builder.Configuration["RapidApiKey"] ??
                  throw new InvalidOperationException("RapidAPI key not found.");

var dbSection = builder.Configuration.GetSection("Database");
var dbPassword = Environment.GetEnvironmentVariable("DATABASE_PASSWORD")
    ?? throw new InvalidOperationException("DATABASE_PASSWORD environment variable is required.");

var connectionString = new NpgsqlConnectionStringBuilder
{
    Host     = dbSection["Host"]     ?? throw new InvalidOperationException("Database:Host is required."),
    Database = dbSection["Database"] ?? throw new InvalidOperationException("Database:Database is required."),
    Username = dbSection["Username"] ?? throw new InvalidOperationException("Database:Username is required."),
    Password = dbPassword
}.ConnectionString;

var clothingValidationSettings = new Stylora.Application.Models.ClothingValidationSettings();
builder.Configuration.GetSection("ClothingValidation").Bind(clothingValidationSettings);
clothingValidationSettings.SeedDirectoryPath = Path.GetFullPath(
    Path.Combine(builder.Environment.ContentRootPath, clothingValidationSettings.SeedDirectoryPath));

var clipPort = Environment.GetEnvironmentVariable("AI_CLIP_PORT");
if (clipPort != null)
    clothingValidationSettings.WorkerBaseUrl = $"http://localhost:{clipPort}/";

var outfitChatModelSettings = new Stylora.Application.Models.OutfitChatModelSettings();
builder.Configuration.GetSection("OutfitChatModel").Bind(outfitChatModelSettings);
outfitChatModelSettings.ModelId = Environment.GetEnvironmentVariable("GEMMA_MODEL_ID")
    ?? builder.Configuration["OutfitChatModel:ModelId"]
    ?? outfitChatModelSettings.ModelId;

var gemmaPort = Environment.GetEnvironmentVariable("AI_GEMMA_PORT");
if (gemmaPort != null)
    outfitChatModelSettings.WorkerBaseUrl = $"http://localhost:{gemmaPort}/";

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
    weatherApiSettings,
    jwtSecret);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<StyloraDbContext>();
    try
    {
        await dbContext.Database.MigrateAsync();
    }
    catch (Npgsql.PostgresException ex) when (ex.MessageText.Contains("extension \"vector\" is not available", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            "PostgreSQL pgvector extension is required. Install it on the database server and restart the API.", ex);
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
    app.UseHttpsRedirection();

app.UseCors("AllowAngular");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public record GoogleClientIdSettings(string ClientId);
