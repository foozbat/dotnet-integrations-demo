using System.Text.Json.Serialization;

namespace IntegrationsDemo;

public record HubspotContactRecord(
    [property: JsonPropertyName("external_contact_id")] string ExternalContactId,
    [property: JsonPropertyName("hubspot_contact_id")] string HubspotContactId
);
