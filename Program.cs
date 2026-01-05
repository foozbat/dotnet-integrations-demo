/**
 * Dotnet Integrations Demo API
 *
 * written by: Aaron Bishop
 * description: A .NET 10 Web API demonstrating integrations with Azure Logic Apps, HubSpot, Stripe, and Azure SQL Database.
 * 
 * Refer to the README.md for detailed project information.
 */

using dotenv.net;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.OpenApi;
using Microsoft.EntityFrameworkCore;
using IntegrationsDemo;
using Stripe.Checkout;

// Load .env file if it exists
DotEnv.Load(options: new DotEnvOptions(ignoreExceptions: true));

// connect to Azure Key Vault (prod only)
var keyVaultUrl = Environment.GetEnvironmentVariable("AZURE_KEY_VAULT_URL") ?? "";
SecretClient? kv = null;
if (!string.IsNullOrEmpty(keyVaultUrl))
{
    kv = new(new Uri(keyVaultUrl), new DefaultAzureCredential());
}

// Get secrets (prod), fall back to environment variables (dev)
string GetSecret(string name)
{
    if (kv != null)
    {
        try
        {
            return kv.GetSecret(name).Value.Value;
        }
        catch (Azure.RequestFailedException)
        {
            // fall back to environment variable
        }
    }

    return Environment.GetEnvironmentVariable(name.ToUpperInvariant().Replace("-", "_")) ?? "";
}

var connectionString = GetSecret("azure-sql-connection-string");
var logicAppUrl = GetSecret("azure-logic-app-url");
var stripeWebhookSecret = GetSecret("stripe-webhook-secret");
var sentryDsn = GetSecret("sentry-dsn");

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Suppress HTTPS redirect warning in development
builder.Logging.AddFilter("Microsoft.AspNetCore.HttpsPolicy.HttpsRedirectionMiddleware", LogLevel.Error);

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
    options.UseSqlServer(connectionString, sqlOptions =>
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null));
});

// Add Sentry SDK
builder.WebHost.UseSentry(options =>
{
    options.Dsn = sentryDsn;

    // If DSN is empty, Sentry won't initialize
    if (string.IsNullOrEmpty(sentryDsn))
    {
        Console.WriteLine("WARNING: SENTRY_DSN is not set. Sentry will not be initialized.");
        return;
    }

    options.Debug = true; // Always show debug output for this demo
    options.TracesSampleRate = 1.0;
    options.MinimumBreadcrumbLevel = LogLevel.Information;
    options.MinimumEventLevel = LogLevel.Warning; // Changed from Error to Warning
    options.EnableLogs = true;

    Console.WriteLine($"Sentry initialized with DSN: {sentryDsn[..30]}...");
});

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Dotnet Integrations API v1"));

/**
 * MIDDLEWARE
 */

// Middleware to log raw request bodies in development for debugging endpoints
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

// Middleware to add Sentry context tags
app.Use(async (context, next) =>
{
    SentrySdk.ConfigureScope(scope =>
    {
        scope.SetTag("correlation_id", context.TraceIdentifier);
        scope.SetTag("endpoint", context.Request.Path.Value ?? "/");
    });
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

// Endpoint: POST /api/users
// Captures new lead information, validates data, stores in Azure SQL Database, and triggers Logic Apps workflow
app.MapPost("/api/users", async (AzureSQLDbContext db, LeadCreateRequest request, ILogger<Program> logger) =>
{
    // Check for duplicate email
    var emailExists = await db.Leads.AnyAsync(l => l.Email == request.Email);
    if (emailExists)
    {
        return Results.BadRequest(new { Message = "A user with this email already exists." });
    }

    // Create new lead from request
    Lead lead = new()
    {
        FirstName = request.FirstName,
        LastName = request.LastName,
        Email = request.Email,
        Phone = request.Phone,
        Plan = request.Plan
    };

    // Save to db
    db.Leads.Add(lead);
    await db.SaveChangesAsync();

    // Send webhook to Azure Logic Apps in the background
    JsonWebhook webhook = new()
    {
        WebhookUrl = logicAppUrl,
        CorrelationId = Guid.NewGuid().ToString(),
        Timeout = TimeSpan.FromSeconds(30),
        Logger = logger
    };
    _ = Task.Run(async () => await webhook.SendAsync(lead));

    return Results.Ok(new { Message = "Signup successful.", User = lead });
})
.WithName("CreateUserSignup")
.WithSummary("Create a new user signup")
.WithDescription("Validates and stores a new user in the database, then asynchronously sends the data to an Azure Logic Apps webhook for further processing.")
.Produces(200)
.Produces(400);

// Endpoint: GET /api/users
// Returns a list of all users stored in the database.
app.MapGet("/api/users", async (AzureSQLDbContext db) => await db.Leads.ToListAsync())
.WithName("GetUsers")
.WithSummary("Get all users")
.WithDescription("Returns a list of all users stored in the database.")
.Produces<List<Lead>>(200);

// Endpoint: PATCH /api/users/{id}
// Updates specific properties of an existing user
app.MapPatch("/api/users/{id}", async (AzureSQLDbContext db, int id, LeadUpdateRequest request, ILogger<Program> logger) =>
{
    Lead? lead = await db.Leads.FindAsync(id);

    if (lead == null)
    {
        return Results.NotFound(new { Message = "User not found." });
    }

    // Update only the properties that are provided (non-null)
    lead.FirstName = request.FirstName ?? lead.FirstName;
    lead.LastName = request.LastName ?? lead.LastName;
    lead.Phone = request.Phone ?? lead.Phone;
    lead.Plan = request.Plan ?? lead.Plan;
    lead.HubspotContactId = request.HubspotContactId ?? lead.HubspotContactId;

    if (request.Email != null)
    {
        // Check for duplicate email (excluding current user)
        var emailExists = await db.Leads.AnyAsync(l => l.Email == request.Email && l.Id != id);
        if (emailExists)
        {
            return Results.BadRequest(new { Message = "A user with this email already exists." });
        }

        lead.Email = request.Email;
    }

    await db.SaveChangesAsync();

    return Results.Ok(new { Message = "User updated successfully.", User = lead });
})
.WithName("UpdateUser")
.WithSummary("Update user properties")
.WithDescription("Updates one or more properties of an existing user. Only provided fields will be updated.")
.Produces(200)
.Produces(400)
.Produces(404);

// Endpoint: DELETE /api/users/{id}
// Deletes an existing user
app.MapDelete("/api/users/{id}", async (AzureSQLDbContext db, int id, ILogger<Program> logger) =>
{
    Lead? lead = await db.Leads.FindAsync(id);

    if (lead == null)
    {
        return Results.NotFound(new { Message = "User not found." });
    }

    db.Leads.Remove(lead);
    await db.SaveChangesAsync();

    return Results.Ok(new { Message = "User deleted successfully.", UserId = id });
})
.WithName("DeleteUser")
.WithSummary("Delete a user")
.WithDescription("Permanently deletes a user from the database.")
.Produces(200)
.Produces(404);

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
    await db.SaveChangesAsync();

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
            SentrySdk.CaptureException(e, scope =>
            {
                scope.SetTag("integration", "stripe");
                scope.SetTag("event_type", "signature_verification");
                scope.SetExtra("has_signature", !string.IsNullOrEmpty(signatureHeader));
            });

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
    await db.SaveChangesAsync();

    logger.LogInformation("Updated lead {LeadId} with Stripe customer {CustomerId}", lead.Id, customerId);

    return Results.Ok(new { Message = "Payment processed successfully.", lead.Id, lead.StripeCustomerId });
})
.WithName("StripeCheckoutCompleted")
.WithSummary("Handle Stripe checkout completion")
.WithDescription("Receives Stripe webhook when a checkout session completes and updates the lead with customer ID.")
.Produces(200)
.Produces(400)
.Produces(404);

// Endpoint: POST /webhooks/logic-app-error
// Receives error notifications from Azure Logic Apps for any workflow failure
app.MapPost("/webhooks/logic-apps/error", async (LogicAppError errorData, ILogger<Program> logger) =>
{
    logger.LogError("Logic App workflow error: {@errorData}", errorData);

    // Parse the scope results to find failed actions
    List<ActionResult> failedActions = errorData.ErrorDetails?
        .Where(action => action.Status is "Failed" or "TimedOut")
        .ToList() ?? [];

    var failedActionNames = string.Join(", ", failedActions.Select(a => a.Name));

    SentrySdk.CaptureMessage(
        $"Azure Logic App Workflow Failed: {failedActionNames}",
        scope =>
        {
            scope.Level = SentryLevel.Error;
            scope.SetTag("integration", "azure-logic-apps");
            scope.SetTag("workflow_name", errorData.WorkflowName);
            scope.SetTag("workflow_run_id", errorData.WorkflowRunId);
            scope.SetTag("failed_actions", failedActionNames);

            scope.SetExtra("trigger_time", errorData.TriggerTime.ToString("o"));
            scope.SetExtra("lead_data", System.Text.Json.JsonSerializer.Serialize(errorData.LeadData));
            scope.SetExtra("all_action_results", System.Text.Json.JsonSerializer.Serialize(errorData.ErrorDetails));

            // Add breadcrumbs for each action in the workflow
            foreach (ActionResult action in errorData.ErrorDetails ?? Enumerable.Empty<ActionResult>())
            {
                BreadcrumbLevel level = action.Status == "Succeeded"
                    ? BreadcrumbLevel.Info
                    : BreadcrumbLevel.Error;

                SentrySdk.AddBreadcrumb(
                    $"Action: {action.Name} - {action.Status}",
                    "workflow",
                    level: level
                );
            }

            // Set user context if email available
            if (errorData.LeadData?.TryGetValue("email", out var email) == true)
            {
                scope.User = new SentryUser { Email = email?.ToString() };
            }
        }
    );

    return Results.Ok(new { Message = "Error logged to Sentry", FailedActions = failedActionNames });
})
.WithName("LogicAppError")
.WithSummary("Receive Logic App error notifications")
.WithDescription("Logs any errors from Azure Logic Apps workflow to Sentry with full action details")
.Produces(200);

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