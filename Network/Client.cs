using Google.FlatBuffers;
using Microsoft.Extensions.Logging;
using Tower.Game;
using Tower.System;
using Tower.Network.Packet;

namespace Tower.Network;

public class Client
{
    private readonly string _username;
    private readonly Connection _connection;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private string? _authToken;
    private Player? _player;

    public Client(string username, ILoggerFactory loggerFactory)
    {
        _username = username;
        _logger = loggerFactory.CreateLogger(_username);
        _connection = new Connection(_logger, _cancellationTokenSource.Token);

        _connection.PlayerSpawnEventHandler += OnPlayerSpawn;
    }

    public async Task Run()
    {
        _logger.LogInformation("Running...");

        _authToken = await _connection.RequestAuthToken(_username);
        var characters = await _connection.RequestCharacters(_username, _authToken);
        if (characters is null || characters.Count == 0)
        {
            _logger.LogError("Failed to request characters");
            Stop();
            return;
        }

        _logger.LogInformation("Character: {}", characters[0]);

        if (!await _connection.ConnectAsync(Settings.RemoteHost, Settings.RemotePort))
        {
            Stop();
            return;
        }

        // Send ClientJoinRequest with acquired token
        var characterName = characters[0];
        var builder = new FlatBufferBuilder(512);
        var request =
            ClientJoinRequest.CreateClientJoinRequest(builder,
                builder.CreateString(_username),
                builder.CreateString(characterName),
                builder.CreateString(_authToken));
        var packetBase = PacketBase.CreatePacketBase(builder, PacketType.ClientJoinRequest, request.Value);
        builder.FinishSizePrefixed(packetBase.Value);
        _connection.SendPacket(builder.DataBuffer);

        await _connection.Run();
    }

    public void Stop()
    {
        _cancellationTokenSource.Cancel();
        _connection.Disconnect();
    }

    private void OnPlayerSpawn(object? _, PlayerSpawnEventArgs args)
    {
        _logger.LogInformation("OnPlayerSpawn");
    }
}