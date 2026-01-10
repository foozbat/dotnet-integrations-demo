using System.Text.Json.Serialization;

namespace IntegrationsDemo;

public class LogicAppError
{
    [JsonPropertyName("workflowRunId")]
    public required string WorkflowRunId { get; set; }

    [JsonPropertyName("workflowName")]
    public required string WorkflowName { get; set; }

    [JsonPropertyName("triggerTime")]
    public DateTime TriggerTime { get; set; }

    [JsonPropertyName("triggerData")]
    public Dictionary<string, object>? TriggerData { get; set; }

    [JsonPropertyName("errorDetails")]
    public List<WorkflowAction>? ErrorDetails { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }
}

public class WorkflowAction
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("status")]
    public required string Status { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("startTime")]
    public DateTime? StartTime { get; set; }

    [JsonPropertyName("endTime")]
    public DateTime? EndTime { get; set; }
}