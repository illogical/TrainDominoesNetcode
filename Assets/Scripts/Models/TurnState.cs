namespace Assets.Scripts.Models
{
    public class TurnState
    {
        public bool HasMadeMove { get; private set; }
        public bool HasLaidFirstTrack { get; private set; }
        public bool HasPlayerAddedTrack { get; private set; }

        
        public TurnState()
        {
            HasLaidFirstTrack = false;
            ResetTurnStatus();
        }

        public void ResetTurnStatus()
        {
            HasMadeMove = false;
            HasPlayerAddedTrack = false;
        }

        public void PlayerAddedTrack()
        {
            HasLaidFirstTrack = true;
            HasPlayerAddedTrack = true;
            PlayerMadeMove();
        }
    
        public void PlayerMadeMove()
        {
            HasMadeMove = true;
        }
    }
}