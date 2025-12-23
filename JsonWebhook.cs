using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

/// <summary>
/// Sends JSON data to a webhook URL via HTTP POST.
/// </summary>
class JsonWebhook
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

    /// <summary>
    /// Sends the provided data as JSON to the webhook URL.
    /// </summary>
    /// <param name="data">The object to serialize and send as JSON.</param>
    /// <returns>True if the request was successful (2xx status code), false otherwise.</returns>
    public async Task<bool> SendAsync(object data)
    {
        try
        {
            Logger?.LogInformation("Sending webhook to {WebhookUrl}. CorrelationId: {CorrelationId}", this.WebhookUrl, this.CorrelationId);

            using (var client = new HttpClient())
            {
                client.Timeout = this.Timeout;
                
                if (!string.IsNullOrEmpty(this.CorrelationId))
                {
                    client.DefaultRequestHeaders.Add("X-Correlation-ID", this.CorrelationId);
                }

                // DEMO ONLY!
                // sleep to simulate slow response
                await Task.Delay(5000);
                
                var json = JsonSerializer.Serialize(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(this.WebhookUrl, content);
                
                if (response.IsSuccessStatusCode)
                {
                    Logger?.LogInformation("Webhook sent successfully. CorrelationId: {CorrelationId}", this.CorrelationId);
                }
                else
                {
                    Logger?.LogWarning("Webhook failed with status {StatusCode}. CorrelationId: {CorrelationId}", response.StatusCode, this.CorrelationId);
                }
                
                return response.IsSuccessStatusCode;
            }
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Error sending webhook. CorrelationId: {CorrelationId}", this.CorrelationId);
            return false;
        }
    }
}