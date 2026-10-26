using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using SecureNotepad.Api.Configuration;
using SecureNotepad.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure MongoDB Atlas Settings
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection(MongoDbSettings.SectionName));

// 2. Register INoteRepository based on configuration
var mongoSettings = builder.Configuration
    .GetSection(MongoDbSettings.SectionName)
    .Get<MongoDbSettings>();

var connectionString = !string.IsNullOrWhiteSpace(mongoSettings?.ConnectionString)
    ? mongoSettings.ConnectionString
    : (Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING") ?? string.Empty);

if (!string.IsNullOrWhiteSpace(connectionString) &&
    !connectionString.Equals("InMemory", StringComparison.OrdinalIgnoreCase))
{
    // Ensure connection string is supplied to MongoDbSettings options
    builder.Services.PostConfigure<MongoDbSettings>(s =>
    {
        if (string.IsNullOrWhiteSpace(s.ConnectionString))
        {
            s.ConnectionString = connectionString;
        }
    });

    // Live MongoDB Atlas repository
    builder.Services.AddSingleton<INoteRepository, MongoNoteRepository>();
}
else
{
    // In-Memory repository for automated testing / offline fallback
    builder.Services.AddSingleton<INoteRepository, InMemoryNoteRepository>();
}

// 3. Add Controllers
builder.Services.AddControllers();

// 4. Configure CORS for Angular Frontend (Localhost, Vercel, & Custom Origins)
var corsPolicyName = "AllowFrontend";
var allowedOriginsEnv = Environment.GetEnvironmentVariable("ALLOWED_ORIGINS") ?? builder.Configuration["Cors:AllowedOrigins"];
var extraOrigins = !string.IsNullOrWhiteSpace(allowedOriginsEnv)
    ? allowedOriginsEnv.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    : Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(corsPolicyName, policy =>
    {
        policy.SetIsOriginAllowed(origin =>
            {
                if (string.IsNullOrWhiteSpace(origin)) return false;
                if (extraOrigins.Length > 0 && extraOrigins.Any(o =>
                    string.Equals(o, origin, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(o, "*", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }

                try
                {
                    var uri = new Uri(origin);
                    return uri.Host == "localhost" ||
                           uri.Host == "127.0.0.1" ||
                           uri.Host.EndsWith("vercel.app", StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    return false;
                }
            })
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// 5. Add Built-in Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });
});

var app = builder.Build();

// Configure Middleware Pipeline
app.UseCors(corsPolicyName);
app.UseRateLimiter();

app.UseRouting();

app.MapControllers();

app.Run();

// Partial class definition to allow integration tests using WebApplicationFactory
public partial class Program { }
