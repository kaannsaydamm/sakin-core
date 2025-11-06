using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sakin.Analytics.ClickHouseSink.Services;
using Sakin.Analytics.ClickHouseSink.Workers;
using Sakin.Common.Configuration;
using Sakin.Messaging.Kafka;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<BaselineAggregationOptions>(
    builder.Configuration.GetSection("BaselineAggregation"));

builder.Services.AddSingleton<IClickHouseService, ClickHouseService>();
builder.Services.AddKafkaConsumer(builder.Configuration);
builder.Services.AddHostedService<EventSinkWorker>();

var host = builder.Build();
await host.RunAsync();
