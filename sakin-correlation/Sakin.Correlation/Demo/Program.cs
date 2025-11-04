using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sakin.Correlation.Parsers;
using Sakin.Correlation.Validation;

namespace Sakin.Correlation.Demo;

public class Program
{
    public static async Task Main(string[] args)
    {
        // Setup dependency injection
        var services = new ServiceCollection();
        
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        services.AddSingleton<IRuleValidator, RuleValidator>();
        services.AddSingleton<IRuleParser, RuleParser>();

        var serviceProvider = services.BuildServiceProvider();
        
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        var parser = serviceProvider.GetRequiredService<IRuleParser>();

        try
        {
            logger.LogInformation("=== Rule DSL Parser Demo ===");

            // Parse rules from the configs/rules directory
            var configsPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "configs", "rules");
            if (!Directory.Exists(configsPath))
            {
                configsPath = Path.Combine(Directory.GetCurrentDirectory(), "configs", "rules");
            }

            if (Directory.Exists(configsPath))
            {
                logger.LogInformation("Loading rules from: {ConfigsPath}", configsPath);
                
                var rules = await parser.ParseRulesFromDirectoryAsync(configsPath);
                
                logger.LogInformation("Successfully loaded {RuleCount} rules:", rules.Count);
                
                foreach (var rule in rules)
                {
                    logger.LogInformation("  - {RuleId}: {RuleName} (Severity: {Severity})", 
                        rule.Id, rule.Name, rule.Severity);
                    
                    logger.LogInformation("    Triggers: {TriggerCount}, Conditions: {ConditionCount}, Actions: {ActionCount}",
                        rule.Triggers.Count, rule.Conditions.Count, rule.Actions.Count);
                }

                // Demonstrate serialization
                if (rules.Count > 0)
                {
                    var firstRule = rules.First();
                    var serialized = parser.SerializeRule(firstRule);
                    logger.LogInformation("Serialized rule {RuleId} length: {Length} characters", 
                        firstRule.Id, serialized.Length);
                }
            }
            else
            {
                logger.LogWarning("Configs/rules directory not found at: {ConfigsPath}", configsPath);
                
                // Demonstrate parsing a simple inline rule
                var simpleRuleJson = @"{
                    ""id"": ""demo-rule"",
                    ""name"": ""Demo Rule"",
                    ""description"": ""A simple demonstration rule"",
                    ""enabled"": true,
                    ""severity"": ""medium"",
                    ""triggers"": [
                        {
                            ""type"": ""event"",
                            ""eventType"": ""demo_event""
                        }
                    ],
                    ""conditions"": [
                        {
                            ""field"": ""source"",
                            ""operator"": ""equals"",
                            ""value"": ""demo_source""
                        }
                    ],
                    ""actions"": [
                        {
                            ""type"": ""alert"",
                            ""parameters"": {
                                ""title"": ""Demo Alert"",
                                ""message"": ""This is a demo alert""
                            }
                        }
                    ]
                }";

                logger.LogInformation("Parsing inline demo rule...");
                var rule = await parser.ParseRuleAsync(simpleRuleJson);
                logger.LogInformation("Successfully parsed demo rule: {RuleId}", rule.Id);
            }

            logger.LogInformation("=== Demo completed successfully ===");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Demo failed with error");
            Environment.ExitCode = 1;
        }
    }
}