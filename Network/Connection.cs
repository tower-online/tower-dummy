using Google.FlatBuffers;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;
using System.Threading.Tasks.Dataflow;
using Tower.Network.Packet;

namespace Tower.Network;

public partial class Connection(ILogger logger, CancellationToken cancellationToken)
{
    private readonly TcpClient _socket = new();
    private NetworkStream? _stream;
    private readonly BufferBlock<ByteBuffer> _sendBufferBlock = new();
    private readonly CancellationToken _cancellationToken = cancellationToken;
    private readonly ILogger _logger = logger;

    public event Action Disconnected;

    public event Action<HeartBeat> HeartBeatEvent;
    public event Action<ClientJoinResponse> ClientJoinResponseEvent;
    public event Action<PlayerEnterZoneResponse> PlayerEnterZoneResponseEvent; 
    public event Action<EntityMovements> EntityMovementsEvent; 
    public event Action<EntitySpawns> EntitySpawnsEvent; 
    public event Action<EntityDespawn> EntityDespawnEvent;

    ~Connection()
    {
        Disconnect();
        _stream?.Dispose();
        _socket.Dispose();
    }

    public async Task Run()
    {
        // Receiving loop
        var receiveTask = Task.Run(async () =>
        {
            while (_socket.Connected)
            {
                var buffer = await ReceivePacketAsync();
                if (buffer is null) continue;
                
                HandlePacket(buffer);
            }
        }, _cancellationToken);

        // Sending loop
        var sendTask = Task.Run(async () =>
        {
            while (_socket.Connected)
            {
                try
                {
                    var buffer = await _sendBufferBlock.ReceiveAsync(_cancellationToken);
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
            await _socket.ConnectAsync(host, port, _cancellationToken);
            _stream = _socket.GetStream();
            _socket.NoDelay = true;

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error connecting: {}", ex);
            Disconnect();
        }

        return false;
    }

    public void Disconnect()
    {
        if (!_socket.Connected) return;

        _logger.LogInformation("Disconnecting...");
        _stream?.Close();
        _socket.Close();
        
        Disconnected.Invoke();
    }

    private async Task<ByteBuffer?> ReceivePacketAsync()
    {
        if (!_socket.Connected || _stream is null) return null;

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
            return null;
        }
    }

    public void SendPacket(ByteBuffer buffer)
    {
        if (!_socket.Connected) return;

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
                EntityMovementsEvent.Invoke(packetBase.PacketBase_AsEntityMovements());
                break;

            case PacketType.EntitySpawns:
                EntitySpawnsEvent.Invoke(packetBase.PacketBase_AsEntitySpawns());
                break;

            case PacketType.EntityDespawn:
                EntityDespawnEvent.Invoke(packetBase.PacketBase_AsEntityDespawn());
                break;

            case PacketType.ClientJoinResponse:
                ClientJoinResponseEvent.Invoke(packetBase.PacketBase_AsClientJoinResponse());
                break;
            
            case PacketType.PlayerEnterZoneResponse:
                PlayerEnterZoneResponseEvent.Invoke(packetBase.PacketBase_AsPlayerEnterZoneResponse());
                break;

            case PacketType.HeartBeat:
                HeartBeatEvent.Invoke(packetBase.PacketBase_AsHeartBeat());
                break;
        }
    }
}