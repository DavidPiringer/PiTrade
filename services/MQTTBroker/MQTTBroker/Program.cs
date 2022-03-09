// Start a MQTT server.
using MQTTnet;
using MQTTnet.Server;
using System.Text;
// Configure MQTT server.
var optionsBuilder = new MqttServerOptionsBuilder();
var mqttServer = new MqttFactory().CreateMqttServer();
await mqttServer.StartAsync(optionsBuilder.Build());
mqttServer.UseApplicationMessageReceivedHandler(e =>
{
  Console.WriteLine("### RECEIVED APPLICATION MESSAGE ###");
  Console.WriteLine($"+ Topic = {e.ApplicationMessage.Topic}");
  Console.WriteLine($"+ Payload = {Encoding.UTF8.GetString(e.ApplicationMessage.Payload)}");
  Console.WriteLine($"+ QoS = {e.ApplicationMessage.QualityOfServiceLevel}");
  Console.WriteLine($"+ Retain = {e.ApplicationMessage.Retain}");
  Console.WriteLine();

});
Console.WriteLine(mqttServer.IsStarted);
Console.WriteLine("Press any key to exit.");
Console.ReadLine();
await mqttServer.StopAsync();