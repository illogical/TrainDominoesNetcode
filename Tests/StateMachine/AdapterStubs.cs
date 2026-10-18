// Only adapter surfaces used by the actual state sources. These are not rule implementations.
using System;
using System.Collections.Generic;
using Assets.Scripts.Game.Integration;
namespace UnityEngine
{
    public class Object { }
    public static class Time { public static int frameCount => 42; }
    public static class Debug
    {
        public static readonly List<string> Messages = new List<string>();
        public static void Log(object message, Object context = null) => Messages.Add(message.ToString());
        public static void LogError(object message) => Messages.Add(message.ToString());
    }
}
public class GameSession : UnityEngine.Object
{
    public ClientSession Client { get; }
    public GameSession(Action<CommandRequest> send = null) { Client = new ClientSession(send ?? (_ => { })); }
}
public class GameplayManager { public readonly TestInput InputManager = new TestInput(); }
public class TestInput : IInteractionInput
{
    public event EventHandler<int> DominoClicked, ChallengeClicked;
    public event EventHandler<TrainIdDto> TrainChosen;
    public event EventHandler DrawButtonClicked, PassClicked, UndoClicked, EndTurnClicked, AnnounceClicked, ReadyButtonClicked, NewGameButtonClicked, RetryClicked, ResumeClicked;
    public void Draw() => DrawButtonClicked?.Invoke(this, EventArgs.Empty);
    public void Pass() => PassClicked?.Invoke(this, EventArgs.Empty);
    public void Undo() => UndoClicked?.Invoke(this, EventArgs.Empty);
    public void End() => EndTurnClicked?.Invoke(this, EventArgs.Empty);
    public void Announce() => AnnounceClicked?.Invoke(this, EventArgs.Empty);
    public void Challenge(int target = 1) => ChallengeClicked?.Invoke(this, target);
    public void Ready() => ReadyButtonClicked?.Invoke(this, EventArgs.Empty);
    public void NewGame() => NewGameButtonClicked?.Invoke(this, EventArgs.Empty);
    public void Retry() => RetryClicked?.Invoke(this, EventArgs.Empty);
    public void Resume() => ResumeClicked?.Invoke(this, EventArgs.Empty);
    public void Click(int tile = 1) => DominoClicked?.Invoke(this, tile);
    public void ChooseTrain(TrainIdDto train) => TrainChosen?.Invoke(this, train);
}
