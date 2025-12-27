using System.Text;
using System.Text.Json;

/// <summary>
/// Sends JSON data to a webhook URL via HTTP POST.
/// </summary>
internal sealed partial class JsonWebhook
{
    /// <summary>
    /// Gets or sets the target webhook URL.
    /// </summary>
    public required string WebhookUrl { get; set; }

    /// <summary>
    /// Gets or sets the timeout for the HTTP request. Defaults to 5 seconds.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets an optional correlation ID that will be sent as an X-Correlation-ID header.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets an optional logger for tracking webhook activity.
    /// </summary>
    public ILogger? Logger { get; set; }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sending webhook to {webhookUrl}. CorrelationId: {correlationId}")]
    private static partial void LogSendingWebhook(ILogger logger, string? webhookUrl, string? correlationId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Webhook sent successfully. CorrelationId: {correlationId}")]
    private static partial void LogWebhookSuccess(ILogger logger, string? correlationId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Webhook failed with status {statusCode}. CorrelationId: {correlationId}")]
    private static partial void LogWebhookFailure(ILogger logger, System.Net.HttpStatusCode statusCode, string? correlationId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error sending webhook. CorrelationId: {correlationId}")]
    private static partial void LogWebhookError(ILogger logger, Exception ex, string? correlationId);

    private void Log(Action<ILogger> logAction)
    {
        if (Logger is not null)
        {
            logAction(Logger);
        }
    }

    /// <summary>
    /// Sends the provided data as JSON to the webhook URL.
    /// </summary>
    /// <param name="data">The object to serialize and send as JSON.</param>
    /// <returns>True if the request was successful (2xx status code), false otherwise.</returns>
    public async Task<bool> SendAsync(object data)
    {
        try
        {
            Log(logger => LogSendingWebhook(logger, WebhookUrl, CorrelationId));

            using HttpClient client = new();
            client.Timeout = Timeout;

            if (!string.IsNullOrEmpty(CorrelationId))
            {
                client.DefaultRequestHeaders.Add("X-Correlation-ID", CorrelationId);
            }

            // DEMO ONLY!
            // sleep to simulate slow response
            await Task.Delay(5000);

            var json = JsonSerializer.Serialize(data);
            StringContent content = new(json, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await client.PostAsync(WebhookUrl, content);

            if (response.IsSuccessStatusCode)
            {
                Log(logger => LogWebhookSuccess(logger, CorrelationId));
            }
            else
            {
                Log(logger => LogWebhookFailure(logger, response.StatusCode, CorrelationId));
            }

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Log(logger => LogWebhookError(logger, ex, CorrelationId));
            return false;
        }
    }
}