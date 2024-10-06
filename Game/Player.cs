using System.Numerics;
using Tower.System;

namespace Tower.Game;

public class Player
{
    private enum State
    {
        Idle,
        Moving,
    }

    private static readonly Random Rand = new();
    private DateTime _lastTransition = DateTime.Now;
    private TimeSpan _transitionDelay = TimeSpan.FromSeconds(0);
    private State _state = State.Idle;

    public uint EntityId { get; init; }
    public string CharacterName { get; set; }
    public Vector3 TargetDirection { get; private set; }

    public Player(uint entityId)
    {
        EntityId = entityId;
    }

    public void Update()
    {
        if (DateTime.Now > _lastTransition + _transitionDelay)
        {
            _lastTransition = DateTime.Now;
            _transitionDelay = TimeSpan.FromSeconds(Rand.Next(3, 10));

            List<State> states = [State.Idle];
            if (Settings.MovementEnabled) states.Add(State.Moving);

            var newState = states[Rand.Next(states.Count)];
            Transitioned(newState);
        }
    }

    private void Transitioned(State newState)
    {
        State oldState = _state;
        _state = newState;

        TargetDirection = _state switch
        {
            State.Idle => Vector3.Zero,
            State.Moving => new Vector3((float)Rand.NextDouble() * 2.0f - 1.0f, 0, (float)Rand.NextDouble() * 2.0f - 1.0f),
            _ => TargetDirection
        };
        if (TargetDirection != Vector3.Zero) TargetDirection = Vector3.Normalize(TargetDirection);
    }
}