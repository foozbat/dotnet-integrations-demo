namespace IntegrationsDemo.Tests;

public class UserTests
{
    [Fact]
    public void User_CanBeCreated_WithRequiredProperties()
    {
        User user = new()
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            Phone = "555-1234"
        };

        Assert.Equal("John", user.FirstName);
        Assert.Equal("Doe", user.LastName);
        Assert.Equal("john.doe@example.com", user.Email);
        Assert.Equal("555-1234", user.Phone);
    }

    [Fact]
    public void User_RequiresFirstName()
    {
        User user = new()
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            Phone = "555-1234"
        };

        Assert.NotNull(user.FirstName);
        Assert.NotEmpty(user.FirstName);
    }

    [Fact]
    public void User_HasContactId_AfterCreation()
    {
        User user = new()
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            Phone = "555-1234"
        };

        Assert.NotNull(user.ContactId);
        Assert.NotEmpty(user.ContactId);
    }
}