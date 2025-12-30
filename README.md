[![CI](https://github.com/foozbat/dotnet-integrations-demo/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/foozbat/dotnet-integrations-demo/actions/workflows/ci.yml)

# .NET Integrations Demo

A real-world cloud integration platform demonstrating modern API-first architecture with multi-channel customer engagement workflows. This project showcases seamless integration between CRM systems, notification services, and cloud automation tools.

<b>🎥 A live demo is available upon request.</b>

## 🛠️ Tech Stack

### Backend
- **.NET 10** - ASP.NET Core Web API with minimal APIs.
- **Swagger/OpenAPI** - Interactive API documentation.
- **Entity Framework Core 10**
- **Azure SQL Database**

### Integration & Automation
- **Azure Logic Apps** - For serverless workflow orchestration between API and integrations.
- **HubSpot CRM**
- **Zapier**
  - SendGrid for email
  - Twilio for SMS
  - Slack for team notifications

### DevOps
- **GitHub Actions** - CI/CD pipeline
- **Azure App Service** - PaaS hosting

## 🏗️ Architecture

```
Client (Postman / Swagger)
         ↓
   ASP.NET Core API
    ├── Save to Azure SQL
    └── Trigger Logic App
         ├── HubSpot CRM
         │   ├── Create Contact
         │   └── Workflow Callback
         │       └── Update Contact ID
         └── Zapier Webhook
             ├── Email (SendGrid)
             ├── SMS (Twilio)
             └── Slack Notification
```

### Workflow Visualizations (click to expand graphics)

<details>
<summary>Azure Resources</summary>

![Azure Resources](screenshots/azure-resource-group.jpg)
</details>

<details>
<summary>Azure Logic App Workflow</summary>

![Azure Logic App](screenshots/azure-logic-app.jpg)
</details>

<details>
<summary>HubSpot Automation Workflow</summary>

![HubSpot Workflow](screenshots/hubspot-workflow.jpg)
</details>

<details>
<summary>Zapier Integration Workflow</summary>

![Zapier Workflow](screenshots/zapier-workflow.jpg)
</details>

## 🚀 Quick Start

### Sample API Request

```http
POST /api/signup
Content-Type: application/json

{
  "firstName": "Demo",
  "lastName": "User",
  "email": "demo.user@acme.com",
  "phone": "555-456-7890"
}
```

## 📋 Future Enhancements
- Add authentication using Azure API Management
- Enable Logic App to update existing HubSpot contacts
- Enhanced error handling and retry policies
- Rate limiting and request throttling