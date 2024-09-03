using Google.FlatBuffers;
using Microsoft.Extensions.Logging;
using Tower.System;
using Tower.Network.Packet;

namespace Tower.Network;

public class Client
{
    private readonly string _username;
    private readonly Connection _connection;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public Client(string username, ILoggerFactory loggerFactory)
    {
        _username = username;
        _logger = loggerFactory.CreateLogger(_username);
        _connection = new Connection(_logger, _cancellationTokenSource.Token);
        
        // _connection.PlayerSpawnEventHandler += 
    }

    public async Task Run()
    {
        _logger.LogInformation("Running...");
        
        var token = await _connection.RequestAuthToken(_username);
        if (token is null || await _connection.ConnectAsync(Settings.RemoteHost, Settings.RemotePort))
        {
            Stop();
            return;
        }
        
        // Send ClientJoinRequest with acquired token
        var builder = new FlatBufferBuilder(512);
        var request =
            ClientJoinRequest.CreateClientJoinRequest(builder,
                ClientPlatform.TEST, builder.CreateString(_username), builder.CreateString(token));
        var packetBase = PacketBase.CreatePacketBase(builder, PacketType.ClientJoinRequest, request.Value);
        builder.FinishSizePrefixed(packetBase.Value);

        _connection.SendPacket(builder.DataBuffer);
        
        _connection.Run();
    }

    public void Stop()
    {
        _cancellationTokenSource.Cancel();
        _connection.Disconnect();
    }
}