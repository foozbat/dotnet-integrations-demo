# Azure Logic App ARM Template - HubSpot to DevOps

This directory contains an Azure Resource Manager (ARM) template exported from an existing Logic App. Use this to deploy a bi-directional sync workflow between HubSpot and Azure DevOps to your own Azure environment.

## What This Logic App Does

1. **Receives HTTP webhook** from HubSpot when a ticket is created or updated
2. **Parses HubSpot ticket data** (subject, content, status, ticket ID)
3. **Routes based on ticket stage** (pipeline stage determines action)
4. **Creates Azure DevOps work items** when HubSpot tickets are new
5. **Tags work items** with `HubSpotTicket-{id}` for bidirectional sync

## Prerequisites

- Azure subscription with permissions to create Logic Apps and API connections
- Existing resource group
- Azure DevOps organization and project
- Azure Key Vault with the following secret:
  - `hubspot-api-access-token` - Your HubSpot private app access token (for validation)
- Azure DevOps API connection configured
- HubSpot workflow configured to call this Logic App webhook

## Key Features

- **Webhook-driven**: Real-time sync triggered by HubSpot events
- **Stage-based routing**: Different actions based on ticket pipeline stage
- **Automatic tagging**: Work items tagged with `HubSpotTicket-{id}` for reverse sync
- **Correlation tracking**: Uses X-Correlation-ID headers for request tracing
- **Secure token handling**: HubSpot access token retrieved from Key Vault

## Deployment

### Update Parameters

Edit `parameters.json`:

```json
{
  "workflows_integrations_demo_la_hubspot_to_devops_name": {
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

### Get the Webhook URL

After deployment:
1. Open Logic App in Azure Portal
2. Go to **Overview** → **Trigger history** → **When an HTTP request is received**
3. Copy the **HTTP POST URL**
4. Save this URL - you'll need it for HubSpot workflow configuration

### Configure HubSpot Workflow

1. **Create HubSpot Workflow:**
   - Trigger: "Ticket is created" or "Ticket enters stage"
   - Action: "Send webhook"
   - Webhook URL: Your Logic App webhook URL
   - Method: POST

2. **Webhook Payload:**
```json
{
  "subject": "{{ticket.subject}}",
  "content": "{{ticket.content}}",
  "createdate": "{{ticket.createdate}}",
  "hs_ticket_id": {{ticket.hs_ticket_id}},
  "hs_pipeline_stage": {{ticket.hs_pipeline_stage}},
  "hs_lastactivitydate": "{{ticket.hs_lastactivitydate}}"
}
```

## Key Vault Secrets Reference

| Secret Name | Description | Example |
|------------|-------------|---------|
| `hubspot-api-access-token` | HubSpot private app token | `pat-na1-...` |

**Required HubSpot scopes:**
- `crm.objects.tickets.read`

## Workflow Logic

### Pipeline Stage Mapping

| HubSpot Stage | Stage ID | Action |
|---------------|----------|--------|
| New | 1 | Create Azure DevOps Issue |
| Other stages | Any | No action (future customization) |

### Work Item Creation

**Issue Title:** HubSpot ticket subject  
**Description:** HubSpot ticket content (HTML formatted)  
**Tags:** `HubSpotTicket-{ticket_id}`

Example:
```
Title: Customer cannot login
Description: <p>Customer reports 500 error when logging in...</p>
Tags: HubSpotTicket-12345678
```

### Expected Request Schema

```json
{
  "type": "object",
  "properties": {
    "content": { "type": "string" },
    "subject": { "type": "string" },
    "createdate": { "type": "integer" },
    "hs_ticket_id": { "type": "integer" },
    "hs_pipeline_stage": { "type": "integer" },
    "hs_lastactivitydate": {}
  }
}
```

## Testing

### Test with cURL:

```bash
curl -X POST https://your-logic-app-url.azurewebsites.net/... \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: test-123" \
  -d '{
    "subject": "Test Ticket",
    "content": "This is a test ticket from HubSpot",
    "createdate": 1704355200000,
    "hs_ticket_id": 12345678,
    "hs_pipeline_stage": 1,
    "hs_lastactivitydate": 1704355200000
  }'
```

### Verify:
1. **Logic App run history** shows successful execution
2. **Azure DevOps project** has new Issue created
3. **Issue tags** include `HubSpotTicket-12345678`
4. **DevOps-to-HubSpot workflow** can now sync status back to HubSpot

## Troubleshooting

**Webhook not triggered:**
- Verify HubSpot workflow is active
- Check webhook URL matches Logic App trigger URL exactly
- Ensure HubSpot workflow sends POST request
- Review HubSpot workflow execution history

**400 Bad Request:**
- Verify webhook payload matches expected JSON schema
- Check all required fields are present
- Ensure `hs_ticket_id` and `hs_pipeline_stage` are integers, not strings

**401 Unauthorized errors:**
- Verify managed identity has Key Vault access policy
- Check `hubspot-api-access-token` secret exists
- Confirm HubSpot private app has not been deactivated

**Work item not created:**
- Verify DevOps connection is authorized
- Check project/organization names in connection settings
- Ensure DevOps project allows Issue work item type
- Review Logic App run history for specific error details

**Missing tags:**
- Verify tag format: `HubSpotTicket-{id}` (no spaces)
- Check DevOps project allows custom tags
- Review the "Create an Issue" action body in run history

## Architecture Notes

This Logic App works with the `devops-to-hubspot` workflow to create bidirectional sync:

```
HubSpot Ticket Created → hubspot-to-devops → DevOps Work Item Created
                                              (with HubSpotTicket-{id} tag)
                                                        ↓
DevOps Work Item Resolved → devops-to-hubspot → HubSpot Ticket Note Added
```

## Customization

### Add More Stage Handlers

Edit the `Switch` action to handle additional pipeline stages:

```json
{
  "cases": {
    "New": {
      "case": 1,
      "actions": { /* create issue */ }
    },
    "In Progress": {
      "case": 2,
      "actions": {
        // Update existing work item status
      }
    },
    "Resolved": {
      "case": 3,
      "actions": {
        // Close work item
      }
    }
  }
}
```

### Include More Ticket Fields

Modify the JSON schema and DevOps issue creation:

```json
{
  "body": {
    "title": "@body('Parse_JSON')?['subject']",
    "description": "@{body('Parse_JSON')?['content']}",
    "dynamicFields": {
      "System.Tags": "HubSpotTicket-@{body('Parse_JSON')?['hs_ticket_id']}",
      "Microsoft.VSTS.Common.Priority": "@{body('Parse_JSON')?['hs_ticket_priority']}",
      "System.AreaPath": "Support"
    }
  }
}
```

### Add Response to HubSpot

Add a "Response" action to acknowledge webhook:

```json
{
  "type": "Response",
  "inputs": {
    "statusCode": 200,
    "body": {
      "message": "Work item created",
      "workItemId": "@{body('Create_an_Issue')?['id']}"
    }
  }
}
```

### Error Notification

Add error handling to notify your API:

```json
{
  "runAfter": {
    "Main_Workflow": ["Failed", "TimedOut"]
  },
  "type": "Http",
  "inputs": {
    "uri": "https://your-api.azurewebsites.net/webhooks/logic-app-error",
    "method": "POST",
    "body": {
      "workflowName": "hubspot-to-devops",
      "errorMessage": "@{result('Main_Workflow')}"
    }
  }
}
```

## Integration with Sentry

If using the Sentry error logging endpoint from the main API, configure error reporting:

```json
{
  "Report_Error_to_API": {
    "runAfter": {
      "Main_Workflow": ["Failed"]
    },
    "type": "Http",
    "inputs": {
      "uri": "https://your-api.azurewebsites.net/webhooks/logic-apps/error",
      "method": "POST",
      "body": {
        "workflowRunId": "@{workflow().run.name}",
        "workflowName": "hubspot-to-devops",
        "errorDetails": "@{result('Main_Workflow')}"
      }
    }
  }
}
```
