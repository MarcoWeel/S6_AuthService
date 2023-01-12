using System.ComponentModel.DataAnnotations;

namespace authservice.Models;

public class UpdateRequest
{
    [Required]
    public Guid Id { get; set; }

    public string? CurrrentPassword { get; set; }

    [EmailAddress]
    public string? Email { get; set; }

    [MinLength(4)]
    public string? Username { get; set; }

    [MinLength(4)]
    public string? NewPassword { get; set; }

    [Phone]
    public string? PhoneNumber { get; set; }

    public Roles? Roles { get; set; }

    public bool? Acknowledged { get; set; }
}
