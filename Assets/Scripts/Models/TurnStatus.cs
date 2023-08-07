namespace Assets.Scripts.Models
{
    public class TurnStatus
    {
        public bool HasMadeMove { get; private set; }
        public bool HasLaidFirstTrack { get; private set; }
        public bool HasPlayerAddedTrack { get; private set; }
        public int? PlayedDominoId { get; private set; }

        public TurnStatus()
        {
            HasLaidFirstTrack = false;
            ResetTurnStatus();
        }

        public void ResetTurnStatus()
        {
            HasMadeMove = false;
            HasPlayerAddedTrack = false;
            PlayedDominoId = null;
        }

        public void PlayerAddedTrack(int affectedDominoId)
        {
            HasLaidFirstTrack = true;
            HasPlayerAddedTrack = true;
            PlayerMadeMove(affectedDominoId);
        }
    
        public void PlayerMadeMove(int affectedDominoId)
        {
            HasMadeMove = true;
            PlayedDominoId = affectedDominoId;
        }
    }
}