using System.ComponentModel.DataAnnotations;

namespace IntegrationsDemo;

public class LeadCreateRequest
{
    [Required]
    [MaxLength(200)]
    public required string FirstName { get; set; }

    [Required]
    [MaxLength(200)]
    public required string LastName { get; set; }

    [Required]
    [EmailAddress]
    [MaxLength(200)]
    public required string Email { get; set; }

    [Required]
    [MaxLength(50)]
    public required string Phone { get; set; }

    [MaxLength(50)]
    public string? Plan { get; set; }
}
