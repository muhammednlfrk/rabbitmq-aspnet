using System.Collections.Concurrent;
using System.Text;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Server.Services;

public class PersonService : IDisposable
{

    private readonly string _rpcQueueName;
    private readonly string _replyQueueName;
    private readonly string _correlationId;
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly IBasicProperties _properties;
    private readonly BlockingCollection<PersonModel> _responses = new BlockingCollection<PersonModel>();

    public PersonService(IConfiguration configuration)
    {
        _rpcQueueName = configuration["RabbitMQ:RPCQueueName"];
        var connectionFactory = new ConnectionFactory()
        {
            HostName = configuration["RabbitMQ:HostName"],
            UserName = configuration["RabbitMQ:UserName"],
            Password = configuration["RabbitMQ:Password"]
        };
        _connection = connectionFactory.CreateConnection();
        _channel = _connection.CreateModel();

        _replyQueueName = _channel.QueueDeclare().QueueName;
        _properties = _channel.CreateBasicProperties();
        _correlationId = Guid.NewGuid().ToString();
        _properties.CorrelationId = _correlationId;
        _properties.ReplyTo = _replyQueueName;

        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += (_, args) => OnRecieved(args);
        _channel.BasicConsume(consumer, _replyQueueName, true);
    }

    private void OnRecieved(BasicDeliverEventArgs args)
    {
        byte[] responseBytes = args.Body.ToArray();
        string responseStr = Encoding.UTF8.GetString(responseBytes);
        PersonModel response = JsonConvert.DeserializeObject<PersonModel>(responseStr) ?? new PersonModel();
        if (args.BasicProperties.CorrelationId == _correlationId)
            _responses.Add(response);
    }

    public PersonModel IsOver18(PersonModel personModel)
    {
        string serializedJson = JsonConvert.SerializeObject(personModel);
        byte[] serializedJsonBytes = Encoding.UTF8.GetBytes(serializedJson);
        _channel.BasicPublish("", _rpcQueueName, _properties, serializedJsonBytes);
        return _responses.Take();
    }

    
    void IDisposable.Dispose()
    {
        _channel.Close();
        _connection.Close();
    }
}
