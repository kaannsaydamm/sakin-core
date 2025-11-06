namespace Sakin.Correlation.Configuration;

public class RulesOptions
{
    public const string SectionName = "Rules";

    public string RulesPath { get; set; } = "./configs/rules";
    public bool ReloadOnChange { get; set; } = false;
    public int DebounceMilliseconds { get; set; } = 300;
}