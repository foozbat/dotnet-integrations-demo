# HubSpot Workflow Deployment

This directory contains HubSpot workflow definitions that can be deployed via GitHub Actions.

## Setup

1. **Create a HubSpot Legacy App:**
   - Grant the following scopes:
     - `automation` (read/write)
     - `crm.objects.contacts` (read)
   - Copy the access token

2. **Add GitHub Secrets:**
   - Go to your repository Settings → Secrets and variables → Actions
   - Add a new secret: `HUBSPOT_ACCESS_TOKEN`
   - Paste your HubSpot legacy app access token

3. **Update the webhook URL:**
   - Edit `hello-world-workflow.json`
   - Replace `https://webhook.site/unique-url-here` with your actual webhook endpoint
   - You can use your Azure Logic App URL or the .NET API endpoint

## Deployment

The workflow will automatically deploy when you:
- Push changes to `hubspot-workflows/**` files on the `main` branch
- Manually trigger via GitHub Actions UI

## Workflow Details

**hello-world-workflow.json**
- **Trigger:** When a new contact is created (createdate is known)
- **Action:** Sends a POST webhook with contact details
- **Enrollment:** Automatic (not manual)

## Testing

After deployment:
1. Create a new contact in HubSpot
2. The workflow should trigger automatically
3. Check the webhook endpoint for the "Hello World" message with contact data

## Customization

To modify the workflow:
1. Edit `hello-world-workflow.json`
2. Commit and push to `main` branch
3. GitHub Actions will automatically deploy the updated workflow
