[![CI](https://github.com/foozbat/dotnet-integrations-demo/actions/workflows/ci.yml/badge.svg?branch=master)](https://github.com/foozbat/dotnet-integrations-demo/actions/workflows/ci.yml)

# .NET + Azure Logic Apps + HubSpot + Zapier Demo

This project demonstrates a simple, real-world cloud integration using:

- ASP.NET Core Web API
- Azure Logic Apps
- HubSpot CRM
- Zapier Webhooks

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
├── Save to Azure SQL DB
└── Azure Logic App
     ├── HubSpot CRM
     └── Zapier
         └── Email via Sendgrid
         └── SMS via Twilio
```

---

## Flow Description

1. A POST request is sent to `/api/signup`
2. The API forwards the request to an Azure Logic App
3. The Logic App:
   - Creates or updates a HubSpot contact
   - Sends a webhook to Zapier
4. Zapier sends a notification (Slack / Email)

---

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