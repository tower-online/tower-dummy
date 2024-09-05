using Google.FlatBuffers;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;
using System.Threading.Tasks.Dataflow;
using Tower.Network.Packet;

namespace Tower.Network;

public partial class Connection(ILogger logger, CancellationToken cancellationToken)
{
    private readonly TcpClient _client = new();
    private NetworkStream? _stream;
    private readonly BufferBlock<ByteBuffer> _sendBufferBlock = new();
    private readonly CancellationToken _cancellationToken = cancellationToken;
    private readonly ILogger _logger = logger;

    ~Connection()
    {
        Disconnect();
        _stream?.Dispose();
        _client.Dispose();
    }

    public async Task Run()
    {
        // Receiving loop
        var receiveTask = Task.Run(async () =>
        {
            while (_client.Connected)
            {
                var buffer = await ReceivePacketAsync();
                HandlePacket(buffer);
            }
        }, _cancellationToken);

        // Sending loop
        var sendTask = Task.Run(async () =>
        {
            while (_client.Connected)
            {
                var buffer = await _sendBufferBlock.ReceiveAsync(_cancellationToken);
                try
                {
                    if (_stream is null) break;
                    await _stream.WriteAsync(buffer.ToSizedArray(), _cancellationToken);
                }
                catch (Exception)
                {
                    Disconnect();
                }
            }
        }, _cancellationToken);

        await Task.WhenAll(receiveTask, sendTask);
    }

    public async Task<bool> ConnectAsync(string host, int port)
    {
        _logger.LogInformation("Connecting to {}:{}...", host, port);
        try
        {
            await _client.ConnectAsync(host, port, _cancellationToken);
            _stream = _client.GetStream();
            _client.NoDelay = true;

            return true;
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
        _client.Close();
    }

    private async Task<ByteBuffer> ReceivePacketAsync()
    {
        if (!_client.Connected || _stream is null) return new ByteBuffer(0);

        try
        {
            var headerBuffer = new byte[FlatBufferConstants.SizePrefixLength];
            await _stream.ReadExactlyAsync(headerBuffer, 0, headerBuffer.Length, _cancellationToken);

            var bodyBuffer = new byte[ByteBufferUtil.GetSizePrefix(new ByteBuffer(headerBuffer))];
            await _stream.ReadExactlyAsync(bodyBuffer, 0, bodyBuffer.Length, _cancellationToken);

            return new ByteBuffer(bodyBuffer);
        }
        catch (Exception)
        {
            Disconnect();
        }

        return new ByteBuffer(0);
    }

    public void SendPacket(ByteBuffer buffer)
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