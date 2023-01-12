namespace authservice.Models;

public abstract class Response<T>
{
    public bool Success { get; set; } = true;
    public string Details { get; set; }
    public T Result { get; set; }
}
