using System;
using Assets.Scripts.Game.Integration;
using TrainDominoesRulesEngine;
static class Program
{
    static void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
    static ProjectionPacket View(long revision, MatchPhase phase = MatchPhase.Playing, string match = "m") => new ProjectionPacket { View = new PlayerViewDto { Token = new TokenDto { MatchId=match, Revision=revision, RoundIndex=0, TurnNumber=1 }, Phase=phase, Player=0, ActivePlayer=0, Hand=new[]{1}, Trains=Array.Empty<TrainViewDto>(), Allowed=new AllowedActionsDto { CanDraw=true }, LegalPlacements=Array.Empty<LegalPlacementDto>() }};
    static void Main()
    {
        ClientSession client = null;
        client = new ClientSession(r => { Check(client.Pending == r, "install pending before immediate host send"); client.Receive(new ProjectionPacket { View=View(1).View, Outcome=new CommandOutcome { RequestId=r.RequestId, Accepted=true } }); });
        client.Receive(View(1)); client.CompletePresentation(client.PresentationGeneration);
        Check(client.Submit(CommandKind.Draw), "submit"); Check(client.Pending==null, "no-op acceptance settles");
        var sent=0; CommandRequest pending=null;
        client=new ClientSession(r=> {sent++;pending=r;}); client.Receive(View(2)); client.CompletePresentation(client.PresentationGeneration);
        client.Submit(CommandKind.Draw); client.Receive(View(4));
        client.Receive(new ProjectionPacket {View=View(3).View,Outcome=new CommandOutcome {RequestId=pending.RequestId,Accepted=false,Error=RuleError.StalePosition}});
        Check(client.View.Token.Revision==4 && client.Pending==null,"old outcome settles without rollback");
        client.CompletePresentation(client.PresentationGeneration); client.Submit(CommandKind.Draw); client.MarkUncertain(); client.Retry(); Check(sent==3,"retry same request");
        var old=client.PresentationGeneration; client.Receive(View(5,MatchPhase.RoundOver)); Check(client.Pending==null,"terminal preemption");
        client.Receive(View(0,MatchPhase.AwaitingReady,"new")); client.CompletePresentation(old); Check(client.IsPresentationBusy,"old callback cannot unlock new match");
        client.CompletePresentation(client.PresentationGeneration); Check(!client.IsPresentationBusy,"current completion unlocks");
        var ignored=client.View; client.Receive(View(99,MatchPhase.Playing,"m")); Check(client.View==ignored,"retired match cannot return");
        client.SetAvailable(false); client.Receive(View(5,MatchPhase.Playing,"m"),true); Check(!client.IsAvailable,"retired synchronization cannot restore availability");
        client.Receive(View(1,MatchPhase.Playing,"new"),true); client.CompletePresentation(client.PresentationGeneration); client.Submit(CommandKind.Draw);
        var suspended=View(2,MatchPhase.Playing,"new"); suspended.View.IsSuspended=true; client.Receive(suspended); Check(client.Pending==null,"suspension retires pending even within same phase");
        client.BeginConnection(); Check(client.View==null && client.Pending==null && !client.BoardEnabled && !client.IsPresentationBusy,"transport restart cannot reuse old rules or pending state");
        var gapClient=new ClientSession(_=>{}); gapClient.Receive(View(1));
        bool gapSnap=false;
        gapClient.PresentationRequested+=(before,after,events,generation,replace)=>gapSnap=replace;
        gapClient.Receive(View(3)); Check(gapSnap,"missing presentation history requires a snap rather than an identity-only placement diff");
        Console.WriteLine("PASS client correlation, freshness, uncertainty, terminal preemption, presentation generations");
    }
}
