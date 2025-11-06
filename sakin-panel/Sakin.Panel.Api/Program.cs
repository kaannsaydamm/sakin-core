using Npgsql;
using Sakin.Common.Configuration;
using Sakin.Common.DependencyInjection;
using Sakin.Common.Logging;
using Sakin.Correlation.Persistence.DependencyInjection;
using Sakin.Correlation.Services;
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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.UseCors();
app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();
app.MapPrometheusScrapingEndpoint();

app.Run();
