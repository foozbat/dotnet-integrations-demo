[![CI](https://github.com/foozbat/dotnet-integrations-demo/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/foozbat/dotnet-integrations-demo/actions/workflows/ci.yml)

# .NET + Azure Logic Apps + HubSpot + Zapier Demo

This project demonstrates a simple, real-world cloud integration using:

- ASP.NET Core Web API
- Azure Logic Apps
- HubSpot CRM
- Zapier Webhooks

## Documentation
Pending

## Goal

When a user signs up via a .NET API endpoint:
1. The contact is created or updated in HubSpot
2. A real-time notification is sent via Zapier

---

## Architecture Overview

```
Client (Postman / Swagger)
↓
ASP.NET Core API
├── Save Contact to Azure SQL DB
└── Azure Logic App
     ├── HubSpot CRM
     |   └── Create / Update Contact
     |   └── Trigger Hubspot Workflow
     |       └── Qualify Lead
     |       └── Update ASP.NET Contact Property
     └── Zapier
         └── Email via Sendgrid
         └── SMS via Twilio
         └── Notify via Slack
```

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