using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Diagnostics;
using System.Text;

namespace authservice.Services;

public interface IMessagingService
{
    void Subscribe(string exchange, Action<BasicDeliverEventArgs, string, string> callback, string exchangeType);
    void Subscribe(string exchange, Action<BasicDeliverEventArgs, string, string> callback, string exchangeType, string bindingKey);
    void Publish(string exchange, string queue, string route, string request);
    void Publish(string exchange, string queue, string route, string request, byte[] message);
    Task<string> PublishAndRetrieve(string exchange, string request);
    Task<string> PublishAndRetrieve(string exchange, string request, byte[] message);
}

public class MessagingService : IMessagingService
{
    private readonly int MESSAGE_TIMEOUT = 20000;

    private readonly IConnection _connection;
    private readonly string _serviceGuid;

    public MessagingService(IConfiguration configuration)
    {
        var factory = new ConnectionFactory()
        {
            HostName = configuration["RabbitMq:Connection:Host"],
            UserName = configuration["RabbitMq:Connection:Username"],
            Password = configuration["RabbitMq:Connection:Password"],
            Port = int.Parse(configuration["RabbitMq:Connection:Port"])
        };

        _connection = factory.CreateConnection();
        _serviceGuid = Guid.NewGuid().ToString();
    }
    public void Subscribe(string exchange, Action<BasicDeliverEventArgs, string, string> callback, string exchangeType)
    {
        Subscribe(exchange, callback, exchangeType, null);
    }

    public void Subscribe(string exchange, Action<BasicDeliverEventArgs, string, string> callback, string exchangeType, string bindingKey)
    {
        string queue = $"{exchange}-{exchangeType}-consumer{(exchangeType == ExchangeType.Fanout ? $"-{_serviceGuid}" : "")}";
        IModel channel = bindingKey is not null ? CreateChannel(queue, exchange, exchangeType, bindingKey) : CreateChannel(queue, exchange, exchangeType);

        CreateConsumer(channel, exchange, queue, callback, true);
    }

    public void Publish(string exchange, string queue, string route, string request)
    {
        Publish(exchange, queue, route, request, Encoding.UTF8.GetBytes("null"));
    }

    public void Publish(string exchange, string queue, string route, string request, byte[] message)
    {
        IModel channel = CreateChannel(queue, exchange);
        Publish(channel, exchange, queue, route, request, message);
        channel.Close();
    }

    public async Task<string> PublishAndRetrieve(string exchange, string request)
    {
        return await PublishAndRetrieve(exchange, request, Encoding.UTF8.GetBytes("null"));
    }

    public async Task<string> PublishAndRetrieve(string exchange, string request, byte[] message)
    {
        string requestGuid = Guid.NewGuid().ToString();
        string route = $"{requestGuid}.{_serviceGuid}.request";
        string queue = $"{exchange}-messaging-{_serviceGuid}";
        IModel channel = CreateChannel(queue, exchange, ExchangeType.Topic, $"*.{_serviceGuid}.response");
        string consumeTag = "";

        List<string> result = new();

        var callback = (BasicDeliverEventArgs ea, string _queue, string _request) =>
        {
            if (ea.RoutingKey != $"{requestGuid}.{_serviceGuid}.response")
            {
                channel.BasicNack(ea.DeliveryTag, false, true);
                return;
            }

            channel.BasicAck(ea.DeliveryTag, false);

            result.Add(Encoding.UTF8.GetString(ea.Body.ToArray()));

            channel.BasicCancel(consumeTag);
            channel.Close();
        };

        consumeTag = CreateConsumer(channel, exchange, queue, callback, false);

        Publish(channel, exchange, queue, route, request, message);

        Stopwatch stopWatch = new();
        stopWatch.Start();
        while (result.Count == 0 && stopWatch.Elapsed.Seconds <= MESSAGE_TIMEOUT)
        {
        }

        if (result.Count == 0)
        {
            channel.BasicCancel(consumeTag);
            channel.Close();
            throw new Exception($"[{DateTime.Now:HH:mm:ss}] request timed out request: {request}, on exchange: {exchange}");
        }

        return result[0];
    }

    private static void Publish(IModel channel, string exchange, string queue, string route, string request, byte[] message)
    {
        Dictionary<string, object> headers = new()
        {
            { "queue", queue },
            { "request", request }
        };

        IBasicProperties props = channel.CreateBasicProperties();
        props.Headers = headers;

        channel.BasicPublish(exchange: exchange, routingKey: route, basicProperties: props, body: message);
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Sent: {request}, on exchange: {exchange}, with content: {Encoding.UTF8.GetString(message)}");
    }

    private static string CreateConsumer(IModel channel, string exchange, string queue, Action<BasicDeliverEventArgs, string, string> callback, bool autoAck)
    {
        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += (model, ea) =>
        {
            bool containsHeaders = ea.BasicProperties.Headers is not null;

            string _queue = containsHeaders && ea.BasicProperties.Headers.ContainsKey("queue") ? Encoding.UTF8.GetString((byte[])ea.BasicProperties.Headers["queue"]) : "";
            string _request = containsHeaders && ea.BasicProperties.Headers.ContainsKey("request") ? Encoding.UTF8.GetString((byte[])ea.BasicProperties.Headers["request"]) : "";
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Received: {_request}, on exchange: {exchange}, with content: {Encoding.UTF8.GetString(ea.Body.ToArray())}");
            callback(ea, _queue, _request);
        };

        return channel.BasicConsume(queue: queue, autoAck: autoAck, consumer: consumer);
    }

    private IModel CreateChannel(string queue, string exchange)
    {
        return CreateChannel(queue, exchange, null);
    }

    private IModel CreateChannel(string queue, string exchange, string exchangeType)
    {
        return CreateChannel(queue, exchange, exchangeType, null);
    }

    private IModel CreateChannel(string queue, string exchange, string exchangeType, string bindingKey)
    {
        Dictionary<string, object> args = new()
        {
            { "x-expires", 10000 }
        };

        IModel channel = _connection.CreateModel();
        if (exchangeType is null)
            channel.ExchangeDeclarePassive(exchange: exchange);
        else
            channel.ExchangeDeclare(exchange: exchange, type: exchangeType);
        channel.QueueDeclare(queue: queue, exclusive: false, autoDelete: false, arguments: args);

        if (bindingKey is not null)
            channel.QueueBind(queue: queue, exchange: exchange, routingKey: bindingKey);

        return channel;
    }
}
