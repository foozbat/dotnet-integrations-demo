namespace IntegrationsDemo;

public record HubspotContactRecord(
#pragma warning disable IDE1006 // Naming Styles
    string hs_object_id,
    string external_contact_id
#pragma warning restore IDE1006 // Naming Styles
);
