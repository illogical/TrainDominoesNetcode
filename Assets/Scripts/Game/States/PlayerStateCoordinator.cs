using System;
using Assets.Scripts.Game.Integration;
using TrainDominoesRulesEngine;

namespace Assets.Scripts.Game.States
{
    /// <summary>One routing policy for every authoritative update. This chooses local interaction, never game rules.</summary>
    public sealed class PlayerStateCoordinator : IDisposable
    {
        private readonly GameStateContext ctx;
        private readonly ClientSession client;
        private bool started;
        private string match;
        private int round = -1;
        public PlayerStateCoordinator(GameStateContext context) { ctx = context; client = context.Client; }
        public void Start()
        {
            if (started) return;
            started = true;
            client.ViewChanged += ViewChanged;
            client.RequestChanged += RequestChanged;
            client.PresentationChanged += PresentationChanged;
            client.SessionAvailabilityChanged += SessionAvailabilityChanged;
            Reconcile();
        }
        public void Dispose()
        {
            client.ViewChanged -= ViewChanged;
            client.RequestChanged -= RequestChanged;
            client.PresentationChanged -= PresentationChanged;
            client.SessionAvailabilityChanged -= SessionAvailabilityChanged;
            started = false;
        }
        private void ViewChanged(object sender, EventArgs e) => Reconcile();
        private void RequestChanged(object sender, EventArgs e) => Reconcile();
        private void PresentationChanged(object sender, EventArgs e) => Reconcile();
        private void SessionAvailabilityChanged(object sender, EventArgs e) => Reconcile();
        public void Reconcile()
        {
            if (!started) return;
            var view = client.View;
            var current = ctx.CurrentState;
            if (!client.IsAvailable || view != null && view.IsSuspended)
            {
                if (!(current is PlayerSessionSuspendedState)) ctx.SwitchState(c => new PlayerSessionSuspendedState(c));
                return;
            }
            bool freshMatch = view != null && match != view.Token.MatchId;
            bool freshRound = view != null && round != view.Token.RoundIndex;
            if (view != null) { match = view.Token.MatchId; round = view.Token.RoundIndex; }
            // Pending of the current phase retains its underlying result/setup presentation.
            if (client.Pending != null)
            {
                if (!(current is PlayerActionPendingState)) ctx.SwitchState(c => new PlayerActionPendingState(c));
                return;
            }
            if (view == null || view.Phase == MatchPhase.AwaitingReady)
            {
                if (freshMatch) ctx.SwitchState(c => new PregameState(c), reuse: false);
                else if (!(current is PregameState)) ctx.SwitchState(c => new PregameState(c));
                return;
            }
            if (view.Phase == MatchPhase.MatchOver)
            {
                if (!(current is GameOverState)) ctx.SwitchState(c => new GameOverState(c));
                return;
            }
            if (view.Phase == MatchPhase.RoundOver)
            {
                if (!(current is RoundOverState)) ctx.SwitchState(c => new RoundOverState(c));
                return;
            }
            if (view.Phase == MatchPhase.AwaitingDeck || !client.IsRoundPresented)
            {
                if (freshRound) ctx.SwitchState(c => new RoundInitializingState(c), reuse: false);
                else if (!(current is RoundInitializingState)) ctx.SwitchState(c => new RoundInitializingState(c));
                return;
            }
            if (view.ActivePlayer == view.Player)
            {
                if (!(current is PlayerActingState)) ctx.SwitchState(c => new PlayerActingState(c));
            }
            else
            {
                if (!(current is PlayerAwaitingTurnState)) ctx.SwitchState(c => new PlayerAwaitingTurnState(c));
            }
        }
    }
}
