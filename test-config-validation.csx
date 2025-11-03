#!/usr/bin/env dotnet script

#r "nuget: Microsoft.Extensions.Configuration, 9.0.0"
#r "nuget: Microsoft.Extensions.Configuration.Json, 9.0.0"
#r "nuget: Microsoft.Extensions.Configuration.EnvironmentVariables, 9.0.0"

using Microsoft.Extensions.Configuration;
using System.Text.Json;

string[] configFiles = {
    "sakin-core/services/network-sensor/appsettings.json",
    "sakin-core/services/network-sensor/appsettings.Development.json",
    "sakin-ingest/appsettings.json",
    "sakin-ingest/appsettings.Development.json",
    "sakin-correlation/appsettings.json",
    "sakin-correlation/appsettings.Development.json"
};

Console.WriteLine("=== Configuration Validation Test ===\n");

foreach (var configFile in configFiles)
{
    string fullPath = Path.Combine("/home/engine/project", configFile);
    
    if (!File.Exists(fullPath))
    {
        Console.WriteLine($"❌ {configFile} - File not found");
        continue;
    }

    try
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Path.GetDirectoryName(fullPath))
            .AddJsonFile(Path.GetFileName(fullPath), optional: false, reloadOnChange: false);
        
        var configuration = builder.Build();
        
        // Try to read Database section
        var dbHost = configuration["Database:Host"];
        var dbUser = configuration["Database:Username"];
        var dbName = configuration["Database:Database"];
        var dbPort = configuration["Database:Port"];
        
        Console.WriteLine($"✓ {configFile}");
        Console.WriteLine($"  Database: {dbName}@{dbHost}:{dbPort} (user: {dbUser})");
        
        // Check for TODO markers
        var dbPassword = configuration["Database:Password"];
        if (dbPassword != null && dbPassword.Contains("TODO"))
        {
            Console.WriteLine($"  Password: ⚠️  TODO placeholder (correct)");
        }
        else if (string.IsNullOrEmpty(dbPassword))
        {
            Console.WriteLine($"  Password: ⚠️  Empty");
        }
        else
        {
            Console.WriteLine($"  Password: Set");
        }
        
        Console.WriteLine();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ {configFile} - Error: {ex.Message}\n");
    }
}

Console.WriteLine("=== Test Complete ===");
