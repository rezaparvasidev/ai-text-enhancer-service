namespace TextEnhancer.Api.Services;

public class AzureOpenAIOptions
{
    public const string SectionName = "AOAI";

    public string Endpoint { get; set; } = string.Empty;
    public string Deployment { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}
