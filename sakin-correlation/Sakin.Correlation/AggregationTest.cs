using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sakin.Correlation.Configuration;
using Sakin.Correlation.Engine;
using Sakin.Correlation.Models;
using Sakin.Correlation.Services;
using Sakin.Common.Models;
using System.Text.Json;

namespace Sakin.Correlation.Tests;

public class AggregationTest
{
    public static async Task RunTest()
    {
        Console.WriteLine("=== Testing Redis Aggregation Functionality ===");

        // Setup DI container
        var services = new ServiceCollection();
        
        // Configuration
        services.Configure<RedisOptions>(options =>
        {
            options.ConnectionString = "localhost:6379";
            options.KeyPrefix = "sakin:test:";
            options.DefaultTTL = 3600;
        });

        services.Configure<AggregationOptions>(options =>
        {
            options.MaxWindowSize = 86400;
            options.CleanupInterval = 300;
        });

        // Logging
        services.AddLogging(builder => builder.AddConsole());

        // Services
        services.AddSingleton<Sakin.Common.Cache.IRedisClient, Sakin.Common.Cache.RedisClient>();
        services.AddSingleton<IRedisStateManager, RedisStateManager>();
        services.AddSingleton<IAggregationEvaluator, AggregationEvaluatorService>();
        services.AddSingleton<IRuleEvaluatorV2, RuleEvaluatorV2>();
        services.AddSingleton<IRuleEvaluator, RuleEvaluator>();

        var serviceProvider = services.BuildServiceProvider();

        try
        {
            // Test the aggregation evaluator
            var aggregationEvaluator = serviceProvider.GetRequiredService<IAggregationEvaluator>();
            var rule = CreateTestRule();
            var events = CreateTestEvents();

            Console.WriteLine($"Created test rule: {rule.Id} - {rule.Name}");
            Console.WriteLine($"Rule will trigger alert when {rule.Condition.Aggregation?.WindowSeconds} seconds have {rule.Condition.Value} events from same group");

            // Process events
            for (int i = 0; i < events.Count; i++)
            {
                Console.WriteLine($"\n--- Processing Event {i + 1} ---");
                var result = await aggregationEvaluator.EvaluateAggregationAsync(rule, events[i]);
                Console.WriteLine($"Event {i + 1}: Alert triggered = {result}");
                
                if (result)
                {
                    Console.WriteLine($"🚨 ALERT! Threshold reached on event {i + 1}");
                    break;
                }
            }

            Console.WriteLine("\n=== Test Complete ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Test failed: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    private static CorrelationRuleV2 CreateTestRule()
    {
        return new CorrelationRuleV2
        {
            Id = "test-bruteforce-01",
            Name = "Test Brute Force Detection",
            Description = "Test rule for aggregation functionality",
            Enabled = true,
            Trigger = new RuleTrigger
            {
                SourceTypes = new List<string> { "test" },
                Match = new Dictionary<string, object> { ["event_type"] = "login_failure" }
            },
            Condition = new ConditionWithAggregation
            {
                Aggregation = new AggregationCondition
                {
                    Function = "count",
                    Field = "username",
                    GroupBy = "source_ip",
                    WindowSeconds = 300
                },
                Operator = "gte",
                Value = 5
            },
            Severity = "high"
        };
    }

    private static List<EventEnvelope> CreateTestEvents()
    {
        var events = new List<EventEnvelope>();
        var baseTime = DateTime.UtcNow;

        for (int i = 0; i < 7; i++) // 7 events, threshold is 5
        {
            events.Add(new EventEnvelope
            {
                EventId = Guid.NewGuid(),
                Normalized = new NormalizedEvent
                {
                    Timestamp = baseTime.AddSeconds(i * 10), // 10 seconds apart
                    EventType = EventType.AuthenticationAttempt,
                    SourceIp = "192.168.1.100", // Same IP for grouping
                    Metadata = new Dictionary<string, object>
                    {
                        ["username"] = "testuser",
                        ["event_code"] = "4625"
                    }
                }
            });
        }

        return events;
    }
}