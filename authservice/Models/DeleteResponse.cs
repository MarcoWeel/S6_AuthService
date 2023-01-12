namespace authservice.Models;

public class DeleteResponse : Response<User>
{
    public DeleteResponse() { }

    public DeleteResponse(User user)
    {
        Result = user;
    }
}