using Google.FlatBuffers;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Tower.Network.Packet;
using Tower.System;

namespace Tower.Network;

public partial class Connection
{
    private readonly TcpClient _client = new TcpClient();
    private NetworkStream _stream;
    private readonly BufferBlock<ByteBuffer> _sendBufferBlock = new();
    private readonly ILogger<Connection> _logger;

    public Connection(ILogger<Connection> logger)
    {
        _logger = logger;
    }

    ~Connection()
    {
        Disconnect();
        _stream?.Dispose();
        _client?.Dispose();
    }

    private void Run()
    {
        _ = Task.Run(async () =>
        {
            const string username = "tester_00001";

            var token = await RequestAuthToken(username);
            if (token.Length == 0) return;

            if (!await ConnectAsync(Settings.RemoteHost, 30000)) return;

            // Receiving loop
            _ = Task.Run(async () =>
            {
                while (_client.Connected)
                {
                    var buffer = await ReceivePacketAsync();
                    HandlePacket(buffer);
                }
            });

            // Sending loop
            _ = Task.Run(async () =>
            {
                while (_client.Connected)
                {
                    var buffer = await _sendBufferBlock.ReceiveAsync();
                    try
                    {
                        await _stream.WriteAsync(buffer.ToSizedArray());
                    }
                    catch (Exception)
                    {
                        Disconnect();
                    }
                }
            });

            // Send ClientJoinRequest with acquired token
            var builder = new FlatBufferBuilder(512);
            var request =
                ClientJoinRequest.CreateClientJoinRequest(builder,
                    ClientPlatform.TEST, builder.CreateString(username), builder.CreateString(token));
            var packetBase = PacketBase.CreatePacketBase(builder, PacketType.ClientJoinRequest, request.Value);
            builder.FinishSizePrefixed(packetBase.Value);

            SendPacket(builder.DataBuffer);
        });
    }

    public async Task<bool> ConnectAsync(string host, int port)
    {
        _logger.LogInformation("Connecting to {}:{}...", host, port);
        try
        {
            await _client.ConnectAsync(host, port);
            _stream = _client.GetStream();
            _client.NoDelay = true;

            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            _logger.LogError("port out of range: {}", port);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error connecting: {}", ex);
        }

        return false;
    }

    public void Disconnect()
    {
        if (!_client.Connected) return;

        _logger.LogInformation("Disconnecting...");
        _stream?.Close();
        _client?.Close();
    }

    private async Task<ByteBuffer> ReceivePacketAsync()
    {
        if (!_client.Connected) return new ByteBuffer(0);

        try
        {
            var headerBuffer = new byte[FlatBufferConstants.SizePrefixLength];
            await _stream.ReadExactlyAsync(headerBuffer, 0, headerBuffer.Length);

            var bodyBuffer = new byte[ByteBufferUtil.GetSizePrefix(new ByteBuffer(headerBuffer))];
            await _stream.ReadExactlyAsync(bodyBuffer, 0, bodyBuffer.Length);

            return new ByteBuffer(bodyBuffer);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            // GD.PrintErr($"[{nameof(Connection)}] Error reading: {ex}");
            Disconnect();
        }

        return new ByteBuffer(0);
    }

    private void SendPacket(ByteBuffer buffer)
    {
        if (!_client.Connected) return;

        _sendBufferBlock.Post(buffer);
    }

    private void HandlePacket(ByteBuffer buffer)
    {
        // if (!PacketBaseVerify.Verify(new Verifier(buffer), 0))
        // {
        //     GD.PrintErr($"[{nameof(Connection)}] Invalid packet base");
        //     Disconnect();
        //     return;
        // }

        var packetBase = PacketBase.GetRootAsPacketBase(buffer);
        switch (packetBase.PacketBaseType)
        {
            case PacketType.EntityMovements:
                HandleEntityMovements(packetBase.PacketBase_AsEntityMovements());
                break;

            case PacketType.EntitySpawns:
                HandleEntitySpawns(packetBase.PacketBase_AsEntitySpawns());
                break;

            case PacketType.EntityDespawn:
                HandleEntityDespawn(packetBase.PacketBase_AsEntityDespawn());
                break;

            case PacketType.PlayerSpawn:
                HandlePlayerSpawn(packetBase.PacketBase_AsPlayerSpawn());
                break;

            case PacketType.ClientJoinResponse:
                HandleClientJoinResponse(packetBase.PacketBase_AsClientJoinResponse());
                break;

            case PacketType.HeartBeat:
                HandleHeartBeat();
                break;
        }
    }

}