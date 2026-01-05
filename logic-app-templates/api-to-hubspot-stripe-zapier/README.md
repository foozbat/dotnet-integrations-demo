# Azure Logic App ARM Template

This directory contains an Azure Resource Manager (ARM) template exported from an existing Logic App. Use this to deploy a copy of the integration workflow to your own Azure environment.

## What This Logic App Does

1. **Receives HTTP webhook** from your .NET API when a new lead signs up
2. **Parses the lead data** (firstName, lastName, email, phone, contactId, plan)
3. **Creates/updates contact in HubSpot** (with external_contact_id custom property)
4. **Creates Stripe checkout session** (if plan is "paid")
5. **Triggers Zapier webhook** with all lead data + checkout URL for notifications (email/SMS/Slack)

## Prerequisites

- Azure subscription with permissions to create Logic Apps and API connections
- Existing resource group
- Azure Key Vault with the following secrets:
  - `hubspot-api-access-token` - Your HubSpot private app access token
  - `stripe-secret-key` - Your Stripe secret key (starts with `sk_test_` or `sk_live_`)
  - `stripe-price-id` - Your Stripe price ID (starts with `price_`)
  - `zapier-webhook-url` - Your Zapier catch webhook URL

## Deployment

### Update Parameters

Edit `parameters.json`:

```json
{
  "workflows_integrations_demo_fzb_name": {
    "value": "YOUR-LOGIC-APP-NAME"
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

### Configure Key Vault Access

Grant the Logic App's managed identity access to Key Vault:
- **Secret permissions**: Get, List
- **Principal**: Your Logic App name

### Update API Configuration

Set the Logic App URL in your .NET API:

**Local (.env):**
```
AZURE_LOGIC_APP_URL=https://your-logic-app-url
```

**Azure App Service:**
```bash
az webapp config appsettings set \
  --name your-app-name \
  --resource-group your-resource-group \
  --settings AZURE_LOGIC_APP_URL='https://your-logic-app-url'
```

## Key Vault Secrets Reference

| Secret Name | Description | Example |
|------------|-------------|---------|
| `hubspot-api-access-token` | HubSpot private app token | `pat-na1-...` |
| `stripe-secret-key` | Stripe API secret key | `sk_test_...` or `sk_live_...` |
| `stripe-price-id` | Stripe product price ID | `price_...` |
| `zapier-webhook-url` | Zapier catch webhook URL | `https://hooks.zapier.com/...` |

## Testing

Test the workflow by sending a request to your API:

```bash
curl -X POST https://your-api.azurewebsites.net/api/signup \
  -H "Content-Type: application/json" \
  -d '{
    "firstName": "Test",
    "lastName": "User",
    "email": "test@example.com",
    "phone": "555-123-4567",
    "plan": "paid"
  }'
```

Then verify:
- Logic App run history shows successful execution
- HubSpot contact created with `external_contact_id`
- Zapier notifications triggered
- Stripe checkout session created (if plan is "paid")

## Troubleshooting

**401 errors:**
- Verify managed identity has Key Vault access policy
- Check secret names match exactly

**HubSpot failures:**
- Verify `hubspot-api-access-token` is valid
- Check HubSpot private app scopes include `crm.objects.contacts.write`

**Stripe failures:**
- Verify `stripe-secret-key` and `stripe-price-id` are correct
- Ensure Stripe price is active

**Zapier not triggered:**
- Verify `zapier-webhook-url` secret is correct
- Check Zapier catch hook is active
