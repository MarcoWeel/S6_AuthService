namespace authservice.Models;

[Flags]
public enum Roles
{
    User = 1,
    ShopManager = 2,
    Admin = 4,
}
