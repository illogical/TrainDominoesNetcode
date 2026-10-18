using System;
using System.Linq;
using System.Collections.Generic;
using Assets.Scripts.Game.Integration;
using TrainDominoesRulesEngine;
using Assets.Scripts.Game.States;

internal static class Program
{
    private static int passed;

    private static void Main()
    {
        Check("Pregame startup sends nothing; setup waits for local presentation", () =>
        {
            using var f = new Fixture();
            Equal(nameof(PregameState), f.State);
            Equal(0, f.Sent.Count);
            f.Install(View());
            Equal(nameof(RoundInitializingState), f.State);
            Equal(0, f.Sent.Count);
            f.Present();
            Equal(nameof(PlayerActingState), f.State);
            var initial = f.Context.CurrentState;
            int transitions = f.Context.Diagnostics.History.Count;
            f.Install(View(revision: 2));
            True(ReferenceEquals(initial, f.Context.CurrentState));
            Equal(transitions, f.Context.Diagnostics.History.Count);
            f.Present();
            f.Install(View(active: 1, revision: 3));
            Equal(nameof(PlayerAwaitingTurnState), f.State);
        });
        Check("setup retry balances observing and retry subscriptions", () =>
        {
            using var f = new Fixture();
            f.Install(View(phase: MatchPhase.AwaitingDeck));
            f.Context.SwitchState(c => new RoundInitializingState(c));
            f.Input.Retry();
            Equal(1, f.Sent.Count);
            Equal(CommandKind.Ready, f.Sent[0].Kind);
            Equal(nameof(PlayerActionPendingState), f.State);
            f.Client.Receive(new ProjectionPacket { View = f.Client.View, Outcome = Outcome(f.Client.Pending, false) });
            Equal(nameof(RoundInitializingState), f.State);
            f.Input.Retry(); Equal(2, f.Sent.Count);
            f.Client.Receive(new ProjectionPacket { View = View(revision: 2), Outcome = Outcome(f.Client.Pending) });
            f.Present();
            f.Input.Retry(); Equal(2, f.Sent.Count);
        });
        Check("Pending is installed before an immediate host outcome", () =>
        {
            Fixture fixture = null;
            using var f = fixture = new Fixture(request =>
            {
                Equal(nameof(PlayerActionPendingState), fixture.State);
                True(ReferenceEquals(request, fixture.Client.Pending));
                fixture.Client.Receive(new ProjectionPacket { View = fixture.Client.View, Outcome = Outcome(request) });
            });
            f.Activate(); f.Input.Draw();
            Equal(1, f.Sent.Count);
            Equal(nameof(PlayerActingState), f.State);
            Equal(null, f.Client.Pending);
        });
        Check("rejection and explicit self reentry never multiply input handlers", () =>
        {
            using var f = new Fixture(); f.Activate();
            f.Context.SwitchState(c => new PlayerActingState(c));
            f.Context.SwitchState(c => new PlayerActingState(c));
            for (int i = 0; i < 3; i++)
            {
                f.Input.Draw();
                Equal(i + 1, f.Sent.Count);
                Equal(nameof(PlayerActionPendingState), f.State);
                f.Input.Draw(); Equal(i + 1, f.Sent.Count);
                f.Client.Receive(new ProjectionPacket { View = f.Client.View, Outcome = Outcome(f.Client.Pending, false) });
                Equal(nameof(PlayerActingState), f.State);
            }
        });
        Check("unchanged revision Ready settles and duplicate outcomes are inert", () =>
        {
            using var f = new Fixture();
            f.Install(View(phase: MatchPhase.AwaitingReady));
            f.Input.Ready(); var request = f.Client.Pending;
            Equal(nameof(PlayerActionPendingState), f.State);
            var packet = new ProjectionPacket { View = f.Client.View, Outcome = Outcome(request) };
            f.Client.Receive(packet); f.Client.Receive(packet);
            Equal(nameof(PregameState), f.State); Equal(null, f.Client.Pending);
            Equal(1, f.Sent.Count);
        });
        Check("waiting announcement and challenge bypass motion but board actions do not", () =>
        {
            using var f = new Fixture(); f.Activate();
            f.Install(View(active: 1, revision: 2));
            True(f.Client.IsPresentationBusy);
            Equal(nameof(PlayerAwaitingTurnState), f.State);
            f.Input.Draw(); Equal(0, f.Sent.Count);
            f.Input.Announce(); Equal(CommandKind.Announce, f.Sent.Last().Kind);
            f.Client.Receive(new ProjectionPacket { Outcome = Outcome(f.Client.Pending) });
            f.Input.Challenge(1); Equal(CommandKind.Challenge, f.Sent.Last().Kind);
            Equal(1, f.Sent.Last().Target);
        });
        Check("setup presentation does not delay announcement commands", () =>
        {
            using var f = new Fixture(); f.Install(View(active: 1));
            Equal(nameof(RoundInitializingState), f.State);
            f.Input.Announce(); Equal(CommandKind.Announce, f.Sent.Last().Kind);
            f.Client.Receive(new ProjectionPacket { Outcome = Outcome(f.Client.Pending) });
            f.Input.Challenge(1); Equal(CommandKind.Challenge, f.Sent.Last().Kind);
        });
        Check("terminal phase preempts old request and late outcome cannot revive it", () =>
        {
            using var f = new Fixture(); f.Activate(); f.Input.Draw();
            var old = f.Client.Pending;
            f.Install(View(phase: MatchPhase.RoundOver, revision: 2));
            Equal(nameof(RoundOverState), f.State); Equal(null, f.Client.Pending);
            f.Client.Receive(new ProjectionPacket { View = View(), Outcome = Outcome(old) });
            Equal(nameof(RoundOverState), f.State); Equal(2L, f.Client.View.Token.Revision);
            f.Input.Ready(); Equal(nameof(PlayerActionPendingState), f.State);
            f.Client.Receive(new ProjectionPacket { Outcome = Outcome(f.Client.Pending) });
            Equal(nameof(RoundOverState), f.State);
            f.Install(View(phase: MatchPhase.MatchOver, revision: 3));
            Equal(nameof(GameOverState), f.State);
        });
        Check("uncertain retry preserves identity and old outcomes cannot replace a newer view", () =>
        {
            using var f = new Fixture(); f.Activate(); f.Input.Draw();
            var request = f.Client.Pending;
            f.Client.MarkUncertain(); True(f.Client.IsUncertain);
            f.Input.Retry(); True(ReferenceEquals(request, f.Sent.Last()));
            f.Install(View(revision: 3)); Equal(nameof(PlayerActionPendingState), f.State);
            f.Client.Receive(new ProjectionPacket { View = View(), Outcome = Outcome(request) });
            Equal(3L, f.Client.View.Token.Revision); Equal(null, f.Client.Pending);
            Equal(nameof(PlayerActingState), f.State);
        });
        Check("suspension requires synchronized recovery and ignores old completion", () =>
        {
            using var f = new Fixture(); f.Activate();
            long generation = f.Client.PresentationGeneration;
            f.Client.SetAvailable(false); Equal(nameof(PlayerSessionSuspendedState), f.State);
            f.Client.CompletePresentation(generation); Equal(nameof(PlayerSessionSuspendedState), f.State);
            f.Install(View(revision: 2)); Equal(nameof(PlayerSessionSuspendedState), f.State);
            f.Client.Receive(new ProjectionPacket { View = View(revision: 2) }, synchronization: true);
            Equal(nameof(PlayerActingState), f.State); True(f.Client.IsPresentationBusy);
            f.Present(); True(f.Client.BoardEnabled);
        });
        Check("selection stays local and legal placement carries explicit target", () =>
        {
            using var f = new Fixture();
            var view = View(); var train = new TrainIdDto { IsShared = true };
            view.LegalPlacements = new[] { new LegalPlacementDto { DominoId = 1, Train = train } };
            f.Install(view); f.Present(); f.Input.Click(1); Equal(0, f.Sent.Count);
            f.Input.ChooseTrain(new TrainIdDto { Owner = 0 }); Equal(0, f.Sent.Count);
            f.Input.ChooseTrain(train); Equal(1, f.Sent.Count);
            Equal(CommandKind.Place, f.Sent[0].Kind); Equal(1, f.Sent[0].DominoId);
            True(f.Sent[0].Train.IsShared); Equal(nameof(PlayerActionPendingState), f.State);
        });
        Check("new round initializes afresh and disposed motion cannot release it", () =>
        {
            using var f = new Fixture(); f.Activate();
            long old = f.Client.PresentationGeneration;
            var next = View(revision: 5); next.Token.RoundIndex = 1;
            f.Install(next); Equal(nameof(RoundInitializingState), f.State);
            f.Client.CompletePresentation(old); Equal(nameof(RoundInitializingState), f.State);
            True(f.Client.IsPresentationBusy); f.Present(); Equal(nameof(PlayerActingState), f.State);
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
            ctx.SwitchState(c => new NeutralState(c));
            Equal(logCount, UnityEngine.Debug.Messages.Count);
            Equal(history.Last().Sequence, ctx.Diagnostics.History.Last().Sequence);
            Equal(nameof(NeutralState), ctx.CurrentState.Name);
        });
        Check("handler tracing includes local events without transitions", () =>
        {
            using var f = new Fixture(); f.Activate();
            f.Context.Diagnostics.Level = StateTraceLevel.Verbose;
            f.Input.Click();
            Equal(1, f.Client.SelectedTile);
            Equal(0, f.Sent.Count);
            True(f.Context.Diagnostics.History.Any(x => x.Operation == "TileClicked"));
            Equal(null, f.Context.Diagnostics.ExecutingOperation);
        });
        Check("exceptions retain failure evidence and unwind operation", () =>
        {
            using var ctx = new GameStateContext(new GameSession(), new GameplayManager());
            ctx.Diagnostics.Level = StateTraceLevel.Verbose;
            Throws<InvalidOperationException>(() => ctx.SwitchState(c => new FailingState(c)));
            Equal(null, ctx.Diagnostics.ExecutingOperation);
            True(ctx.Diagnostics.History.Any(x => x.Kind == StateTraceKind.Fault));
        });
        Check("coordinator and context disposal detach subscriptions and are idempotent", () =>
        {
            var f = new Fixture(); f.Activate();
            f.Coordinator.Dispose(); f.Coordinator.Dispose();
            f.Context.Dispose(); f.Context.Dispose();
            f.Input.Draw(); f.Input.Click(); f.Input.Announce(); f.Input.Ready();
            f.Install(View(phase: MatchPhase.RoundOver, revision: 2));
            f.Present(); f.Client.SetAvailable(false);
            Equal(0, f.Sent.Count); Equal(null, f.Context.CurrentState);
            Throws<ObjectDisposedException>(() => f.Context.SwitchState(c => new ProbeState(c)));
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

    private static PlayerViewDto View(int active = 0, long revision = 1, MatchPhase phase = MatchPhase.Playing)
        => new PlayerViewDto {
            Token = new TokenDto { MatchId = "test-match", RoundIndex = 0, TurnNumber = 1, Revision = revision },
            Player = 0, ActivePlayer = active, Phase = phase, EngineId = 90,
            Hand = new[] { 1, 2 }, Trains = Array.Empty<TrainViewDto>(), LegalPlacements = Array.Empty<LegalPlacementDto>(),
            Allowed = new AllowedActionsDto { CanDraw = true, CanReady = true, CanAnnounce = true, ChallengeTargets = new[] { 1 } }
        };
    private static CommandOutcome Outcome(CommandRequest request, bool accepted = true)
        => new CommandOutcome { RequestId = request.RequestId, Accepted = accepted, Error = accepted ? default : RuleError.StalePosition };
    private sealed class Fixture : IDisposable
    {
        public readonly List<CommandRequest> Sent = new List<CommandRequest>();
        public readonly GameSession Session;
        public readonly GameplayManager Manager = new GameplayManager();
        public readonly GameStateContext Context;
        public readonly PlayerStateCoordinator Coordinator;
        public ClientSession Client => Session.Client;
        public TestInput Input => Manager.InputManager;
        public string State => Context.CurrentState.Name;
        public Fixture(Action<CommandRequest> send = null)
        {
            Session = new GameSession(request => { Sent.Add(request); send?.Invoke(request); });
            Context = new GameStateContext(Session, Manager);
            Coordinator = new PlayerStateCoordinator(Context); Coordinator.Start(); Coordinator.Start();
        }
        public void Install(PlayerViewDto view) => Client.Receive(new ProjectionPacket { View = view });
        public void Present() => Client.CompletePresentation(Client.PresentationGeneration);
        public void Activate() { Install(View()); Present(); }
        public void Dispose() { Coordinator.Dispose(); Context.Dispose(); }
    }
    private sealed class NeutralState : ProbeState { public NeutralState(GameStateContext context) : base(context) { } }
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
