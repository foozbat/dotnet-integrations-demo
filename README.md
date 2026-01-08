[![CI](https://github.com/foozbat/dotnet-integrations-demo/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/foozbat/dotnet-integrations-demo/actions/workflows/ci.yml)

# .NET Integrations Demo

A real-world cloud integration platform demonstrating modern API-first architecture with multi-channel customer engagement workflows. This project showcases seamless integration between CRM systems, notification services, and cloud automation tools.

<b>A live demo is available upon request.</b>

## Tech Stack

### Backend
- **.NET 10** - ASP.NET Core Web API with minimal APIs.
- **Swagger/OpenAPI** 
- **Entity Framework Core 10**
- **Azure SQL Database**

### Integration & Automation
- **Azure Logic Apps** - Serverless workflow orchestration
  - API → HubSpot/Stripe/Zapier (user signup)
  - HubSpot → Azure DevOps (ticket sync)
  - Azure DevOps → HubSpot (status updates)
- **Azure Key Vault** - Secure secret storage
- **HubSpot CRM** - Contact and ticket management
- **Azure DevOps** - Work item tracking
- **Stripe** - Payment processing
- **Zapier** - Multi-channel notifications (SendGrid, Twilio, Slack)
- **Sentry** - Error monitoring and alerting

### DevOps
- **GitHub Actions** - CI/CD pipeline
- **Azure App Service** - PaaS hosting

## Architecture

### User Signup Flow
```
Client (Postman / Swagger)
    ↓
ASP.NET Core API (Create User)
    ├── Validate and Save User to Azure SQL DB
    └── Trigger Azure Logic App (Webhook)
         ├── HubSpot CRM
         │   ├── Create/Update Contact (with external_contact_id)
         ├── Stripe (if paid plan)
         │   └── Create Checkout Session
         └── Zapier Webhook
             ├── Email (SendGrid) with checkout link
             ├── SMS (Twilio)
             └── Slack Notification

User Clicks Payment Link
    ↓
Stripe Checkout (Payment Complete)
    └── ASP.NET Core API (Stripe Webhook)
         ├── Validate Stripe Request
         └── Update User.StripeCustomerId, User.StripeSubscriptionId in Azure SQL DB
```

### HubSpot ↔ Azure DevOps Bidirectional Sync
```
HubSpot Ticket Created
    ↓
Hubpot Workflow
    └── Azure Logic App (Webhook)
         └── Create Azure DevOps Issue (tagged: HubSpotTicket-{id})

Azure DevOps Issue Closed
    ↓
Azure Logic App (Polling)
    └── Add Note to HubSpot Ticket ("Issue resolved by developers")
```

## Design Decisions
- Logic Apps used for long-running and fan-out workflows
- API remains stateless and focused on validation + persistence
- External systems isolated behind workflow boundaries
- Bidirectional sync via tag-based linking (HubSpot ↔ DevOps)
- Sentry integration for error tracking across all workflows

### Workflow Visualizations (click to expand graphics)

<details>
<summary>Azure Resources</summary>

![Azure Resources](screenshots/azure-resource-group.jpg)
</details>

<details>
<summary>Create User Logic Apps Workflow</summary>

![Azure Logic App](screenshots/azure-logic-app.jpg)
</details>

<details>
<summary>HubSpot ↔ Azure DevOps Sync</summary>

### HubSpot → DevOps
1. Webhook trigger from HubSpot ticket creation:

![HubSpot Workflow](screenshots/hubspot-workflow.jpg)

2. Creates Azure DevOps Issue with `HubSpotTicket-{id}` tag:

![Hubspot to Devops Workflow](screenshots/logicapp-hubspot-to-devops.jpg)

### DevOps → HubSpot
1. Polls Azure DevOps for work item updates
2. Adds note to HubSpot ticket when issue resolved

![Devops to Hubspot Workflow](screenshots/logicapp-devops-to-hubspot.jpg)

</details>

<details>
<summary>Zapier Integration Workflow</summary>

![Zapier Workflow](screenshots/zapier-workflow.jpg)
</details>

## Quick Start

### API Endpoints

**User Signup:**
```http
POST /api/users
Content-Type: application/json

{
  "firstName": "Demo",
  "lastName": "User",
  "email": "demo.user@acme.com",
  "phone": "555-456-7890",
  "plan": "paid"
}
```

**Logic App Error Reporting:**
```http
POST /webhooks/logic-apps/error
Content-Type: application/json

{
  "workflowRunId": "08585375681612345678",
  "workflowName": "hubspot-to-devops",
  "errorDetails": [...]
}
```

### Logic App Templates

Three ARM templates available in `logic-app-templates/`:
- **api-to-hubspot-stripe-zapier** - User signup orchestration
- **hubspot-to-devops** - Creates DevOps issues from HubSpot tickets
- **devops-to-hubspot** - Syncs DevOps status back to HubSpot

See individual README files for deployment instructions.

## Future Enhancements
- Add authentication using Azure API Management
- Implement retry policies with exponential backoff
- Add rate limiting and request throttling
- Expand DevOps sync to handle more ticket states