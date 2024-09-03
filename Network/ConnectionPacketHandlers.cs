using Google.FlatBuffers;
using Microsoft.Extensions.Logging;
using Tower.Network.Packet;
using Vector2 = System.Numerics.Vector2;

namespace Tower.Network;

public partial class Connection
{
    public event EventHandler<EntityMovementsEventArgs> EntityMovementsEventHandler;
    public event EventHandler<EntitySpawnsEventArgs> EntitySpawnsEventHandler;
    public event EventHandler<PlayerSpawnEventArgs> PlayerSpawnEventHandler;


    #region Client Packet Handlers

    private void HandleHeartBeat()
    {
        var builder = new FlatBufferBuilder(64);

        HeartBeat.StartHeartBeat(builder);
        var heartBeat = HeartBeat.EndHeartBeat(builder);
        var packetBase = PacketBase.CreatePacketBase(builder, PacketType.HeartBeat, heartBeat.Value);
        builder.FinishSizePrefixed(packetBase.Value);

        SendPacket(builder.DataBuffer);
    }

    private void HandleClientJoinResponse(ClientJoinResponse response)
    {
        if (response.Result != ClientJoinResult.OK)
        {
            _logger.LogError("[ClientJoinResponse] Failed");

            //TODO: Signal fail or retry?
            Disconnect();
            return;
        }

        _logger.LogInformation("[ClientJoinResponse] OK");
    }

    #endregion

    #region Entity Packet Handlers

    private void HandleEntityMovements(EntityMovements movements)
    {
        var length = movements.MovementsLength;
        var entityIds = new int[length];
        var targetDirections = new Vector2[length];
        var targetPositions = new Vector2[length];

        for (var i = 0; i < length; i++)
        {
            if (!movements.Movements(i).HasValue)
            {
                _logger.LogError("[{}] Invalid array", nameof(HandleEntityMovements));
                return;
            }

            var movement = movements.Movements(i).Value;
            var targetDirection = movement.TargetDirection;
            var targetPosition = movement.TargetPosition;

            entityIds[i] = (int)movement.EntityId;
            targetDirections[i] = new Vector2(targetDirection.X, targetDirection.Y);
            targetPositions[i] = new Vector2(targetPosition.X, targetPosition.Y);
        }

        EntityMovementsEventHandler.Invoke(this,
            new EntityMovementsEventArgs(entityIds, targetDirections, targetPositions));
    }

    private void HandleEntitySpawns(EntitySpawns spawns)
    {
        var length = spawns.SpawnsLength;
        var entityIds = new int[length];
        var entityTypes = new int[length];
        var positions = new Vector2[length];
        var rotations = new float[length];

        for (var i = 0; i < length; i++)
        {
            if (!spawns.Spawns(i).HasValue)
            {
                _logger.LogError("[{}] Invalid array", nameof(HandleEntitySpawns));
                return;
            }

            var spawn = spawns.Spawns(i).Value;
            var position = spawn.Position;

            entityIds[i] = (int)spawn.EntityId;
            entityTypes[i] = (int)spawn.EntityType;
            positions[i] = new Vector2(position.X, position.Y);
            rotations[i] = spawn.Rotation;
        }

        EntitySpawnsEventHandler.Invoke(this, new EntitySpawnsEventArgs(entityIds, entityTypes, positions, rotations));
    }

    private void HandleEntityDespawn(EntityDespawn despawn)
    {
    }

    #endregion

    #region Player Packet Handlers

    private void HandlePlayerSpawn(PlayerSpawn spawn)
    {
        var position = new Vector2();
        if (spawn.Position.HasValue)
        {
            var pos = spawn.Position.Value;
            position.X = pos.X;
            position.Y = pos.Y;
        }

        PlayerSpawnEventHandler.Invoke(this,
            new PlayerSpawnEventArgs((int)spawn.EntityId, (int)spawn.EntityType, position, spawn.Rotation));    
    }

    #endregion

    #region Player Action Handlers

    public void HandlePlayerMovement(Vector2 targetDirection)
    {
        var builder = new FlatBufferBuilder(128);
        PlayerMovement.StartPlayerMovement(builder);
        PlayerMovement.AddTargetDirection(builder,
            Packet.Vector2.CreateVector2(builder, targetDirection.X, targetDirection.Y));
        var movement = PlayerMovement.EndPlayerMovement(builder);
        var packetBase = PacketBase.CreatePacketBase(builder, PacketType.PlayerMovement, movement.Value);
        builder.FinishSizePrefixed(packetBase.Value);

        SendPacket(builder.DataBuffer);
    }

    #endregion
}

public class EntityMovementsEventArgs(int[] entityIds, Vector2[] targetDirections, Vector2[] targetPositions)
    : EventArgs
{
    public int[] EntityIds { get; } = entityIds;
    public Vector2[] TargetDirections { get; } = targetDirections;
    public Vector2[] TargetPositions { get; } = targetPositions;
}

public class EntitySpawnsEventArgs(int[] entityIds, int[] entityTypes, Vector2[] positions, float[] rotations)
    : EventArgs
{
    public int[] EntityIds { get; } = entityIds;
    public int[] EntityTypes { get; } = entityTypes;
    public Vector2[] TargetPositions { get; } = positions;
    public float[] Rotations { get; } = rotations;
}

public class PlayerSpawnEventArgs(int entityId, int entityType, Vector2 position, float rotation) : EventArgs
{
    int EntityId { get; } = entityId;
    int EntityType { get; } = entityType;
    Vector2 Position { get; } = position;
    float Rotation { get; } = rotation;
}