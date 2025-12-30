using dotenv.net;
using Microsoft.OpenApi;
using Microsoft.EntityFrameworkCore;
using IntegrationsDemo;

// Load .env file if it exists
DotEnv.Load(options: new DotEnvOptions(ignoreExceptions: true));

var webhookUrl = Environment.GetEnvironmentVariable("AZURE_LOGIC_APP_URL") ?? "";
var connectionString = Environment.GetEnvironmentVariable("AZURE_SQL_CONNECTION_STRING") ?? "";

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
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
    if (context.Request.Method == "POST" || context.Request.Method == "PUT" || context.Request.Method == "PATCH")
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

// -------------
// API ENDPOINTS
// -------------

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
    if (!MyRegex().IsMatch(lead.Email))
    {
        return Results.BadRequest(new { Message = "Invalid email format." });
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

// -----------------
// WEBHOOK ENDPOINTS
// -----------------

// Endpoint: POST /webhooks/hubspot/registerContact
// Receives HubSpot contact registration data and updates the corresponding lead in the database.
app.MapPost("/webhooks/hubspot/updateContactId", async (AzureSQLDbContext db, HubspotContactRecord hubspotData, ILogger<Program> logger) =>
{
    // LOG THE RAW JSON RECEIVED
    logger.LogInformation("Received HubSpot contact update webhook: {@hubspotData}", hubspotData);

    // update the lead with a HubspotContactId if it exists based on external_contact_id => ContactId mapping
    if (string.IsNullOrEmpty(hubspotData.external_contact_id) || string.IsNullOrEmpty(hubspotData.hs_object_id))
    {
        return Results.BadRequest(new { Message = "ExternalContactId and HubspotContactId are required." });
    }

    Lead? lead = await db.Leads.FirstOrDefaultAsync(l => l.ContactId == hubspotData.external_contact_id);

    if (lead == null)
    {
        return Results.NotFound(new { Message = "Lead not found with the specified ContactId." });
    }

    lead.HubspotContactId = hubspotData.hs_object_id;
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

app.Run();

public partial class Program
{
    [System.Text.RegularExpressions.GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase, "en-US")]
    private static partial System.Text.RegularExpressions.Regex MyRegex();
}