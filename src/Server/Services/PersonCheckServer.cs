using System.Text;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Server.Services;

public class PersonCheckServer
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ILogger<PersonCheckServer> _logger;
    private readonly string _queueName;

    public PersonCheckServer(IConfiguration configuration, ILogger<PersonCheckServer> logger)
    {
        _logger = logger;
        var connectionFactory = new ConnectionFactory()
        {
            HostName = configuration["RabbitMQ:HostName"],
            UserName = configuration["RabbitMQ:UserName"],
            Password = configuration["RabbitMQ:Password"]
        };
        _connection = connectionFactory.CreateConnection();
        _channel = _connection.CreateModel();
        _queueName = configuration["RabbitMQ:RPCQueueName"];
        _channel.QueueDeclare(_queueName, false, false, false, null);
        _channel.BasicQos(0, 1, false);


        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += (_, args) => OnRecieved(args);
        _channel.BasicConsume(_queueName, false, consumer);
    }
    private void OnRecieved(BasicDeliverEventArgs args)
    {
        PersonModel? response = null;
        try
        {
            string personJson = Encoding.UTF8.GetString(args.Body.ToArray());
            PersonModel person = JsonConvert.DeserializeObject<PersonModel>(personJson) ?? new PersonModel();
            response = person;
            response.IsOver18 = person.Age >= 18;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
        }
        finally
        {
            Thread.Sleep(30000);
            string responseJson = JsonConvert.SerializeObject(response);
            byte[] responseBytes = Encoding.UTF8.GetBytes(responseJson);
            _channel.BasicPublish("", args.BasicProperties.ReplyTo, args.BasicProperties, responseBytes);
            _channel.BasicAck(args.DeliveryTag, false);
        }
    }
}
