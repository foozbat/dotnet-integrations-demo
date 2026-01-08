using System.ComponentModel.DataAnnotations;

namespace IntegrationsDemo;

public class UserUpdateRequest
{
    [MaxLength(200)]
    public string? FirstName { get; set; }

    [MaxLength(200)]
    public string? LastName { get; set; }

    [EmailAddress]
    [MaxLength(200)]
    public string? Email { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(50)]
    public string? Plan { get; set; }

    public long? HubspotContactId { get; set; }
}
