# HubSpot Workflow Deployment

This directory contains HubSpot workflow definitions that can be deployed via GitHub Actions.

## Workflow Overview

**register-contact-workflow.json**
- **Purpose:** Syncs HubSpot contacts back to the .NET API backend
- **Trigger:** When a contact has the `external_contact_id` custom property set
- **Action:** Sends a POST webhook with `ExternalContactId` and `HubspotContactId` to update the Lead record

## Prerequisites

### 1. Create HubSpot Custom Property

Before deploying, create a custom contact property in HubSpot:
- **Property name:** `external_contact_id`
- **Type:** Single-line text
- **Purpose:** Stores the Lead's ContactId from your .NET API

### 2. Create a HubSpot Legacy App

1. Go to HubSpot Settings → Integrations → Legacy Apps
2. Create a new private app with the following scopes:
   - `automation` (read/write)
   - `crm.objects.contacts` (read)
3. Copy the access token

### 3. Configure GitHub Secrets and Variables

Go to your repository Settings → Secrets and variables → Actions:

**Secrets:**
- `HUBSPOT_ACCESS_TOKEN` - Your HubSpot legacy app access token

**Variables:**
- `AZURE_APP_NAME` - Your Azure App Service name (e.g., `my-integrations-api`)

## Deployment

The workflow deploys automatically when you:
- Push changes to `hubspot-workflows/**` files on the `main` branch
- Manually trigger the "Deploy HubSpot Workflow" action

The GitHub Action will:
1. Replace `{{WEBHOOK_URL}}` with your Azure App Service URL
2. Create the workflow in HubSpot via the Automation v4 API

## Manual Configuration Required

⚠️ **Important:** After deployment, you must manually configure the webhook body in HubSpot UI:

1. Open the workflow in HubSpot (Settings → Workflows)
2. Edit the webhook action
3. In the "Request body" section, add these properties:
   - `ExternalContactId` → Map to contact property `external_contact_id`
   - `HubspotContactId` → Map to contact property `Record ID`

This manual step is required because the HubSpot Automation v4 API does not support setting webhook body mappings programmatically.

## How It Works

1. Your .NET API creates a contact in HubSpot via Azure Logic Apps
2. The Logic App sets the `external_contact_id` property to the Lead's `ContactId`
3. HubSpot workflow enrolls the contact (because `external_contact_id` IS_KNOWN)
4. Webhook sends `ExternalContactId` and `HubspotContactId` to `/webhooks/hubspot/updateContactId`
5. .NET API updates the Lead record with the HubSpot contact ID