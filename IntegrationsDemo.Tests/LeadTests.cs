namespace IntegrationsDemo.Tests;

public class LeadTests
{
    [Fact]
    public void Lead_CanBeCreated_WithRequiredProperties()
    {
        Lead lead = new()
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            Phone = "555-1234"
        };

        Assert.Equal("John", lead.FirstName);
        Assert.Equal("Doe", lead.LastName);
        Assert.Equal("john.doe@example.com", lead.Email);
        Assert.Equal("555-1234", lead.Phone);
    }

    [Fact]
    public void Lead_RequiresFirstName()
    {
        Lead lead = new()
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            Phone = "555-1234"
        };

        Assert.NotNull(lead.FirstName);
        Assert.NotEmpty(lead.FirstName);
    }

    [Fact]
    public void Lead_HasContactId_AfterCreation()
    {
        Lead lead = new()
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            Phone = "555-1234"
        };

        Assert.NotNull(lead.ContactId);
        Assert.NotEmpty(lead.ContactId);
    }
}