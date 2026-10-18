namespace TrainDominoesRulesEngine
{
    public abstract class GameCommand
    {
        public PositionToken Expected { get; }
        protected GameCommand(PositionToken expected) { Expected = expected; }
    }
    public sealed class Place : GameCommand
    {
        public int DominoId { get; }
        public TrainId Train { get; }
        public Place(PositionToken expected, int dominoId, TrainId train) : base(expected)
        { DominoId = dominoId; Train = train; }
    }
    public sealed class Draw : GameCommand { public Draw(PositionToken expected) : base(expected) { } }
    public sealed class Pass : GameCommand { public Pass(PositionToken expected) : base(expected) { } }
    public sealed class EndTurn : GameCommand { public EndTurn(PositionToken expected) : base(expected) { } }
    public sealed class Undo : GameCommand { public Undo(PositionToken expected) : base(expected) { } }
    public sealed class Ready : GameCommand { public Ready(PositionToken expected) : base(expected) { } }
    public sealed class AnnounceOneTile : GameCommand { public AnnounceOneTile(PositionToken expected) : base(expected) { } }
    public sealed class ChallengeMissedAnnouncement : GameCommand
    {
        public PlayerId Target { get; }
        public ChallengeMissedAnnouncement(PositionToken expected, PlayerId target) : base(expected) { Target = target; }
    }
}
