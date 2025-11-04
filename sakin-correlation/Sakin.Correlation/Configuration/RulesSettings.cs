namespace Sakin.Correlation.Configuration;

public class RulesSettings
{
    public const string SectionName = "Rules";

    public string RulesPath { get; set; } = "/configs/rules/";
    public bool ReloadOnChange { get; set; } = true;
}
