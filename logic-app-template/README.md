# Azure Logic App ARM Template

This directory contains an Azure Resource Manager (ARM) template for deploying the integration Logic App.

## What This Logic App Does

1. **Receives HTTP webhook** from your .NET API when a new lead signs up
2. **Parses the lead data** (firstName, lastName, email, phone, contactId)
3. **Creates/updates contact in HubSpot** if not already exists
4. **Triggers Zapier webhook** for additional actions (email, SMS, Slack notifications)

## Prerequisites

- Azure subscription
- Azure Key Vault with the following secrets:
  - `hubspot-api-access-token` - Your HubSpot private app access token
  - `zapier-webhook-url` - Your Zapier catch webhook URL (optional if using parameter)
- Permissions to create Logic Apps and API connections in your resource group

## Deployment

### Option 1: Azure Portal

1. Go to Azure Portal → Create a resource → Search for "Template deployment"
2. Click "Build your own template in the editor"
3. Copy the contents of `template.json` and paste it
4. Fill in the parameters:
   - **logicAppName**: Name for your Logic App (e.g., `integrations-logic-app`)
   - **location**: Azure region (e.g., `centralus`, `eastus`)
   - **keyVaultName**: Name of your existing Key Vault
   - **zapierWebhookUrl**: (Optional) Direct Zapier URL or leave empty to use Key Vault secret

### Option 2: Azure CLI

```bash
# Create or use existing resource group
az group create --name integrations-demo --location centralus

# Deploy template
az deployment group create \
  --resource-group integrations-demo \
  --template-file template.json \
  --parameters parameters.json
```

## Configuration

### Update parameters.json

Edit `parameters.json` with your values:

```json
{
  "logicAppName": {
    "value": "my-integrations-logic-app"
  },
  "location": {
    "value": "centralus"
  },
  "keyVaultName": {
    "value": "my-keyvault-name"
  },
  "zapierWebhookUrl": {
    "value": ""
  }
}
```

### Key Vault Setup

The Logic App's managed identity needs access to your Key Vault:

1. Deploy the Logic App first
2. Go to your Key Vault → Access policies
3. Add access policy:
   - Secret permissions: **Get**, **List**
   - Select principal: Search for your Logic App name
   - Save the policy

### HubSpot Configuration

Create a private app in HubSpot with these scopes:
- `crm.objects.contacts.write`
- `crm.objects.contacts.read`

Store the access token in Key Vault as `hubspot-api-access-token`.

### Zapier Configuration

1. Create a Zap with a "Catch Hook" trigger
2. Copy the webhook URL
3. Either:
   - Store it in Key Vault as `zapier-webhook-url`, OR
   - Pass it directly in the `zapierWebhookUrl` parameter

## Getting the Logic App URL

After deployment, get the HTTP trigger URL:

```bash
# Azure CLI
az logic workflow show \
  --resource-group integrations-demo \
  --name my-integrations-logic-app \
  --query "accessEndpoint" -o tsv
```

```powershell
# PowerShell
(Get-AzLogicApp -ResourceGroupName integrations-demo -Name my-integrations-logic-app).AccessEndpoint
```

Or from Azure Portal:
1. Open your Logic App
2. Go to Logic app designer
3. Expand the HTTP trigger
4. Copy the "HTTP POST URL"

## Updating Your .NET API

Set the Logic App URL as an environment variable or in Key Vault:

```bash
# Update App Service setting
az webapp config appsettings set \
  --name your-app-name \
  --resource-group integrations-demo \
  --settings AZURE_LOGIC_APP_URL='https://your-logic-app-url'
```

## Testing

Send a test request to the Logic App URL:

```bash
curl -X POST https://your-logic-app-url \
  -H "Content-Type: application/json" \
  -d '{
    "id": 1,
    "firstName": "Test",
    "lastName": "User",
    "email": "test@example.com",
    "phone": "123-456-7890",
    "contactId": "test-guid-123",
    "hubspotContactId": null
  }'
```

## Monitoring

View run history:
1. Azure Portal → Your Logic App → Overview
2. Click on "Runs history"
3. Click on a run to see detailed execution

## Customization

The template includes:
- **System-assigned managed identity** for secure Key Vault access
- **Correlation ID tracking** via `x-correlation-id` header
- **Conditional HubSpot creation** (only if `hubspotContactId` is empty)
- **Custom contact property mapping** for `external_contact_id`

To modify the workflow:
1. Deploy the template
2. Edit the Logic App in the Azure Portal designer
3. Export the updated template if needed

## Troubleshooting

**Logic App fails to access Key Vault:**
- Verify managed identity has Key Vault access policy
- Check Key Vault firewall settings

**HubSpot API returns 401:**
- Verify `hubspot-api-access-token` secret exists and is valid
- Check HubSpot private app scopes

**Zapier webhook not triggering:**
- Verify webhook URL is correct
- Check Zapier catch hook is active
- Look for the correlation ID in Zapier logs
