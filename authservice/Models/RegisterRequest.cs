using System.ComponentModel.DataAnnotations;

namespace authservice.Models;

public class RegisterRequest
{
    [Required, EmailAddress]
    public string Email { get; set; }

    [Required, MinLength(4)]
    public string Username { get; set; }

    [Required, MinLength(4)]
    public string Password { get; set; }

    [Required, Phone]
    public string PhoneNumber { get; set; }

    [Required]
    public Roles Roles { get; set; }
}