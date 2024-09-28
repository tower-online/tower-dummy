using System.Timers;
using Google.FlatBuffers;
using Microsoft.Extensions.Logging;
using Tower.Game;
using Tower.System;
using Tower.Network.Packet;
using Timer = System.Timers.Timer;

namespace Tower.Network;

public class Client
{
    private readonly string _username;
    private readonly Connection _connection;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private string? _authToken;
    private Player? _player;

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromMilliseconds(100);
    private readonly Timer _updateTimer = new(UpdateInterval);

    public Client(string username, ILoggerFactory loggerFactory)
    {
        _username = username;
        _logger = loggerFactory.CreateLogger(_username);
        _connection = new Connection(_logger, _cancellationTokenSource.Token);

        _connection.Disconnected += OnDisconnected;
        
        _connection.HeartBeatEvent += OnHeartBeat;
        _connection.ClientJoinResponseEvent += OnClientJoinResponse;
        _connection.PlayerEnterZoneResponseEvent += response =>
        {
            if (response.Result) return;
            _logger.LogWarning("PlayerEnterZone failed");
        };

        _updateTimer.Elapsed += OnUpdate;
        _updateTimer.AutoReset = true;
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

        var connected = await _connection.ConnectAsync(Settings.RemoteHost, Settings.RemotePort);
        if (!connected)
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
        
        _updateTimer.Enabled = true;
        await _connection.Run();
    }

    public void Stop()
    {
        _updateTimer.Enabled = false;
        _cancellationTokenSource.Cancel();
        _connection.Disconnect();
    }

    private void OnUpdate(object? sender, ElapsedEventArgs e)
    {
        _player?.Update();

        FlatBufferBuilder builder = new(128);
        PlayerMovement.StartPlayerMovement(builder);
        var targetDirectionOffset = Vector2.CreateVector2(builder, _player.TargetDirection.X, _player.TargetDirection.Y);
        PlayerMovement.AddTargetDirection(builder, targetDirectionOffset);
        var movementOffset = PlayerMovement.EndPlayerMovement(builder);
        var packetBaseOffset = PacketBase.CreatePacketBase(builder, PacketType.PlayerMovement, movementOffset.Value);
        PacketBase.FinishSizePrefixedPacketBaseBuffer(builder, packetBaseOffset);
        
        _connection.SendPacket(builder.DataBuffer);
    }

    private void OnDisconnected()
    {
        Stop();
    }

    private void OnHeartBeat(HeartBeat heartBeat)
    {
        _logger.LogDebug("beating");
        
        var builder = new FlatBufferBuilder(64);
        HeartBeat.StartHeartBeat(builder);
        var beat = HeartBeat.EndHeartBeat(builder);
        var packetBase = PacketBase.CreatePacketBase(builder, PacketType.HeartBeat, beat.Value);
        builder.FinishSizePrefixed(packetBase.Value);
        _connection.SendPacket(builder.DataBuffer);
    }

    private void OnClientJoinResponse(ClientJoinResponse response)
    {
        _logger.LogDebug("client join response");
        var spawn = response.Spawn.Value;
        var playerData = spawn.Data.Value;
        var location = response.CurrentLocation.Value;
        
        _player = new Player(spawn.EntityId)
        {
            CharacterName = playerData.Name
        };
        
        var builder = new FlatBufferBuilder(128);
        PlayerEnterZoneRequest.StartPlayerEnterZoneRequest(builder);
        PlayerEnterZoneRequest.AddLocation(builder, WorldLocation.CreateWorldLocation(builder, location.Floor, location.ZoneId));
        var request = PlayerEnterZoneRequest.EndPlayerEnterZoneRequest(builder);
        var packetBase = PacketBase.CreatePacketBase(builder, PacketType.PlayerEnterZoneRequest, request.Value);
        builder.FinishSizePrefixed(packetBase.Value);
        _connection.SendPacket(builder.DataBuffer);
    }
}