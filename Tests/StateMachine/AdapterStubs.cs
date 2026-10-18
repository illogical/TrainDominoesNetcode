// Only adapter surfaces used by the actual state sources. These are not rule implementations.
using System;
using System.Collections.Generic;

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
    public event EventHandler OnPlayerJoined;
    public int EngineRequests, JoinRequests, DrawRequests, EndRequests, SelectRequests, ReadyRequests;
    public void Join() => OnPlayerJoined?.Invoke(this, EventArgs.Empty);
    public void PlaceEngineServerRpc() => EngineRequests++;
    public void PlayerJoinedServerRpc() => JoinRequests++;
    public void DrawDominoesServerRpc() => DrawRequests++;
    public void EndTurnServerRpc() => EndRequests++;
    public void SelectDominoServerRpc(int id) => SelectRequests++;
    public void PlayerReadyForNextRoundServerRpc() => ReadyRequests++;
}

public class GameplayManager
{
    public readonly TestInput InputManager = new TestInput();
    public readonly TestSound SoundManager = new TestSound();
    public readonly TestTurn TurnManager = new TestTurn();
    public readonly TestRound RoundManager = new TestRound();
    public readonly TestTracker DominoTracker = new TestTracker();
    public event EventHandler AwaitTurn, PlayerTurnEnded, AllPlayersReadyForNextRound;
    public event EventHandler<ulong> PlayerTurnStarted, PlayerHasWonRound, PlayerHasWonGame;
    public event EventHandler<int> PlayerAddedDomino, PlayerReversedMove;
    public void StartTurn() => PlayerTurnStarted?.Invoke(this, 1);
    public void Added() => PlayerAddedDomino?.Invoke(this, 1);
    public void Undo() => PlayerReversedMove?.Invoke(this, 1);
    public void Await() => AwaitTurn?.Invoke(this, EventArgs.Empty);
    public void End() => PlayerTurnEnded?.Invoke(this, EventArgs.Empty);
    public void WinRound() => PlayerHasWonRound?.Invoke(this, 1);
    public void WinGame() => PlayerHasWonGame?.Invoke(this, 1);
    public void NextRound() => AllPlayersReadyForNextRound?.Invoke(this, EventArgs.Empty);
    public void ClientResetForNextRound() { }
    public void GameIsOver(ulong id, Dictionary<ulong, int> scores, Dictionary<ulong, int> totals) { }
    public void RoundIsOver(ulong id, Dictionary<ulong, int> scores, Dictionary<ulong, int> totals) { }
}

public class TestInput
{
    public event EventHandler DrawButtonClicked, EndTurnClicked, NewGameButtonClicked, ReadyButtonClicked;
    public event EventHandler<int> DominoClicked;
    public void Draw() => DrawButtonClicked?.Invoke(this, EventArgs.Empty);
    public void End() => EndTurnClicked?.Invoke(this, EventArgs.Empty);
    public void NewGame() => NewGameButtonClicked?.Invoke(this, EventArgs.Empty);
    public void Ready() => ReadyButtonClicked?.Invoke(this, EventArgs.Empty);
    public void Click() => DominoClicked?.Invoke(this, 1);
    public void SetDrawButtonEnabled(bool value) { }
    public void SetEndTurnButtonEnabled(bool value) { }
    public void SetRestartReadyButtonEnabled(bool value) { }
}
public class TestSound { public void PlayRandomClickSound() { } }
public class TestTracker { public void Reset() { } }
public class TestTurn
{
    public bool IsGroupTurn;
    public void ResetForNextRound() { }
    public ulong? GetRoundWinnerClientId() => 1;
    public ulong? GetGameWinnerClientId() => 1;
}
public class TestRound
{
    public Dictionary<ulong, int> GetRoundScores() => new Dictionary<ulong, int>();
    public Dictionary<ulong, int> GetPlayerTotalScores() => new Dictionary<ulong, int>();
}
