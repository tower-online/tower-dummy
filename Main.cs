using System;
using Microsoft.Extensions.Logging;
using Tower.Network;
using Tower.System;

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Debug);
});

List<Client> clients = [];
for (var i = 1; i < Settings.NumClients + 1; i++)
{
    clients.Add(new Client($"dummy_{i:D5}", loggerFactory));
}

await Task.WhenAll(clients.Select(client => client.Run()));