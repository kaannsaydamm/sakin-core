using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sakin.Analytics.BaselineWorker.Services;
using Sakin.Analytics.BaselineWorker.Workers;
using Sakin.Common.Cache;
using Sakin.Common.Configuration;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<BaselineAggregationOptions>(
    builder.Configuration.GetSection("BaselineAggregation"));
builder.Services.Configure<RedisOptions>(
    builder.Configuration.GetSection("Redis"));

builder.Services.AddSingleton<IRedisClient, RedisClient>();
builder.Services.AddSingleton<IBaselineCalculatorService, BaselineCalculatorService>();
builder.Services.AddHostedService<BaselineCalculationWorker>();

var host = builder.Build();
await host.RunAsync();
