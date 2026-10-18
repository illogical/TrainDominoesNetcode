using System;
using System.Linq;
using Assets.Scripts.Game.States;

internal static class Program
{
    private static int passed;

    private static void Main()
    {
        Check("startup includes Pregame and nested Started entry", () =>
        {
            var session = new GameSession();
            using var ctx = new GameStateContext(session, new GameplayManager());
            Equal(nameof(PregameState), ctx.CurrentState.Name);
            session.Join();
            Equal(nameof(PlayerTurnStartedState), ctx.CurrentState.Name);
            Equal(1, session.EngineRequests);
            Equal(1, session.JoinRequests);
            var transitions = ctx.Diagnostics.History.Where(x => x.Kind == StateTraceKind.Transition).ToArray();
            Equal(3, transitions.Length);
            Equal(nameof(PregameState), transitions[0].ToState);
            Equal(nameof(GameStartedState), transitions[1].ToState);
            Equal(nameof(PlayerTurnStartedState), transitions[2].ToState);
            Equal("GameSession_OnPlayerJoined", transitions[1].Operation);
            True(ctx.Diagnostics.History.All(x => x.Kind == StateTraceKind.Transition));
        });
        Check("lazy cache, explicit fresh instance, and context isolation", () =>
        {
            using var ctx = new GameStateContext(new GameSession(), new GameplayManager());
            int creates = 0;
            Func<GameStateContext, ProbeState> factory = c => { creates++; return new ProbeState(c); };
            ctx.SwitchState(factory);
            var first = (ProbeState)ctx.CurrentState;
            ctx.SwitchState(factory);
            Equal(1, creates);
            Equal(2, first.Entries);
            Equal(1, first.Exits);
            ctx.SwitchState(factory, reuse: false);
            Equal(2, creates);
            True(!ReferenceEquals(first, ctx.CurrentState));
            ctx.SwitchState(factory);
            True(ReferenceEquals(first, ctx.CurrentState));
            using var other = new GameStateContext(new GameSession(), new GameplayManager());
            other.SwitchState(factory);
            Equal(3, creates);
            True(!ReferenceEquals(first, other.CurrentState));
        });
        Check("self reentry and undo do not duplicate input subscriptions", () =>
        {
            var session = new GameSession();
            var manager = new GameplayManager();
            using var ctx = new GameStateContext(session, manager);
            session.Join();
            manager.StartTurn();
            manager.StartTurn();
            manager.InputManager.Click();
            Equal(1, session.SelectRequests);
            manager.Added();
            Equal(nameof(PlayerMadeMoveState), ctx.CurrentState.Name);
            manager.Undo();
            manager.InputManager.Click();
            Equal(2, session.SelectRequests);
            manager.Await();
            manager.InputManager.Click();
            Equal(2, session.SelectRequests);
            manager.WinRound();
            manager.InputManager.Ready();
            Equal(1, session.ReadyRequests);
            manager.NextRound();
            Equal(2, session.EngineRequests);
            manager.WinGame();
            manager.InputManager.NewGame();
            Equal(3, session.EngineRequests);
        });
        Check("legacy group placement remains in active state", () =>
        {
            var manager = new GameplayManager();
            manager.TurnManager.IsGroupTurn = true;
            using var ctx = new GameStateContext(new GameSession(), manager);
            ctx.SwitchState(c => new PlayerTurnStartedState(c));
            manager.Added();
            Equal(nameof(PlayerTurnStartedState), ctx.CurrentState.Name);
        });
        Check("verbose operation nesting, history cap, and off switch", () =>
        {
            using var ctx = new GameStateContext(new GameSession(), new GameplayManager());
            ctx.Diagnostics.Level = StateTraceLevel.Verbose;
            using (ctx.Diagnostics.TraceExecution(ctx.CurrentState, "outer"))
            {
                Equal("outer", ctx.Diagnostics.ExecutingOperation);
                ctx.SwitchState(c => new ProbeState(c));
                Equal("outer", ctx.Diagnostics.ExecutingOperation);
            }
            Equal(null, ctx.Diagnostics.ExecutingOperation);
            for (int i = 0; i < 150; i++) ctx.SwitchState(c => new ProbeState(c));
            Equal(128, ctx.Diagnostics.History.Count);
            var history = ctx.Diagnostics.History;
            True(history.Zip(history.Skip(1), (a, b) => a.Sequence < b.Sequence).All(x => x));
            True(history.All(x => x.Frame == 42 && x.TimestampUtc.Kind == DateTimeKind.Utc));
            int logCount = UnityEngine.Debug.Messages.Count;
            ctx.Diagnostics.Level = StateTraceLevel.Off;
            ctx.SwitchState(c => new GameJoinedState(c));
            Equal(logCount, UnityEngine.Debug.Messages.Count);
            Equal(history.Last().Sequence, ctx.Diagnostics.History.Last().Sequence);
            Equal(nameof(GameJoinedState), ctx.CurrentState.Name);
        });
        Check("handler tracing includes events without transitions", () =>
        {
            var session = new GameSession();
            var manager = new GameplayManager();
            using var ctx = new GameStateContext(session, manager);
            ctx.Diagnostics.Level = StateTraceLevel.Verbose;
            session.Join();
            manager.InputManager.Draw();
            Equal(1, session.DrawRequests);
            True(ctx.Diagnostics.History.Any(x => x.Operation == "InputManager_DrawButtonClicked"));
            Equal(null, ctx.Diagnostics.ExecutingOperation);
        });
        Check("exceptions retain failure evidence and unwind operation", () =>
        {
            using var ctx = new GameStateContext(new GameSession(), new GameplayManager());
            ctx.Diagnostics.Level = StateTraceLevel.Verbose;
            Throws<InvalidOperationException>(() => ctx.SwitchState(c => new FailingState(c)));
            Equal(null, ctx.Diagnostics.ExecutingOperation);
            True(ctx.Diagnostics.History.Any(x => x.Kind == StateTraceKind.Fault));
        });
        Check("dispose detaches active input and is idempotent", () =>
        {
            var session = new GameSession();
            var manager = new GameplayManager();
            var ctx = new GameStateContext(session, manager);
            session.Join();
            ctx.Dispose();
            ctx.Dispose();
            manager.InputManager.Draw();
            manager.InputManager.Click();
            session.Join();
            Equal(0, session.DrawRequests);
            Equal(0, session.SelectRequests);
            Equal(1, session.EngineRequests);
            Throws<ObjectDisposedException>(() => ctx.SwitchState(c => new ProbeState(c)));
        });
        Check("invalid factories leave the active state untouched", () =>
        {
            using var ctx = new GameStateContext(new GameSession(), new GameplayManager());
            using var other = new GameStateContext(new GameSession(), new GameplayManager());
            var initial = ctx.CurrentState;
            Throws<ArgumentNullException>(() => ctx.SwitchState<ProbeState>(null));
            Throws<InvalidOperationException>(() => ctx.SwitchState<ProbeState>(c => null));
            Throws<InvalidOperationException>(() => ctx.SwitchState(c => new ProbeState(other)));
            True(ReferenceEquals(initial, ctx.CurrentState));
        });
        Console.WriteLine($"PASS: {passed} state-machine checks (adapter stubs; no Unity/Netcode play verification).");
    }

    private static void Check(string name, Action action)
    {
        action();
        passed++;
        Console.WriteLine($"PASS: {name}");
    }
    private static void True(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
    private static void Equal<T>(T expected, T actual) => True(Equals(expected, actual));
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new Exception($"Expected {typeof(T).Name}");
    }

    private class ProbeState : GameStateBase
    {
        public int Entries, Exits;
        public ProbeState(GameStateContext context) : base(context) { }
        public override void EnterState() => Entries++;
        public override void LeaveState() => Exits++;
    }
    private sealed class FailingState : ProbeState
    {
        public FailingState(GameStateContext context) : base(context) { }
        public override void EnterState() => throw new InvalidOperationException("test entry failure");
    }
}
