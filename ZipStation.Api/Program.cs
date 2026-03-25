using System.Text.Json.Serialization;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MongoDB.Driver;
using Serilog;
using Serilog.Events;
using ZipStation.Api.Helpers;
using ZipStation.Business.Helpers;

var builder = WebApplication.CreateBuilder(args);

// --- Configuration ---
var appConfig = builder.Configuration.Get<AppConfig>() ?? new AppConfig();
builder.Services.Configure<AppConfig>(builder.Configuration);

// --- Encryption ---
var encryptionKey = appConfig.EncryptionKey;
if (string.IsNullOrEmpty(encryptionKey))
{
    // Auto-generate a key on first run — user should persist this in .env
    encryptionKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
    Log.Warning("No EncryptionKey configured. Generated a temporary key. Set EncryptionKey in your .env or appsettings to persist encrypted data across restarts.");
}
ZipStation.Business.Helpers.EncryptionHelper.Initialize(encryptionKey);

// --- Serilog ---
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", appConfig.AppName)
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// --- CORS ---
var allowedOrigins = appConfig.AllowedOrigins
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy("ZipStationCors", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// --- Controllers ---
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// --- API Versioning ---
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
})
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// --- Swagger ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Zip Station API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Firebase JWT Bearer token. Enter: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// --- Firebase JWT Authentication ---
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = appConfig.Firebase.BearerTokenIssuer;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = appConfig.Firebase.BearerTokenIssuer,
            ValidateAudience = true,
            ValidAudience = appConfig.Firebase.BearerTokenAudience,
            ValidateLifetime = true
        };
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Log.Warning("JWT auth failed: {Error}", context.Exception.Message);
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Log.Information("JWT validated for {User}", context.Principal?.Identity?.Name);
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// --- Firebase Admin SDK (for user management) ---
var fbProjectId = Environment.GetEnvironmentVariable("FIREBASE_ADMIN_PROJECT_ID")
    ?? appConfig.Firebase?.BearerTokenAudience;
var fbClientEmail = Environment.GetEnvironmentVariable("FIREBASE_ADMIN_CLIENT_EMAIL");
var fbPrivateKey = Environment.GetEnvironmentVariable("FIREBASE_ADMIN_PRIVATE_KEY");
if (!string.IsNullOrEmpty(fbClientEmail) && !string.IsNullOrEmpty(fbPrivateKey))
{
    try
    {
        // Reconstruct the JSON that GoogleCredential expects
        var serviceAccountJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            type = "service_account",
            project_id = fbProjectId ?? "",
            private_key = fbPrivateKey.Replace("\\n", "\n"),
            client_email = fbClientEmail,
            token_uri = "https://oauth2.googleapis.com/token"
        });
        FirebaseAdmin.FirebaseApp.Create(new FirebaseAdmin.AppOptions
        {
            Credential = Google.Apis.Auth.OAuth2.GoogleCredential.FromJson(serviceAccountJson)
        });
        Log.Information("Firebase Admin SDK initialized");
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Failed to initialize Firebase Admin SDK — user deletion from Firebase will not work");
    }
}
else
{
    Log.Warning("FIREBASE_ADMIN_CLIENT_EMAIL/FIREBASE_ADMIN_PRIVATE_KEY not set — Firebase Admin SDK not initialized. User deletion from Firebase will not work.");
}

// --- AutoMapper ---
builder.Services.AddAutoMapper(typeof(ZipStation.Mapping.CompanyMappingProfile).Assembly);

// --- Response caching ---
builder.Services.AddResponseCaching();

// --- Dependency Injection ---
DependencyInjection.SetupAllDependencyInjection(builder);

var app = builder.Build();

// --- Middleware pipeline ---
app.UseSerilogRequestLogging();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Zip Station API v1");
    options.RoutePrefix = "swagger";
});

app.UseCors("ZipStationCors");

app.UseAuthentication();
app.UseAuthorization();

app.UseResponseCaching();

// Enable request body buffering for patch operations
app.Use(async (context, next) =>
{
    context.Request.EnableBuffering();
    await next();
});

// Ensure MongoDB indexes
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
    await MongoIndexes.EnsureIndexesAsync(db, appConfig);
}

app.MapControllers();

// Health check endpoint (no auth)
app.MapGet("/api/v1/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTimeOffset.UtcNow }));

Log.Information("Zip Station API starting on {Urls}", string.Join(", ", app.Urls));

await app.RunAsync();
