// Use secure TCP connection.
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using MQTTnet.Extensions.ManagedClient;
using System.Text;

// Setup and start a managed MQTT client.
var options = new ManagedMqttClientOptionsBuilder()
    .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
    .WithClientOptions(new MqttClientOptionsBuilder()
      .WithTcpServer("localhost", 1883)
      .WithCleanSession()
      .Build())
    .Build();

var mqttClient = new MqttFactory().CreateManagedMqttClient();
mqttClient.UseApplicationMessageReceivedHandler(e =>
{
  Console.WriteLine("### RECEIVED APPLICATION MESSAGE ###");
  Console.WriteLine($"+ Topic = {e.ApplicationMessage.Topic}");
  Console.WriteLine($"+ Payload = {Encoding.UTF8.GetString(e.ApplicationMessage.Payload)}");
  Console.WriteLine($"+ QoS = {e.ApplicationMessage.QualityOfServiceLevel}");
  Console.WriteLine($"+ Retain = {e.ApplicationMessage.Retain}");
  Console.WriteLine();
});
await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic("my/topic").Build());
await mqttClient.StartAsync(options);
// StartAsync returns immediately, as it starts a new thread using Task.Run, 
// and so the calling thread needs to wait.
while (true)
{
  Console.WriteLine(mqttClient.IsConnected);
  var message = new MqttApplicationMessageBuilder()
    .WithTopic("my/topic")
    .WithPayload("Hello World")
    .WithExactlyOnceQoS()
    .WithRetainFlag()
    .Build();

  await mqttClient.PublishAsync(message, CancellationToken.None);
  Console.ReadLine();
}