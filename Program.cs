/**
 * Dotnet Integrations Demo API
 *
 * written by: Aaron Bishop
 * description: A sample .NET 7 Web API demonstrating integrations with Azure Logic Apps, HubSpot, Stripe, and Azure SQL Database.
 * 
 * Refer to the README.md for detailed project information.
 */

using dotenv.net;
using Microsoft.OpenApi;
using Microsoft.EntityFrameworkCore;
using IntegrationsDemo;
using Stripe.Checkout;

// Load .env file if it exists
DotEnv.Load(options: new DotEnvOptions(ignoreExceptions: true));

var webhookUrl = Environment.GetEnvironmentVariable("AZURE_LOGIC_APP_URL") ?? "";
var connectionString = Environment.GetEnvironmentVariable("AZURE_SQL_CONNECTION_STRING") ?? "";
var stripeWebhookSecret = Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET") ?? "";

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    // enable for demo, would normally only enable this in dev environment
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Dotnet Integrations API", Description = "An Amazing Dotnet Integrations API", Version = "v1" });
});

// Configure database connection
builder.Services.AddDbContext<AzureSQLDbContext>(options =>
{
    _ = options.UseSqlServer(connectionString, sqlOptions =>
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null));
});

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
_ = app.UseSwagger();
_ = app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Dotnet Integrations API v1"));

// Middleware to log raw request bodies
app.Use(async (context, next) =>
{
    // also check if this is development environment
    if (context.Request.Method is "POST" or "PUT" or "PATCH" && context.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
    {
        context.Request.EnableBuffering();

        using StreamReader reader = new(context.Request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;

        ILogger<Program> logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Raw request to {Method} {Path}: {Body}", 
            context.Request.Method, 
            context.Request.Path, 
            body);
    }
    await next();
});

app.UseHttpsRedirection();

/**
 * API ENDPOINTS
 */

// Health check endpoint
app.MapGet("/", () => Results.Ok(new { status = "healthy", message = "Dotnet Integrations API is running" }))
.WithName("HealthCheck")
.ExcludeFromDescription();

// Endpoint: POST /api/signup
// Captures new lead information, validates data, stores in Azure SQL Database, and triggers Logic Apps workflow
app.MapPost("/api/signup", async (AzureSQLDbContext db, Lead lead, ILogger<Program> logger) =>
{
    // validation
    if (string.IsNullOrEmpty(lead.FirstName) || string.IsNullOrEmpty(lead.LastName) || string.IsNullOrEmpty(lead.Email) || string.IsNullOrEmpty(lead.Phone))
    {
        return Results.BadRequest(new { Message = "First Name, Last Name, Email, and Phone are required." });
    }

    // check for valid email
    if (!ValidEmail().IsMatch(lead.Email))
    {
        return Results.BadRequest(new { Message = "Invalid email format." });
    }

    // check for duplicate email
    var emailExists = await db.Leads.AnyAsync(l => l.Email == lead.Email);
    if (emailExists)
    {
        return Results.BadRequest(new { Message = "A lead with this email already exists." });
    }

    // save to db
    _ = db.Leads.Add(lead);
    _ = await db.SaveChangesAsync();

    // send webhook to Azure Logic Apps in the background
    JsonWebhook webhook = new()
    {
        WebhookUrl = webhookUrl,
        CorrelationId = Guid.NewGuid().ToString(),
        Timeout = TimeSpan.FromSeconds(30),
        Logger = logger
    };
    _ = Task.Run(async () => await webhook.SendAsync(lead));

    return Results.Ok(new { Message = "Signup successful." });
})
.WithName("SignUp")
.WithSummary("Create a new lead signup")
.WithDescription("Validates and stores a new lead in the database, then asynchronously sends the data to an Azure Logic Apps webhook for further processing.")
.Produces(200)
.Produces(400);

// Endpoint: GET /api/leads
// Returns a list of all leads stored in the database.
app.MapGet("/api/leads", async (AzureSQLDbContext db) => await db.Leads.ToListAsync())
.WithName("GetLeads")
.WithSummary("Get all leads")
.WithDescription("Returns a list of all leads stored in the database.")
.Produces<List<Lead>>(200);

/**
 * WEBHOOK ENDPOINTS
 */

// Endpoint: POST /webhooks/hubspot
// Receives HubSpot contact registration data and updates the corresponding lead in the database.
app.MapPost("/webhooks/hubspot", async (AzureSQLDbContext db, HubspotWebhookEvent hubspotData, ILogger<Program> logger) =>
{
    // LOG THE RAW JSON RECEIVED
    logger.LogInformation("Received HubSpot contact update webhook: {@hubspotData}", hubspotData);

    // update the lead with a HubspotContactId if it exists based on external_contact_id => ContactId mapping
    if (string.IsNullOrEmpty(hubspotData.ExternalContactId) || hubspotData.HubspotContactId == 0)
    {
        return Results.BadRequest(new { Message = "ExternalContactId and HubspotContactId are required." });
    }

    Lead? lead = await db.Leads.FirstOrDefaultAsync(l => l.ContactId == hubspotData.ExternalContactId);

    if (lead == null)
    {
        return Results.NotFound(new { Message = "Lead not found with the specified ContactId." });
    }

    lead.HubspotContactId = hubspotData.HubspotContactId;
    lead.UpdatedAt = DateTime.UtcNow;
    _ = await db.SaveChangesAsync();

    return Results.Ok(new { Message = "Hubspot contact registered.", lead.Id, lead.ContactId });
})
.WithName("UpdateHubspotContactId")
.WithSummary("Update HubSpot Contact ID for a lead")
.WithDescription("Updates a lead with their HubSpot contact ID using the external contact ID for mapping.")
.Produces(200)
.Produces(400)
.Produces(404);

// Endpoint: POST /webhooks/stripe
// Receives Stripe webhook events when a checkout session is completed
app.MapPost("/webhooks/stripe", async (HttpContext context, AzureSQLDbContext db, ILogger<Program> logger) =>
{
    // Read raw body for signature verification
    using StreamReader reader = new(context.Request.Body);
    var json = await reader.ReadToEndAsync();

    // Verify webhook signature
    var signatureHeader = context.Request.Headers["Stripe-Signature"].ToString();

    Stripe.Event? stripeEvent = null;

    if (string.IsNullOrEmpty(stripeWebhookSecret))
    {
        logger.LogWarning("STRIPE_WEBHOOK_SECRET not configured, skipping signature verification");
        // Parse without verification in dev/testing
        stripeEvent = Stripe.EventUtility.ParseEvent(json, throwOnApiVersionMismatch: false);
    }
    else if (string.IsNullOrEmpty(signatureHeader))
    {
        logger.LogWarning("Missing Stripe-Signature header");
        return Results.BadRequest(new { Message = "Missing signature header." });
    }
    else
    {
        try
        {
            stripeEvent = Stripe.EventUtility.ConstructEvent(json, signatureHeader, stripeWebhookSecret, throwOnApiVersionMismatch: false);
            logger.LogInformation("Webhook signature verified successfully");
        }
        catch (Stripe.StripeException e)
        {
            logger.LogError(e, "Webhook signature verification failed");
            return Results.BadRequest(new { Message = "Invalid signature." });
        }
    }

    if (stripeEvent == null)
    {
        return Results.BadRequest(new { Message = "Failed to parse Stripe event." });
    }

    logger.LogInformation("Received Stripe webhook: {EventType} {EventId}", stripeEvent.Type, stripeEvent.Id);

    // Ignore non-checkout completed events
    if (stripeEvent.Type != "checkout.session.completed")
    {
        logger.LogWarning("Ignoring Stripe event type: {EventType}", stripeEvent.Type);
        return Results.Ok(new { Message = "Event type not handled." });
    }

    if (stripeEvent.Data.Object is not Session session)
    {
        return Results.BadRequest(new { Message = "Invalid checkout session data." });
    }

    // note: customerId will only be provided if this is a subscription
    var customerId = session.CustomerId;
    var customerEmail = session.CustomerDetails?.Email;

    if (string.IsNullOrEmpty(customerId) || string.IsNullOrEmpty(customerEmail))
    {
        return Results.BadRequest(new { Message = "Customer ID and email are required." });
    }

    Lead? lead = await db.Leads.FirstOrDefaultAsync(l => l.Email == customerEmail);
    if (lead == null)
    {
        return Results.NotFound(new { Message = "Lead not found with the specified email." });
    }

    // Update lead with Stripe subscription info
    lead.StripeCustomerId = customerId;
    lead.SubscriptionStatus = session.Status ?? "complete";
    lead.UpdatedAt = DateTime.UtcNow;
    _ = await db.SaveChangesAsync();

    logger.LogInformation("Updated lead {LeadId} with Stripe customer {CustomerId}", lead.Id, customerId);

    return Results.Ok(new { Message = "Payment processed successfully.", lead.Id, lead.StripeCustomerId });
})
.WithName("StripeCheckoutCompleted")
.WithSummary("Handle Stripe checkout completion")
.WithDescription("Receives Stripe webhook when a checkout session completes and updates the lead with customer ID.")
.Produces(200)
.Produces(400)
.Produces(404);

/**
 * Stripe redirect endpoints for testing purposes
 * Implement in a real front-end
 */

// Endpoint: GET /payment-success
// Payment success page
app.MapGet("/payment-success", () => Results.Content("""
    <html>
        <body style="font-family: Arial; text-align: center; padding: 50px;">
            <h1>Payment Successful!</h1>
            <p>Thank you for your payment. You will receive a confirmation email shortly.</p>
        </body>
    </html>
    """, "text/html"))
.WithName("PaymentSuccess")
.ExcludeFromDescription();

// Endpoint: GET /payment-cancelled
// Payment cancelled page  
app.MapGet("/payment-cancelled", () => Results.Content("""
    <html>
        <body style="font-family: Arial; text-align: center; padding: 50px;">
            <h1>Payment Cancelled</h1>
            <p>Your payment was cancelled. No charges were made.</p>
        </body>
    </html>
    """, "text/html"))
.WithName("PaymentCancelled")
.ExcludeFromDescription();

// Run the application
app.Run();

public partial class Program
{
    [System.Text.RegularExpressions.GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase, "en-US")]
    private static partial System.Text.RegularExpressions.Regex ValidEmail();
}