using System.Text.Json.Serialization;

namespace IntegrationsDemo;

public class LogicAppError
{
    public required string WorkflowRunId { get; set; }
    public required string WorkflowName { get; set; }
    public DateTime TriggerTime { get; set; }
    public Dictionary<string, object>? LeadData { get; set; }
    public List<ActionResult>? ErrorDetails { get; set; }
    public DateTime Timestamp { get; set; }
}

public class ActionResult
{
    public required string Name { get; set; }
    public required string Status { get; set; }

    [JsonPropertyName("error")]
    public ErrorInfo? Error { get; set; }

    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}

public class ErrorInfo
{
    public string? Code { get; set; }
    public string? Message { get; set; }
}