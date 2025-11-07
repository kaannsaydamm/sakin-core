using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Sakin.Common.Audit;
using Sakin.Common.Configuration;
using Sakin.Common.DependencyInjection;
using Sakin.Common.Logging;
using Sakin.Common.Security;
using Sakin.Common.Security.Services;
using Sakin.Common.Validation;
using Sakin.Correlation.Persistence.DependencyInjection;
using Sakin.Correlation.Services;
using Sakin.Messaging.Abstractions;
using Sakin.Messaging.DependencyInjection;
using Sakin.Panel.Api.Middleware;
using Sakin.Panel.Api.Services;
using Serilog;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, loggerConfiguration) =>
{
    TelemetryExtensions.ConfigureSakinSerilog(
        loggerConfiguration,
        context.Configuration,
        context.HostingEnvironment.EnvironmentName);
});

builder.Services.AddSakinTelemetry(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",
                "https://localhost:3000",
                "http://localhost:5173",
                "https://localhost:5173")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<NpgsqlConnection>(_ =>
    new NpgsqlConnection(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddSakinCommon(builder.Configuration);
builder.Services.AddCorrelationPersistence(builder.Configuration);
builder.Services.Configure<AlertLifecycleOptions>(builder.Configuration.GetSection(AlertLifecycleOptions.SectionName));
builder.Services.AddScoped<IAlertDeduplicationService, AlertDeduplicationService>();
builder.Services.AddScoped<IAlertLifecycleService, AlertLifecycleService>();
builder.Services.AddScoped<IAlertService, AlertService>();
builder.Services.AddScoped<IAssetService, AssetService>();

// Security configuration
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<ApiKeyOptions>(builder.Configuration.GetSection(ApiKeyOptions.SectionName));
builder.Services.Configure<ClientCredentialsOptions>(builder.Configuration.GetSection(ClientCredentialsOptions.SectionName));
builder.Services.Configure<ValidationOptions>(builder.Configuration.GetSection(ValidationOptions.SectionName));
builder.Services.Configure<TlsOptions>(builder.Configuration.GetSection(TlsOptions.SectionName));

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IJwtService, JwtService>();
builder.Services.AddSingleton<IClientCredentialsStore, ClientCredentialsStore>();
builder.Services.AddSingleton<InputValidator>();

// Audit logging
builder.Services.Configure<AuditLoggingOptions>(builder.Configuration.GetSection(AuditLoggingOptions.SectionName));
builder.Services.AddKafkaMessaging(builder.Configuration);
builder.Services.AddScoped<IAuditService, AuditService>();

// JWT Authentication
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = jwtOptions.ValidateIssuer,
        ValidateAudience = jwtOptions.ValidateAudience,
        ValidateLifetime = jwtOptions.ValidateLifetime,
        ValidateIssuerSigningKey = jwtOptions.ValidateIssuerSigningKey,
        ValidIssuer = jwtOptions.Issuer,
        ValidAudience = jwtOptions.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
        ClockSkew = TimeSpan.FromMinutes(5)
    };
});

// Authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ReadAlerts", policy => 
        policy.RequireAssertion(context => 
            HasPermission(context.User, Permission.ReadAlerts)));
    
    options.AddPolicy("WriteAlerts", policy => 
        policy.RequireAssertion(context => 
            HasPermission(context.User, Permission.WriteAlerts)));
    
    options.AddPolicy("ReadAuditLogs", policy => 
        policy.RequireAssertion(context => 
            HasPermission(context.User, Permission.ReadAuditLogs)));
    
    options.AddPolicy("Admin", policy => 
        policy.RequireAssertion(context => 
            HasPermission(context.User, Permission.SystemAdmin)));
});

static bool HasPermission(System.Security.Claims.ClaimsPrincipal user, Permission requiredPermission)
{
    var permissionsClaim = user.FindFirst(SakinClaimTypes.Permissions)?.Value;
    if (string.IsNullOrEmpty(permissionsClaim) || !int.TryParse(permissionsClaim, out var permissions))
    {
        return false;
    }
    return ((Permission)permissions & requiredPermission) == requiredPermission;
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.UseCors();
app.UseHttpsRedirection();

app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<AuditLoggingMiddleware>();

app.MapControllers();
app.MapPrometheusScrapingEndpoint();

app.Run();
