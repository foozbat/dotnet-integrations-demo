using System.Text.Json.Serialization;

namespace IntegrationsDemo;

public class ZapierError
{
    [JsonPropertyName("zapId")]
    public required string ZapId { get; set; }

    [JsonPropertyName("stepId")]
    public required string StepId { get; set; }

    [JsonPropertyName("runId")]
    public required string RunId { get; set; }

    [JsonPropertyName("error")]
    public Dictionary<string, object>? Error { get; set; }
}
