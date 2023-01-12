using Microsoft.EntityFrameworkCore;
using authservice.Models;

namespace authservice.Data;

public class AuthContext : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseInMemoryDatabase(databaseName: "AuthDB");
    }

    public DbSet<User> User { get; set; } = default!;
}
