using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sakin.Correlation.Configuration;
using Sakin.Correlation.Parsers;
using Sakin.Correlation.Validation;

namespace TestRuleLoading;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Build configuration
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        // Setup DI
        var services = new ServiceCollection();

        services.Configure<RulesOptions>(configuration.GetSection(RulesOptions.SectionName));
        services.AddSingleton<IRuleValidator, RuleValidator>();
        services.AddSingleton<IRuleParser, RuleParser>();
        services.AddLogging(builder => builder.AddConsole());

        var serviceProvider = services.BuildServiceProvider();

        // Test rule loading
        var ruleParser = serviceProvider.GetRequiredService<IRuleParser>();
        var rulesOptions = serviceProvider.GetRequiredService<IOptions<RulesOptions>>().Value;
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            Console.WriteLine($"Loading rules from: {rulesOptions.RulesPath}");
            
            if (!Directory.Exists(rulesOptions.RulesPath))
            {
                Console.WriteLine($"ERROR: Rules directory not found: {rulesOptions.RulesPath}");
                return 1;
            }

            var rules = await ruleParser.ParseRulesFromDirectoryAsync(rulesOptions.RulesPath);
            
            Console.WriteLine($"Successfully loaded {rules.Count} rules:");
            
            foreach (var rule in rules)
            {
                Console.WriteLine($"  - {rule.Id}: {rule.Name} (Enabled: {rule.Enabled})");
            }
            
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return 1;
        }
    }
}