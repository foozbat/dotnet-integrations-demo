# Azure Logic App ARM Template - DevOps to HubSpot

This directory contains an Azure Resource Manager (ARM) template exported from an existing Logic App. Use this to deploy a bi-directional sync workflow between Azure DevOps and HubSpot to your own Azure environment.

## What This Logic App Does

1. **Monitors Azure DevOps work items** for updates (polling every 3 minutes)
2. **Parses work item tags** to find associated HubSpot ticket IDs
3. **Detects work item state changes** (e.g., "Done", "Closed")
4. **Posts notes to HubSpot tickets** when work items are resolved
5. **Associates notes with tickets** using HubSpot's CRM associations

## Prerequisites

- Azure subscription with permissions to create Logic Apps and API connections
- Existing resource group
- Azure DevOps organization and project
- Azure Key Vault with the following secret:
  - `hubspot-api-access-token` - Your HubSpot private app access token
- Azure DevOps API connection configured

## Key Features

- **Tag-based linking**: Uses `HubSpotTicket-{id}` tags in DevOps to link work items to HubSpot tickets
- **Automatic status sync**: When DevOps work item is marked "Done", adds note to HubSpot ticket
- **Correlation tracking**: Uses X-Correlation-ID headers for request tracing
- **Secure token handling**: HubSpot access token retrieved from Key Vault

## Deployment

### Update Parameters

Edit `parameters.json`:

```json
{
  "workflows_integrations_demo_la_devops_to_hubspot_name": {
    "value": "YOUR-LOGIC-APP-NAME"
  },
  "connections_visualstudioteamservices_externalid": {
    "value": "/subscriptions/YOUR-SUBSCRIPTION-ID/resourceGroups/YOUR-RESOURCE-GROUP/providers/Microsoft.Web/connections/visualstudioteamservices"
  },
  "connections_keyvault_externalid": {
    "value": "/subscriptions/YOUR-SUBSCRIPTION-ID/resourceGroups/YOUR-RESOURCE-GROUP/providers/Microsoft.Web/connections/keyvault"
  }
}
```

### Deploy Template

Deploy via Azure Portal using **Template deployment (deploy using custom template)** or use Azure CLI:

```bash
az deployment group create \
  --resource-group your-resource-group \
  --template-file template.json \
  --parameters parameters.json
```

### Configure API Connections

1. **Azure DevOps Connection:**
   - Navigate to API Connections in Azure Portal
   - Authorize the `visualstudioteamservices` connection
   - Grant access to your DevOps organization and project

2. **Key Vault Connection:**
   - Grant the Logic App's managed identity access to Key Vault
   - **Secret permissions**: Get, List
   - **Principal**: Your Logic App name

### Configure DevOps Trigger

Update the Logic App trigger settings:
- **Project**: Your Azure DevOps project name
- **Account**: Your Azure DevOps organization name
- **Team**: Your team name (optional)
- **Polling interval**: Default is 3 minutes

## Key Vault Secrets Reference

| Secret Name | Description | Example |
|------------|-------------|---------|
| `hubspot-api-access-token` | HubSpot private app token | `pat-na1-...` |

**Required HubSpot scopes:**
- `crm.objects.tickets.read`
- `crm.objects.tickets.write`
- `crm.schemas.custom.read`

## Workflow Logic

### Tag Format
Work items must be tagged with `HubSpotTicket-{ticketId}` format:
```
Example: HubSpotTicket-12345678
```

### Status Mapping
| DevOps State | Action |
|--------------|--------|
| Done | Adds note to HubSpot ticket: "Issue resolved by developers." |
| Other states | No action taken |

### Note Structure
```json
{
  "properties": {
    "hs_timestamp": "2026-01-04T12:00:00Z",
    "hs_note_body": "Issue resolved by developers."
  },
  "associations": [
    {
      "to": { "id": "12345678" },
      "types": [
        {
          "associationCategory": "HUBSPOT_DEFINED",
          "associationTypeId": 18  // Note-to-Ticket association
        }
      ]
    }
  ]
}
```

## Testing

1. **Create a HubSpot ticket** (note the ticket ID)
2. **Create or update an Azure DevOps work item:**
   - Add tag: `HubSpotTicket-{your-ticket-id}`
3. **Change work item state to "Done"**
4. **Verify in Logic App run history:**
   - Trigger should fire within 3 minutes
   - Tag parsing extracts ticket ID
   - HTTP request to HubSpot succeeds
5. **Check HubSpot ticket** for new note

## Troubleshooting

**Trigger not firing:**
- Verify DevOps connection is authorized
- Check project/organization names match exactly
- Ensure work items are being updated

**401 Unauthorized errors:**
- Verify managed identity has Key Vault access policy
- Check `hubspot-api-access-token` secret exists and is valid
- Confirm HubSpot private app scopes include ticket write permissions

**Tag not found:**
- Ensure work item has tag in format `HubSpotTicket-{id}`
- Check tag parsing logic handles semicolon-separated tags

**Note not appearing in HubSpot:**
- Verify ticket ID is valid and ticket exists
- Check association type ID (18 = Note-to-Ticket)
- Review HubSpot API response in Logic App run history

**HTTP 400 errors:**
- Verify JSON payload structure matches HubSpot API expectations
- Check timestamp format is ISO 8601

## Architecture Notes

This Logic App complements the `hubspot-to-devops` workflow to create a bidirectional sync:

```
HubSpot Ticket Created → hubspot-to-devops → DevOps Work Item Created
                                              (with HubSpotTicket-{id} tag)
                                                        ↓
DevOps Work Item Resolved → devops-to-hubspot → HubSpot Ticket Note Added
```

## Customization

### Add More Status Mappings

Edit the `Switch` action to handle additional states:

```json
{
  "cases": {
    "Done": { ... },
    "In Progress": {
      "case": "Active",
      "actions": {
        // Add note: "Work in progress..."
      }
    }
  }
}
```

### Customize Note Content

Modify the `Compose_JSON` action to include dynamic content:

```json
{
  "hs_note_body": "Work item @{triggerBody()?['id']} resolved by @{triggerBody()?['fields']?['System_AssignedTo']}"
}
```

### Change Polling Frequency

Update trigger `recurrence` settings (minimum: 1 minute):

```json
{
  "recurrence": {
    "interval": 5,
    "frequency": "Minute"
  }
}
```
