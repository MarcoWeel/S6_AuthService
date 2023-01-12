using Microsoft.Build.Framework;
using Microsoft.EntityFrameworkCore;

namespace authservice.Models;

[Index(nameof(Email), IsUnique = true)]
public class User
{
    [Required]
    public Guid Id { get; set; }
    [Required]
    public string Username { get; set; }
    [Required]
    public string PasswordHash { get; set; }
    [Required]
    public string PhoneNumber { get; set; }
    [Required]
    public string Email { get; set; }
    [Required]
    public Roles Roles { get; set; }
    [Required]
    public bool Acknowledged { get; set; }
}
