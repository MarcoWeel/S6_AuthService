using authservice.Data;
using authservice.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Data;
using System.Text;

namespace authservice.Services;

public interface IDataAccessService
{
    Task<User> GetById(Guid id);
    Task<User> GetByEmail(string email);
    Task<User?> AddUser(User user);
    Task<User?> UpdateUser(User user);
    Task<bool> DeleteUser(Guid id);
    Task<List<User>> GetUsers();
    Task<List<User>> GetUsersWithRole(Roles[] roles);
    void SubscribeToGlobal();
}

public class DataAccessService : IDataAccessService
{
    private readonly IMessagingService _messagingService;

    public DataAccessService(IMessagingService messagingService)
    {
        _messagingService = messagingService;
    }

    public void SubscribeToGlobal()
    {
        _messagingService.Subscribe("auth", (BasicDeliverEventArgs ea, string queue, string request) => RouteCallback(ea, request), ExchangeType.Fanout, "*");
    }

    private static async void RouteCallback(BasicDeliverEventArgs ea, string request)
    {
        using AuthContext context = new();

        string data = Encoding.UTF8.GetString(ea.Body.ToArray());

        switch (request)
        {
            case "deleteuser":
                {
                    Guid id = Guid.Parse(data);
                    User user = await context.User.SingleOrDefaultAsync(m => m.Id == id);
                    if (user == null)
                        break;
                    context.User.Remove(user);
                    await context.SaveChangesAsync();
                    break;
                };
            case "updateuser":
                {
                    var updateduser = JsonConvert.DeserializeObject<User>(data);
                    if (updateduser == null)
                        break;

                    var olduser = await context.User.SingleOrDefaultAsync(m => m.Id == updateduser.Id);
                    if (olduser != null)
                    {
                        olduser.Email = updateduser.Email;
                        olduser.Username = updateduser.Username;
                        olduser.PasswordHash = updateduser.PasswordHash;
                        olduser.PhoneNumber = updateduser.PhoneNumber;
                        olduser.Acknowledged = updateduser.Acknowledged;
                        olduser.Roles = updateduser.Roles;
                    }
                    else
                    {
                        context.Add(updateduser);
                    }

                    await context.SaveChangesAsync();

                    break;
                }
            default:
                Console.WriteLine($"Request {request} Not Found");
                break;
        }
    }

    public async Task<User> GetById(Guid id)
    {
        using AuthContext context = new();

        var user = await context.User.SingleOrDefaultAsync(m => m.Id == id);

        if (user != null)
            return user;

        string response = await _messagingService.PublishAndRetrieve("auth-data", "getbyid", Encoding.UTF8.GetBytes(id.ToString()));

        user = JsonConvert.DeserializeObject<User>(response);
        if (user == null)
            return null;

        context.Add(user);
        await context.SaveChangesAsync();

        return user;
    }

    public async Task<User> GetByEmail(string email)
    {
        using AuthContext context = new();

        var user = await context.User.SingleOrDefaultAsync(m => m.Email == email);

        if (user != null)
            return user;

        string response = await _messagingService.PublishAndRetrieve("auth-data", "getbyemail", Encoding.UTF8.GetBytes(email));
        if (response == null)
            return null;

        user = JsonConvert.DeserializeObject<User>(response);
        if (user == null)
            return null;

        context.Add(user);
        await context.SaveChangesAsync();

        return user;
    }

    public async Task<User?> AddUser(User user)
    {
        using AuthContext context = new();

        var existing = await context.User.SingleOrDefaultAsync(m => m.Email == user.Email);
        if (existing != null)
            return null;

        string response = await _messagingService.PublishAndRetrieve("auth-data", "adduser", Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(user)));

        user = JsonConvert.DeserializeObject<User>(response);
        if (user == null)
            return null;

        context.Add(user);
        await context.SaveChangesAsync();

        return user;
    }

    public async Task<User?> UpdateUser(User user)
    {
        using AuthContext context = new();

        string response = await _messagingService.PublishAndRetrieve("auth-data", "updateuser", Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(user)));

        user = JsonConvert.DeserializeObject<User>(response);
        if (user == null)
            return null;

        _messagingService.Publish("auth", "auth-messaging", "updateuser", "updateuser", Encoding.UTF8.GetBytes(response));

        return user;
    }

    public async Task<bool> DeleteUser(Guid id)
    {
        using AuthContext context = new();
        var response = await _messagingService.PublishAndRetrieve("auth-data", "deleteuser", Encoding.UTF8.GetBytes(id.ToString()));
        if (response == null)
            return false;

        var user = await context.User.SingleOrDefaultAsync(m => m.Id == id);
        if (user == null)
            return false;

        context.User.Remove(user);
        await context.SaveChangesAsync();
        _messagingService.Publish("auth", "auth-messaging", "deleteuser", "deleteuser", Encoding.UTF8.GetBytes(id.ToString()));
        _messagingService.Publish("gdprexchange", "", "gdpr", "", Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(id)));

        return true;
    }

    public async Task<List<User>> GetUsers()
    {
        using AuthContext context = new();

        if (!hasallusers)
            await RetrieveAllUsers(context);

        return await context.User.ToListAsync();
    }

    public async Task<List<User>> GetUsersWithRole(Roles[] roles)
    {
        using AuthContext context = new();

        if (!hasallusers && gettingUser == null)
            gettingUser = RetrieveAllUsers(context);

        if (!hasallusers)
            await gettingUser;

        return await context.User.Where(m => roles.Contains(m.Roles)).ToListAsync();
    }

    private bool hasallusers = false;
    private Task gettingUser;
    private async Task RetrieveAllUsers(AuthContext context)
    {
        try
        {
            string response = await _messagingService.PublishAndRetrieve("auth-data", "getallusers");
            List<User> users = JsonConvert.DeserializeObject<List<User>>(response);
            foreach (User user in users)
            {
                bool existing = context.User.FirstOrDefault(e => e.Id == user.Id) != null;
                if (!existing)
                    context.User.Add(user);
            }
            await context.SaveChangesAsync();
            await Task.Delay(1000);
            hasallusers = true;
        }
        catch(Exception ex)
        {
            gettingUser = null;
            throw new Exception(ex.Message);
        }
    }
}
