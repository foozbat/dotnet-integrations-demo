namespace IntegrationsDemo;

public record HubspotWebhookEvent(
    string ExternalContactId,
    long HubspotContactId
);
