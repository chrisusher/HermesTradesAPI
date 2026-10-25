namespace Shared.Config;

public sealed class NasdaqBounceOptions
{
    public const string SectionName = "NasdaqBounce";
    public bool EntriesEnabled { get; set; }
    public string FoundryEndpoint { get; set; } = "https://market-data-foundry.cognitiveservices.azure.com/";
    public string FoundryDeployment { get; set; } = "gpt-6-luna";
    public int MaximumCandleAgeMinutes { get; set; } = 35;
    public int FoundryTimeoutSeconds { get; set; } = 45;
}
