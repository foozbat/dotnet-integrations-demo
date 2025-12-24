using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

[Index(nameof(CorrelationId))]
public class Lead
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(200)]
    public required string FirstName { get; set; }
    
    [Required]
    [MaxLength(200)]
    public required string LastName { get; set; }
    
    [Required]
    [MaxLength(200)]
    public required string Email { get; set; }
    
    [Required]
    [MaxLength(50)]
    public required string Phone { get; set; }
    
    [MaxLength(100)]
    public string? CorrelationId { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}