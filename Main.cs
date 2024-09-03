using System;
using Microsoft.Extensions.Logging;
using Tower.Network;
using Tower.System;

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
});

var clients = new Client[Settings.NumClients];
for (var i = 0; i < Settings.NumClients; i++)
{
    clients[i] = new Client($"dummy_{i:D5}", loggerFactory);
}

await Task.WhenAll(clients.Select(client => client.Run()));