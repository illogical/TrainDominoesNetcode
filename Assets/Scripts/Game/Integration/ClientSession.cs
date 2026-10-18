using System;
using System.Collections.Generic;
using System.Linq;
using TrainDominoesRulesEngine;

namespace Assets.Scripts.Game.Integration
{
    /// <summary>Recipient-only state and delivery correlation. Never owns game rules or waits for animation to accept a result.</summary>
    public sealed class ClientSession
    {
        private readonly Action<CommandRequest> send;
        private readonly HashSet<string> retiredMatches = new HashSet<string>();
        public PlayerViewDto View { get; private set; }
        public CommandRequest Pending { get; private set; }
        public bool IsUncertain { get; private set; }
        public bool IsAvailable { get; private set; } = true;
        public bool IsPresentationBusy { get; private set; }
        public bool IsRoundPresented { get; private set; }
        public long PresentationGeneration { get; private set; }
        public int? SelectedTile { get; private set; }
        public string Feedback { get; private set; } = "Host: start a match with 2–8 connected players.";
        public event EventHandler ViewChanged, RequestChanged, SessionAvailabilityChanged, PresentationChanged, SelectionChanged;
        public event Action<PlayerViewDto, PlayerViewDto, PresentationEventDto[], long, bool> PresentationRequested;
        public event Action StartMatchRequested, NewMatchRequested, SynchronizationRequested, ResumeRequested;
        public ClientSession(Action<CommandRequest> send) { this.send = send ?? throw new ArgumentNullException(nameof(send)); }

        public void BeginConnection()
        {
            View = null; Pending = null; IsUncertain = false; SelectedTile = null;
            PresentationGeneration++; IsPresentationBusy = false; IsRoundPresented = false;
            IsAvailable = true;
            RequestChanged?.Invoke(this, EventArgs.Empty);
            ViewChanged?.Invoke(this, EventArgs.Empty);
        }
        public bool BoardEnabled => IsAvailable && View != null && !View.IsSuspended && View.Phase == MatchPhase.Playing && View.ActivePlayer == View.Player && Pending == null && !IsPresentationBusy;
        public bool CanSubmit => IsAvailable && View != null && !View.IsSuspended && Pending == null;
        public void Ready()
        {
            if (View == null) { StartMatchRequested?.Invoke(); return; }
            if (View.Allowed.CanReady) Submit(CommandKind.Ready);
        }
        public void NewMatch() => NewMatchRequested?.Invoke();
        public void Resume() => ResumeRequested?.Invoke();
        public void Synchronize() => SynchronizationRequested?.Invoke();
        public void SetFeedback(string message) { Feedback = message; ViewChanged?.Invoke(this, EventArgs.Empty); }
        public void SetAvailable(bool available)
        {
            IsAvailable = available;
            if (!available) { PresentationGeneration++; IsPresentationBusy = false; SelectedTile = null; }
            SessionAvailabilityChanged?.Invoke(this, EventArgs.Empty);
        }
        public bool Submit(CommandKind kind, int dominoId = -1, TrainIdDto train = null, int target = -1)
        {
            if (!CanSubmit) return false;
            if (kind != CommandKind.Announce && kind != CommandKind.Challenge && kind != CommandKind.Ready && !BoardEnabled) return false;
            Pending = new CommandRequest { RequestId = Guid.NewGuid().ToString("N"), Expected = TokenDto.From(View.Token.ToToken()), Kind = kind, DominoId = dominoId, Train = train, Target = target };
            IsUncertain = false;
            RequestChanged?.Invoke(this, EventArgs.Empty);
            // A host may synchronously settle this request. Never change request state after send.
            send(Pending);
            return true;
        }
        public void MarkUncertain()
        {
            if (Pending == null) return;
            IsUncertain = true;
            Feedback = "Waiting for confirmation. Reconcile or retry the same request.";
            RequestChanged?.Invoke(this, EventArgs.Empty);
        }
        public void Retry()
        {
            if (Pending == null || !IsAvailable) return;
            send(Pending);
        }
        public void ClickTile(int tile)
        {
            if (!BoardEnabled) return;
            if (View.Hand.Contains(tile))
            {
                SelectedTile = SelectedTile == tile ? null : (int?)tile;
                SelectionChanged?.Invoke(this, EventArgs.Empty);
                return;
            }
            var train = View.Trains.FirstOrDefault(t => t.Placements.Length > 0 && t.Placements[t.Placements.Length-1].DominoId == tile);
            if (train != null) ChooseTrain(train.Id);
            else SetFeedback("Choose a personal train or the Mexican Train using its button.");
        }
        public void ChooseTrain(TrainIdDto train)
        {
            if (!BoardEnabled || !SelectedTile.HasValue) return;
            if (!View.LegalPlacements.Any(p => p.DominoId == SelectedTile && p.Train.ToId() == train.ToId()))
            { SetFeedback("That tile cannot be played on this train."); return; }
            Submit(CommandKind.Place, SelectedTile.Value, train);
        }
        public void Receive(ProjectionPacket packet, bool synchronization = false)
        {
            if (packet == null) return;
            var old = View;
            var next = packet.View;
            bool sameMatch = old != null && next != null && old.Token.MatchId == next.Token.MatchId;
            bool newer = next != null && !retiredMatches.Contains(next.Token.MatchId) && (old == null || !sameMatch || next.Token.Revision > old.Token.Revision);
            bool settled = Pending != null && packet.Outcome != null && packet.Outcome.RequestId == Pending.RequestId;
            bool requestChanged = settled;
            if (settled)
            {
                Pending = null; IsUncertain = false;
                Feedback = packet.Outcome.Accepted ? "" : packet.Outcome.Error == RuleError.AnnouncementNotRequired ? "No announcement is needed." : packet.Outcome.Error.ToString();
            }
            if (newer)
            {
                bool replace = old == null || !sameMatch || old.Token.RoundIndex != next.Token.RoundIndex || old.EngineId != next.EngineId;
                bool missingHistory = sameMatch && next.Token.Revision > old.Token.Revision + 1;
                bool terminal = next.Phase == MatchPhase.RoundOver || next.Phase == MatchPhase.MatchOver || next.IsSuspended;
                bool oldPhase = old != null && (replace || old.Phase != next.Phase);
                if (old != null && !sameMatch) retiredMatches.Add(old.Token.MatchId);
                if (oldPhase || next.IsSuspended) { requestChanged |= Pending != null; Pending = null; IsUncertain = false; }
                View = next;
                if (SelectedTile.HasValue && (!next.Hand.Contains(SelectedTile.Value) || next.ActivePlayer != next.Player || terminal)) SelectedTile = null;
                IsPresentationBusy = true;
                if (replace) IsRoundPresented = false;
                if (replace || synchronization || terminal || missingHistory) PresentationGeneration++;
                PresentationRequested?.Invoke(old, next, packet.Events ?? Array.Empty<PresentationEventDto>(), PresentationGeneration, replace || synchronization || terminal || missingHistory);
            }
            else if (synchronization && next != null && sameMatch && next.Token.Revision == old.Token.Revision)
            {
                IsPresentationBusy = true; PresentationGeneration++;
                PresentationRequested?.Invoke(old, old, Array.Empty<PresentationEventDto>(), PresentationGeneration, true);
            }
            // Availability is restored only by an explicit synchronized response, not a delayed cached packet.
            if (synchronization && next != null && View != null && next.Token.MatchId == View.Token.MatchId && next.Token.Revision == View.Token.Revision && !next.IsSuspended) IsAvailable = true;
            if (requestChanged) RequestChanged?.Invoke(this, EventArgs.Empty);
            ViewChanged?.Invoke(this, EventArgs.Empty);
        }
        public void CompletePresentation(long generation)
        {
            if (generation != PresentationGeneration) return;
            IsPresentationBusy = false;
            IsRoundPresented = View != null && View.Phase == MatchPhase.Playing;
            PresentationChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
