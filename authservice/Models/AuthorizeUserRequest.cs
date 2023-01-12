using System.ComponentModel.DataAnnotations;

namespace authservice.Models;

public class AuthorizeUserRequest
{
    [Required, EmailAddress]
    public Guid UserId { get; set; }
    [Required]
    public bool Authenticated { get; set; }
}