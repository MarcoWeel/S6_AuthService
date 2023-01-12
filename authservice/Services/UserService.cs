using authservice.Data;
using authservice.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace authservice.Services;

public interface IUserService
{
    Task<AuthenticateResponse> Authenticate(AuthenticateRequest authenticateRequest);
    Task<AuthenticateResponse> Register(RegisterRequest registerRequest);
    Task<DeleteResponse> Delete(Guid id);
    Task<AuthenticateResponse> Update(UpdateRequest updateRequest);
    Task<AuthenticateResponse> UpdateAsAdmin(UpdateRequest updateRequest);
}

public class UserService : IUserService
{
    private readonly string _jwt;
    private readonly IDataAccessService _dataAccessService;

    public UserService(IConfiguration configuration, IDataAccessService dataAccessService)
    {
        _jwt = configuration["Jwt:Key"];
        _dataAccessService = dataAccessService;
    }
    public async Task<AuthenticateResponse> Authenticate(AuthenticateRequest authenticateRequest)
    {
        User user = await _dataAccessService.GetByEmail(authenticateRequest.Email);

        if (user == null) return new AuthenticateResponse()
        {
            Success = false,
            Details = "Email or password is incorrect."
        };

        if (!user.Acknowledged) return new AuthenticateResponse()
        {
            Success = false,
            Details = user.Roles.ToString() + " account is not verified."
        };

        bool verify = BCrypt.Net.BCrypt.Verify(authenticateRequest.Password, user.PasswordHash);
        if (!verify) return new AuthenticateResponse()
        {
            Success = false,
            Details = "Email or password is incorrect."
        };

        var token = GenerateJwtToken(user, _jwt);

        return new AuthenticateResponse(user, token);
    }

    public async Task<AuthenticateResponse> Register(RegisterRequest registerRequest)
    {
        User existingUser = await _dataAccessService.GetByEmail(registerRequest.Email);

        if (existingUser != null) return new AuthenticateResponse()
        {
            Success = false,
            Details = "Email is already in use."
        };

        var user = new User() {
            Email = registerRequest.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(registerRequest.Password),
            Username = registerRequest.Username,
            PhoneNumber = registerRequest.PhoneNumber,
            Roles = registerRequest.Roles,
            Acknowledged = registerRequest.Roles == Roles.User
        };

        user = await _dataAccessService.AddUser(user);
        if (user == null)
            throw new Exception("user didnt add succesfully");

        string token = user.Acknowledged ? GenerateJwtToken(user, _jwt) : null;

        return new AuthenticateResponse(user, token);
    }

    public async Task<DeleteResponse> Delete(Guid id)
    {
        User user = await _dataAccessService.GetById(id);

        if (user == null)
        {
            return new DeleteResponse()
            {
                Success = false,
                Details = "User does not exist."
            };
        }

        await _dataAccessService.DeleteUser(id);

        await _dataAccessService.DeleteForGDPR(id);

        return new DeleteResponse(user);
    }

    public async Task<AuthenticateResponse> Update(UpdateRequest updateRequest)
    {
        User user = await _dataAccessService.GetById(updateRequest.Id);

        if (user == null)
        {
            return new AuthenticateResponse()
            {
                Success = false,
                Details = "User does not exist."
            };
        }

        if (updateRequest.CurrrentPassword == null) return new AuthenticateResponse()
        {
            Success = false,
            Details = "Please provide a password."
        };
        bool verify = BCrypt.Net.BCrypt.Verify(updateRequest.CurrrentPassword, user.PasswordHash);
        if (!verify) return new AuthenticateResponse()
        {
            Success = false,
            Details = "Current password is incorrect."
        };

        if (updateRequest.Email != null)
            user.Email = updateRequest.Email;
        if (updateRequest.Username != null)
            user.Username = updateRequest.Username;
        if (updateRequest.NewPassword != null)
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(updateRequest.NewPassword);
        if (updateRequest.PhoneNumber != null)
            user.PhoneNumber = updateRequest.PhoneNumber;

        User? updatedUser = await _dataAccessService.UpdateUser(user);
        if (updatedUser == null)
            throw new Exception("user didnt update succesfully");

        string token = GenerateJwtToken(updatedUser, _jwt);

        return new AuthenticateResponse(updatedUser, token);
    }

    public async Task<AuthenticateResponse> UpdateAsAdmin(UpdateRequest updateRequest)
    {
        User user = await _dataAccessService.GetById(updateRequest.Id);

        if (user == null)
        {
            return new AuthenticateResponse()
            {
                Success = false,
                Details = "User does not exist."
            };
        }

        if (updateRequest.Email != null)
            user.Email = updateRequest.Email;
        if (updateRequest.Username != null)
            user.Username = updateRequest.Username;
        if (updateRequest.NewPassword != null)
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(updateRequest.NewPassword);
        if (updateRequest.PhoneNumber != null)
            user.PhoneNumber = updateRequest.PhoneNumber;
        if (updateRequest.Acknowledged != null)
            user.Acknowledged = updateRequest.Acknowledged.Value;
        if (updateRequest.Roles != null)
            user.Roles = updateRequest.Roles.Value;

        User? updateUser = await _dataAccessService.UpdateUser(user);
        if (updateUser == null)
            throw new Exception("user didnt update succesfully");

        string token = GenerateJwtToken(updateUser, _jwt);

        return new AuthenticateResponse(updateUser, token);
    }

    public string GenerateJwtToken(User user, string key)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var _key = Encoding.ASCII.GetBytes(key);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("id", user.Id.ToString()),
                new Claim("role", ((int)user.Roles).ToString())
            }),
            Expires = DateTime.UtcNow.AddDays(1),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(_key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}
