using Google.FlatBuffers;
using Tower.Network.Packet;

namespace Tower.Network;

public partial class Connection
{
    
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
            GD.PrintErr($"[{nameof(Connection)}] [ClientJoinResponse] Failed");

            //TODO: Signal fail or retry?
            Disconnect();
            return;
        }

        GD.Print($"[{nameof(Connection)}] [ClientJoinResponse] OK");
    }

    #endregion

    #region Entity Packet Handlers

    private void HandleEntityMovements(EntityMovements movements)
    {
        var length = movements.MovementsLength;
        var entityIds = new int[length];
        var targetDirections = new Godot.Vector2[length];
        var targetPositions = new Godot.Vector2[length];

        for (var i = 0; i < length; i++)
        {
            if (!movements.Movements(i).HasValue)
            {
                GD.PrintErr($"[{nameof(Connection)}] [{nameof(HandleEntityMovements)}] Invalid array");
                return;
            }

            var movement = movements.Movements(i).Value;
            var targetDirection = movement.TargetDirection;
            var targetPosition = movement.TargetPosition;

            entityIds[i] = (int)movement.EntityId;
            targetDirections[i] = new Godot.Vector2(targetDirection.X, targetDirection.Y);
            targetPositions[i] = new Godot.Vector2(targetPosition.X, targetPosition.Y);
        }

        CallDeferred(GodotObject.MethodName.EmitSignal, SignalName.SEntityMovements,
            entityIds, targetDirections, targetPositions);
    }

    private void HandleEntitySpawns(EntitySpawns spawns)
    {
        var length = spawns.SpawnsLength;
        var entityIds = new int[length];
        var entityTypes = new int[length];
        var positions = new Godot.Vector2[length];
        var rotations = new float[length];

        for (var i = 0; i < length; i++)
        {
            if (!spawns.Spawns(i).HasValue)
            {
                GD.PrintErr($"[{nameof(Connection)}] [{nameof(HandleEntitySpawns)}] Invalid array");
                return;
            }

            var spawn = spawns.Spawns(i).Value;
            var position = spawn.Position;

            entityIds[i] = (int)spawn.EntityId;
            entityTypes[i] = (int)spawn.EntityType;
            positions[i] = new Godot.Vector2(position.X, position.Y);
            rotations[i] = spawn.Rotation;
        }

        CallDeferred(GodotObject.MethodName.EmitSignal, SignalName.SEntitySpawns,
            entityIds, entityTypes, positions, rotations);
    }

    private void HandleEntityDespawn(EntityDespawn despawn)
    {
    }

    #endregion

    #region Player Packet Handlers

    private void HandlePlayerSpawn(PlayerSpawn spawn)
    {
        var position = new Godot.Vector2();
        if (spawn.Position.HasValue)
        {
            var pos = spawn.Position.Value;
            position.X = pos.X;
            position.Y = pos.Y;
        }
        
        CallDeferred(GodotObject.MethodName.EmitSignal, SignalName.SPlayerSpawn, (int)spawn.EntityId, (int)spawn.EntityType, position, spawn.Rotation);
    }

    #endregion

    #region Player Action Handlers

    public void HandlePlayerMovement(Godot.Vector2 targetDirection)
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