using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sakin.Correlation.Parsers;
using Sakin.Correlation.Validation;

namespace Sakin.Correlation.Demo;

public class TestProgram
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
        
        var parser = serviceProvider.GetRequiredService<IRuleParser>();

        try
        {
            // Test with a simple valid rule
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

            Console.WriteLine("Parsing simple demo rule...");
            var rule = await parser.ParseRuleAsync(simpleRuleJson);
            Console.WriteLine($"Successfully parsed rule: {rule.Id}");
            
            // Test serialization
            var serialized = parser.SerializeRule(rule);
            Console.WriteLine($"Serialized rule length: {serialized.Length}");
            
            // Test roundtrip
            var roundtrip = await parser.ParseRuleAsync(serialized);
            Console.WriteLine($"Roundtrip successful: {roundtrip.Id}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            Environment.ExitCode = 1;
        }
    }
}