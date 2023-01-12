namespace authservice.Models;

public class AuthenticateResult
{
    public Guid Id { get; set; }
    public string Username { get; set; }
    public string Email { get; set; }
    public string PhoneNumber { get; set; }
    public string Roles { get; set; }
    public string Token { get; set; }
    public bool Acknowledged { get; set; }
}

public class AuthenticateResponse : Response<AuthenticateResult>
{
    public AuthenticateResponse() { }
    public AuthenticateResponse(User user, string token)
    {
        Result = new AuthenticateResult()
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Roles = ((int)user.Roles).ToString(),
            Token = token,
            Acknowledged = user.Acknowledged
        };
    }
}