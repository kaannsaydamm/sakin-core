using Sakin.Correlation.Persistence.DependencyInjection;
using Sakin.Correlation.Services;
using Sakin.Panel.Api.Services;
using Sakin.Common.Cache;
using Sakin.Common.DependencyInjection;
using Sakin.Common.Configuration;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

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

// Add database configuration
builder.Services.AddScoped<NpgsqlConnection>(sp => 
    new NpgsqlConnection(builder.Configuration.GetConnectionString("Postgres")));

// Register Sakin services
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

app.UseCors();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
