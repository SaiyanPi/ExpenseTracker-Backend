using ExpenseTracker.Persistence.DI;
using ExpenseTracker.Application.DI;
using ExpenseTracker.Infrastructure.DI;
using ExpenseTracker.Infrastructure.HealthCheck;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using ExpenseTracker.Persistence;
using ExpenseTracker.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using ExpenseTracker.API.Middleware;
using FluentValidation.AspNetCore;
using FluentValidation;
using ExpenseTracker.Application.Common.Authorization.Permissions;
using ExpenseTracker.Application.Common.Authorization;
using QuestPDF.Infrastructure;
using ExpenseTracker.Infrastructure.Services.Notification;
using Serilog;
using OpenTelemetry.Metrics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Security.Claims;
using ExpenseTracker.Application.Common.Security;
using ExpenseTracker.Application.Common.Interfaces.Services;
using ExpenseTracker.Domain.Entities;
using ExpenseTracker.Domain.SharedKernel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using ExpenseTracker.API.Swagger;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using ExpenseTracker.API.Security;
using System.Text.Json.Serialization;
using ExpenseTracker.Application.Common.Observability.Metrics.Cache;
using ExpenseTracker.API.Models;
using System.Diagnostics;


// config Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()             

    // Reduce framework noise
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)

    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithProcessId()
    .Enrich.WithThreadId()
    .WriteTo.Console(
        outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] " +
            "[CorrId={CorrelationId}] " +
            "[UserId={UserId}] " +
            "{Message:lj}{NewLine}{Exception}"
    )
    .CreateLogger();
    
var builder = WebApplication.CreateBuilder(args);

// config Serilog
builder.Host.UseSerilog();

// qualifying for community license (required for pdf export)
QuestPDF.Settings.License = LicenseType.Community;


// Register services, repositories, etc.
builder.Services.AddApplicationServices();
builder.Services.AddPersistenceServices(builder.Configuration);
builder.Services.AddInfrastructureServices(builder.Configuration);

// register API layer Mapping Profiles
builder.Services.AddAutoMapper(typeof(Program));

// Custom Authorization failure handler to log 403 forbidden events
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, AuthorizationFailureResultHandler>();

// Add in-memory caching
builder.Services.AddMemoryCache();

// Add controllers / minimal APIs
builder.Services.AddControllers()
    .AddJsonOptions(options => 
    {   
        // Serialize enums as strings instead of numeric values.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
    // .AddXmlSerializerFormatters();

// Modern FluentValidation registration
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddFluentValidationClientsideAdapters();
// Register all validators from Application assembly
// builder.Services.AddValidatorsFromAssemblyContaining<CreateExpenseDtoValidator>();
builder.Services.AddValidatorsFromAssembly(typeof(ExpenseTracker.Application.AssemblyReference).Assembly);


builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddEndpointsApiExplorer();

// Configure JWT authentication 
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var jwtSettings = builder.Configuration.GetSection("JwtConfig");
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSettings["Secret"] ?? throw new InvalidOperationException("JWT Secret is missing from configuration."))
        )
    };

    options.Events = new JwtBearerEvents
    {
        // KEY piece for SignalR: SignalR sends the token via query string, not the authorization header
        OnMessageReceived = context =>
        {
            // If the request is for SignalR and token is in the query string
            var accessToken = context.Request.Query["access_token"];

            // If the request is for the hub...
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) &&
                path.StartsWithSegments("/hubs/notifications"))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        },

        // For 403 access denied logging, we already handled via authorization failure result handler
        // Now we handle 401 Unauthorized logging
        // OnChallenge is triggered any time authentication fails, before hitting the endpoint.
        // This captures missing token, invalid token, expired token.
        OnChallenge = async context =>
        {
            var httpContext = context.HttpContext;

            // Extract claims if any (may be null)
            var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userEmail = httpContext.User.FindFirst(ClaimTypes.Email)?.Value;

            var endpoint = SecurityEventContext.GetEndpoint(httpContext);
            var ip = SecurityEventContext.GetIp(httpContext);
            var userAgent = SecurityEventContext.GetUserAgent(httpContext);
            var correlationId = SecurityEventContext.GetCorrelationId(httpContext);

            // Fire-and-forget logging
            try
            {
                using var scope = httpContext.RequestServices.CreateScope();
                var securityLogger = scope.ServiceProvider.GetRequiredService<ISecurityEventLoggerService>();

                await securityLogger.LogSecurityEventAsync(new SecurityEventLog
                {
                    EventType = SecurityEventTypes.UnauthorizedAccess,
                    UserId = userId,
                    UserEmail = userEmail,
                    Outcome = SecurityEventOutcome.Failed,
                    Endpoint = endpoint,
                    IpAddress = ip,
                    UserAgent = userAgent,
                    CorrelationId = correlationId,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch
            {
                // swallow exceptions — must not block the request
            }

            // IMPORTANT: continue the normal 401 response
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.HandleResponse(); // prevents default challenge handling
        }
    };

});


// add authorization policies
builder.Services.AddAuthorization(options =>
{
    // Expense policies
    options.AddPolicy(ExpensePermission.View, policy =>
        policy.RequireClaim(AppClaimTypes.Permission, ExpensePermission.View));
    options.AddPolicy(ExpensePermission.Create, policy =>
        policy.RequireClaim(AppClaimTypes.Permission, ExpensePermission.Create));
    options.AddPolicy(ExpensePermission.Update, policy =>
        policy.RequireClaim(AppClaimTypes.Permission, ExpensePermission.Update));
    options.AddPolicy(ExpensePermission.Delete, policy =>
        policy.RequireClaim(AppClaimTypes.Permission, ExpensePermission.Delete));
    options.AddPolicy(ExpensePermission.ViewAll, policy =>
        policy.RequireClaim(AppClaimTypes.Permission, ExpensePermission.ViewAll));
    
    // Expense filter policy(access to both admin and regular user)
    options.AddPolicy("Expense.Filter", policy =>
        policy.RequireAssertion(context => 
        context.User.HasClaim(AppClaimTypes.Permission, ExpensePermission.View) ||
        context.User.HasClaim(AppClaimTypes.Permission, ExpensePermission.ViewAll)
        ));

    // Category policies
    options.AddPolicy(CategoryPermission.View, policy =>
        policy.RequireClaim(AppClaimTypes.Permission, CategoryPermission.View));
    options.AddPolicy(CategoryPermission.Create, policy =>
        policy.RequireClaim(AppClaimTypes.Permission, CategoryPermission.Create));
    options.AddPolicy(CategoryPermission.Update, policy =>
        policy.RequireClaim(AppClaimTypes.Permission, CategoryPermission.Update));
    options.AddPolicy(CategoryPermission.Delete, policy =>
        policy.RequireClaim(AppClaimTypes.Permission, CategoryPermission.Delete));
    options.AddPolicy(CategoryPermission.ViewAll, policy =>
        policy.RequireClaim(AppClaimTypes.Permission, CategoryPermission.ViewAll));

    // Budget policies
    options.AddPolicy(BudgetPermission.View, policy =>
        policy.RequireClaim(AppClaimTypes.Permission, BudgetPermission.View));
    options.AddPolicy(BudgetPermission.Create, policy =>
        policy.RequireClaim(AppClaimTypes.Permission, BudgetPermission.Create));
    options.AddPolicy(BudgetPermission.Update, policy =>
        policy.RequireClaim(AppClaimTypes.Permission, BudgetPermission.Update));
    options.AddPolicy(BudgetPermission.Delete, policy =>
        policy.RequireClaim(AppClaimTypes.Permission, BudgetPermission.Delete));
    options.AddPolicy(BudgetPermission.ViewAll, policy =>
        policy.RequireClaim(AppClaimTypes.Permission, BudgetPermission.ViewAll));
    
    // Dashboard policy
    options.AddPolicy(DashboardPermission.View, policy =>
        policy.RequireClaim(AppClaimTypes.Permission, DashboardPermission.View));
    
    // UserManagement policy
    options.AddPolicy(UserManagementPermission.All, policy =>
        policy.RequireClaim(AppClaimTypes.Permission, UserManagementPermission.All));
    
    // AuditLog policy
    options.AddPolicy(AuditLogPermission.View, policy =>
        policy.RequireClaim(AppClaimTypes.Permission, AuditLogPermission.View)); 
});

// for consistent Http response
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var response = new ErrorResponse
        {
            StatusCode = StatusCodes.Status400BadRequest,
            Error = "ValidationException",
            Message = "Validation failed.",
            Details = context.ModelState
                .Where(x => x.Value!.Errors.Count > 0)
                .ToDictionary(
                    x => x.Key,
                    x => x.Value!.Errors
                        .Select(e => e.ErrorMessage)
                        .ToArray()
                ),
            TraceId = Activity.Current?.TraceId.ToString() ?? string.Empty,
            CorrelationId = context.HttpContext.Items[CorrelationIdMiddleware.HeaderName]?.ToString()
            ?? context.HttpContext.TraceIdentifier
        };

        return new BadRequestObjectResult(response);
    };
});

// Add SignalR -------------------
builder.Services.AddSignalR()
    .AddHubOptions<NotificationHub>(options =>
    {
        options.EnableDetailedErrors = true;
    });

// CORS config ------------------
builder.Services.AddCors(options =>
{
    // options.AddPolicy("FrontendPolicy", policy =>
    // {
    //     policy
    //         .AllowAnyHeader()
    //         .AllowAnyMethod()
    //         .AllowCredentials()
    //         .WithOrigins("http://localhost:4200"); // frontend URL
    // });

    // This is for testing JS AJAX. Totally not related to ExpenseTracker
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .WithOrigins(
                "http://127.0.0.1:5500",
                "http://localhost:4200"
            ); // frontend URL
    });
});

// OpenTelemetry config ------------------
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation() // tracks HTTP requests
            .AddMeter("ExpenseTracker.Application");    // custom business metrics
            // // Optional console exporter for dev/debug
            // #if DEBUG
            //         metrics.AddConsoleExporter();
            // #endif

        // Prometheus exporter for dashboards
        metrics.AddPrometheusExporter();  // exposes /metrics endpoint
    });

// Register custom cache metrics
CacheMetricsSetup.RegisterCacheMetrics();

// Add HealthChecks -----------------
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ExpenseTrackerDbContext>(
        name: "database",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "ready" }) //readiness tag
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" }) // simple liveness check
    .AddCheck<SmtpHealthCheck>("smtp", failureStatus: HealthStatus.Degraded, tags: new[] { "ready" }); // smtp email server check

// config API versioning -----------------
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);   // default version if client does NOT specify one
    options.AssumeDefaultVersionWhenUnspecified = true; // If client does not specify version, use default
    options.ReportApiVersions = true;     // Adds headers like: api-supported-versions: 1.0
    options.ApiVersionReader = new UrlSegmentApiVersionReader();    // Tell ASP.NET how to read the version
});

// config API versioning explorer (for Swagger) -----------------
builder.Services.AddVersionedApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";  // e.g., v1, v2
    options.SubstituteApiVersionInUrl = true;  // replaces {version} in route
});

// Register the Swagger options configurator -----------------
// Swagger
builder.Services.AddSwaggerGen();
builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();

// config rate limiting -----------------
builder.Services.AddRateLimiter(options =>
{
    // logging for rejected requests
    options.OnRejected = async (context, token) =>
    {
        var httpContext = context.HttpContext;

        // Get correlationId from your middleware (source of truth)
        var correlationId =
            httpContext.Items[CorrelationIdMiddleware.HeaderName]?.ToString()
            ?? httpContext.TraceIdentifier;

        // Add to response header
        // httpContext.Response.Headers["X-Correlation-ID"] = correlationId;

        // log the rejection
        var logger = httpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("RateLimiting");

        logger.LogWarning(
            "Request was rate limited. Path={Path}, CorrelationId={CorrelationId}",
            httpContext.Request.Path,
            correlationId);

        // Let framework produce default 429 body
        await Task.CompletedTask;
    };

    // Global default policy
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var key =
            context.User.Identity?.Name ??
            context.Connection.RemoteIpAddress?.ToString() ??
            "anonymous";

        return RateLimitPartition.GetTokenBucketLimiter(
            key,
            _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 100,
                TokensPerPeriod = 100,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });


    // Auth endpoints 
    options.AddPolicy("Auth", context =>
    {
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ipAddress,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });
    });

    // Heavy endpoints (dashboard/export)
    options.AddPolicy("Heavy", context =>
    {
        var userId = context.User.Identity?.Name ?? "anonymous";

        return RateLimitPartition.GetTokenBucketLimiter(
            partitionKey: userId,
            factory: _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 10,
                TokensPerPeriod = 10,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });

});


var app = builder.Build();


// Run the seeder
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var context = services.GetRequiredService<ExpenseTrackerDbContext>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

    await ExpenseTrackerDbContextSeed.SeedAsync(context, userManager, roleManager);
}


// swagger config
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI(options =>
    {
        var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();

        foreach (var description in provider.ApiVersionDescriptions)
        {
            options.SwaggerEndpoint(
                $"/swagger/{description.GroupName}/swagger.json",
                description.GroupName.ToUpperInvariant()
            );
        }

        options.DisplayRequestDuration();
    });
}
// Register exception middleware FIRST in pipeline
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseMiddleware<CorrelationIdMiddleware>();

// app.UseMiddleware<RequestLogContextMiddleware>();

// app.UseMiddleware<RequestTimingMiddleware>();

// app.UseRateLimiter(); // -> this will fail for controllers using api versioning

app.UseHttpsRedirection();

app.UseStaticFiles();   // for profile image uploads, etc.

app.UseCors("FrontendPolicy");

app.UseAuthentication();  

// app.UseMiddleware<AuthorizationFailureLoggingMiddleware>();

app.UseAuthorization();

app.UseMiddleware<RequestLogContextMiddleware>();   // after auth: UserId available for the logs

app.UseMiddleware<RequestTimingMiddleware>();

// Rate limiting AFTER routing and endpoint metadata is available
app.UseRateLimiter();

app.MapControllers();

app.MapHub<NotificationHub>(NotificationHub.HubUrl);    // map the SignalRhub to a URL

app.UseOpenTelemetryPrometheusScrapingEndpoint(); // default: /metrics

// Map HealthCheck endpoints
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"), // liveness checks only
    ResponseWriter = HealthChecks.UI.Client.UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"), // readiness checks only
    ResponseWriter = HealthChecks.UI.Client.UIResponseWriter.WriteHealthCheckUIResponse
});


app.Run();
