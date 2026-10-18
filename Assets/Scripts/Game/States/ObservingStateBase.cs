using System;
using Assets.Scripts.Game.Integration;

namespace Assets.Scripts.Game.States
{
    /// <summary>Shared observing inputs. Authoritative transitions belong exclusively to PlayerStateCoordinator.</summary>
    public abstract class ObservingStateBase : GameStateBase
    {
        protected ObservingStateBase(GameStateContext context) : base(context) { }
        public override void EnterState()
        {
            ctx.Input.AnnounceClicked += AnnounceClicked;
            ctx.Input.ChallengeClicked += ChallengeClicked;
        }
        public override void LeaveState()
        {
            ctx.Input.AnnounceClicked -= AnnounceClicked;
            ctx.Input.ChallengeClicked -= ChallengeClicked;
        }
        private void AnnounceClicked(object sender, EventArgs e)
        { using var trace = TraceExecution(); ctx.Client.Submit(CommandKind.Announce); }
        private void ChallengeClicked(object sender, int target)
        { using var trace = TraceExecution(); ctx.Client.Submit(CommandKind.Challenge, target: target); }
    }
}
