namespace IntegrationsDemo.Tests;

public class LeadTests
{
    [Fact]
    public void Lead_CanBeCreated_WithRequiredProperties()
    {
        // Arrange & Act
        var lead = new Lead
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            Phone = "555-1234"
        };

        // Assert
        Assert.Equal("John", lead.FirstName);
        Assert.Equal("Doe", lead.LastName);
        Assert.Equal("john.doe@example.com", lead.Email);
        Assert.Equal("555-1234", lead.Phone);
    }
}
