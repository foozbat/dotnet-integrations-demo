using dotenv.net; 
using Microsoft.OpenApi;
using Microsoft.EntityFrameworkCore;

DotEnv.Load();
var env = DotEnv.Read();
string webhookUrl = env["AZURE_LOGIC_APP_URL"];
var connectionString = env["SQL_SERVER_CONNECTION_STRING"];

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Dotnet Integrations API", Description = "An Amazing Dotnet Integrations API", Version = "v1" });
});

// connect to azure sql database using entra authentication
builder.Services.AddDbContext<AzureSQLDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions => 
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null)));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Dotnet Integrations API v1"));
}

app.UseHttpsRedirection();

app.MapPost("/api/signup", async (AzureSQLDbContext db, Lead lead, ILogger<Program> logger) =>
{
    // validation
    if (string.IsNullOrEmpty(lead.FirstName) || string.IsNullOrEmpty(lead.LastName) || string.IsNullOrEmpty(lead.Email) || string.IsNullOrEmpty(lead.Phone))
    {
        return Results.BadRequest(new { Message = "First Name, Last Name, Email, and Phone are required." });
    }

    // check for valid email
    if (!System.Text.RegularExpressions.Regex.IsMatch(lead.Email, 
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$", 
        System.Text.RegularExpressions.RegexOptions.IgnoreCase))
    {
        return Results.BadRequest(new { Message = "Invalid email format." });
    }

    // add correlation id to track request across services
    lead.CorrelationId = Guid.NewGuid().ToString();

    // save to db
    db.Leads.Add(lead);
    await db.SaveChangesAsync();

    // send webhook to Azure Logic Apps in the background
    var webhook = new JsonWebhook
    {
        WebhookUrl = webhookUrl,
        CorrelationId = lead.CorrelationId,
        Timeout = TimeSpan.FromSeconds(30),
        Logger = logger
    };
    _ = Task.Run(async () => await webhook.SendAsync(lead));

    return Results.Ok(new { Message = "Signup successful." });
})
.WithName("SignUp");

app.MapGet("/api/leads", async (AzureSQLDbContext db) => await db.Leads.ToListAsync())
.WithName("GetLeads");

app.Run();

// enable for unit tests
public partial class Program { }