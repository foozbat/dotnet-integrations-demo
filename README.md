[![CI](https://github.com/foozbat/dotnet-integrations-demo/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/foozbat/dotnet-integrations-demo/actions/workflows/ci.yml)

# .NET + Azure Logic Apps + HubSpot + Zapier Demo

This project demonstrates a simple, real-world cloud integration using:

- ASP.NET Core Web API
- Azure Logic Apps
- HubSpot CRM
- Zapier Webhooks

## Documentation
Pending

## Architecture Overview

```
Client (Postman / Swagger)
↓
ASP.NET Core API
├── Save Contact to Azure SQL DB
└── Azure Logic App
     ├── HubSpot CRM
     |   └── Create Contact
     |   └── Trigger Hubspot Workflow
     |       └── Update ASP.NET Contact with Hubspot Contact ID
     └── Zapier
         └── Email via Sendgrid
         └── SMS via Twilio
         └── Notify via Slack
```

### Azure Logic App Workflow
![Azure Logic App](screenshots/azure-logic-app.jpg)

### HubSpot Automation Workflow
![HubSpot Workflow](screenshots/hubspot-workflow.jpg)

### Zapier Integration Workflow
![Zapier Workflow](screenshots/zapier-workflow.jpg)

## Sample Request

```http
POST /api/signup
Content-Type: application/json

{
  "firstName": "Demo",
  "lastName": "User",
  "email": "demo.user@acme.com",
  "phone": "123-456-7890"
}
```

## Future Improvements
- Add authentication and authorization
- Enable Logic App to update existing contacts in HubSpot
- Improve error handling in Zapier