using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Assets.Scripts.Game.Integration;
using Assets.Scripts.Game;

using TrainDominoesRulesEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

// Install only in an isolated temporary project. Production scripts are never modified by this harness.
public sealed class IntegrationRuntime:MonoBehaviour
{
    CommandRequest captured; PlayerViewDto initialView;
    bool host,draft,gapOnly; string role; GameSession session; int actions, placements, draws, passes, undos, ends, rounds, announcements; bool undoDone;
    ClientSession Client=>session.Client;
    string Prefix=>gapOnly?"/tmp/td-integration-presentation":draft?"/tmp/td-integration-draft":"/tmp/td-integration-immediate";
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot() { if(!Environment.GetCommandLineArgs().Contains("--integration-eight")) new GameObject("IntegrationRuntime").AddComponent<IntegrationRuntime>(); }
    void Mark(string s)=>Debug.Log("INTEGRATION "+role+" "+s);
    void Assert(bool condition,string name) { if(!condition) throw new Exception("ASSERT "+name); Mark("PASS "+name); }
    IEnumerator Wait(Func<bool> condition,string name,float limit=25)
    {
        float start=Time.realtimeSinceStartup;
        while(!condition()) { if(Time.realtimeSinceStartup-start>limit) throw new Exception("TIMEOUT "+name+" state="+session?.PlayerStateContext?.CurrentState?.GetType().Name+" feedback="+session?.Client?.Feedback); yield return null; }
    }
    IEnumerator Start()
    {
        host=Environment.GetCommandLineArgs().Contains("--integration-host"); draft=Environment.GetCommandLineArgs().Contains("--integration-draft"); gapOnly=Environment.GetCommandLineArgs().Contains("--presentation-only"); role=host?"host":"client";
        foreach(var m in FindObjectsByType<SimpleMatchmaking>(FindObjectsSortMode.None)) m.enabled=false;
        yield return new WaitForSecondsRealtime(2);
        session=FindFirstObjectByType<GameSession>();
        Client.RequestChanged+=(sender,args)=> { if(Client.Pending!=null) captured=JsonUtility.FromJson<CommandRequest>(JsonUtility.ToJson(Client.Pending)); };
        var net=NetworkManager.Singleton; net.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1",17898);
        Assert(host?net.StartHost():net.StartClient(),"network-start");
        yield return Wait(()=>net.IsConnectedClient && session.IsSpawned,"network-spawn");
        if(host)
        {
            yield return Wait(()=>net.ConnectedClientsIds.Count==2,"two-peers");
            var ids=net.ConnectedClientsIds.OrderBy(i=>i).ToArray();
            var authority=new AuthoritativeMatchCoordinator("cli-"+(draft?"draft":"immediate"),ids,0,new MatchSettings(draft?CommitmentMode.Draft:CommitmentMode.Immediate,AnnouncementPenalty.DrawOneTile),Deck);
            typeof(GameSession).GetField("authority",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(session,authority);
            File.WriteAllText(Prefix+"-ready","ready");
        }
        else yield return Wait(()=>File.Exists(Prefix+"-ready"),"authority-ready");
        Client.Synchronize(); yield return Wait(()=>Client.View!=null,"initial-view");
        Assert(Client.View.Hand.Length==0 && Client.View.Players.Length==2,"filtered-initial-view");
        yield return ReadyUntilPlaying();
        yield return Wait(()=>!Client.IsPresentationBusy,"deal-complete");
        Assert(Client.View.Hand.Length==DominoCatalog.HandSize(2) && Client.View.Trains.Length==3,"deal-profile-hand-and-three-trains");
        initialView=Copy(Client.View);
        Mark("normal-speed-setup-completed");
        if(gapOnly)
        {
            Time.timeScale=50;yield return RevisionGapProbe();File.WriteAllText(Prefix+"-"+role+"-gap","done");
            yield return Wait(()=>File.Exists(Prefix+"-"+(host?"client":"host")+"-gap"),"both-gap-probes");
            Mark("PASS ALL");yield return new WaitForSecondsRealtime(1);Application.Quit();yield break;
        }
        if(!host) yield return Wait(()=>File.Exists(Prefix+"-first-done"),"host-first-action");
        float started=Time.realtimeSinceStartup;
        while(Client.View.Phase!=MatchPhase.MatchOver && Time.realtimeSinceStartup-started<300)
        {
            if(Client.View.IsSuspended) throw new Exception("unexpected suspension");
            if(Client.View.Phase==MatchPhase.RoundOver)
            {
                rounds=Client.View.RoundResults.Length;
                if(Client.CanSubmit && Client.View.Allowed.CanReady) Client.Ready();
                yield return null; continue;
            }
            if(Client.CanSubmit && Client.View.Allowed.CanAnnounce)
            {
                Client.Submit(CommandKind.Announce);yield return Wait(()=>Client.Pending==null,"announce-settled");
                if(string.IsNullOrEmpty(Client.Feedback)) {announcements++;Mark("PASS accepted-Announce");}
                yield return null;continue;
            }
            if(!Client.BoardEnabled) { yield return null; continue; }
            var view=Client.View;
            CommandKind kind; int tile=-1; TrainIdDto train=null;
            if(draft && view.Allowed.CanUndo && !undoDone) { kind=CommandKind.Undo; undoDone=true; undos++; }
            else if(draft && view.Allowed.CanEndTurn) { kind=CommandKind.EndTurn; ends++; }
            else if(view.LegalPlacements.Length>0) { var p=view.LegalPlacements[0]; kind=CommandKind.Place;tile=p.DominoId;train=p.Train;placements++; }
            else if(view.Allowed.CanDraw) { kind=CommandKind.Draw;draws++; }
            else if(view.Allowed.CanPass) { kind=CommandKind.Pass;passes++; }
            else throw new Exception("active actor has no progress command");
            var original=new CommandRequest{ RequestId=Guid.NewGuid().ToString("N"),Expected=TokenDto.From(view.Token.ToToken()),Kind=kind,DominoId=tile,Train=train };
            if(kind==CommandKind.Place) { Client.ClickTile(tile); yield return new WaitForSecondsRealtime(actions==0?0.3f:0.01f); Client.ChooseTrain(train); }
            else Client.Submit(kind);
            actions++;
            yield return Wait(()=>Client.Pending==null,"request-settled");
            if(Client.Feedback==RuleError.StalePosition.ToString()) { Mark("observed-safe-stale-"+kind+" retry-from-latest-view");yield return null;continue; }
            Assert(string.IsNullOrEmpty(Client.Feedback),"accepted-"+kind);
            yield return Wait(()=>!Client.IsPresentationBusy,"motion-complete");
            if(kind==CommandKind.Place && Client.View.Token.RoundIndex==view.Token.RoundIndex) AssertPlacement(tile);
            if(actions==1 && host)
            {
                Mark("normal-speed-first-action-completed");
                long revision=Client.View.Token.Revision;
                // A stale independently identified command must cross real RPC transport and leave the board unchanged.
                Raw(captured); yield return new WaitForSecondsRealtime(0.3f);
                Assert(Client.View.Token.Revision==revision,"duplicate-rpc-does-not-mutate");
                original.RequestId="stale-"+role;
                Raw(original); yield return new WaitForSecondsRealtime(0.3f);
                Assert(Client.View.Token.Revision==revision,"stale-rpc-does-not-mutate");
                Time.timeScale=50; Mark("accelerated-timescale-50"); File.WriteAllText(Prefix+"-first-done","ready");
            }
            if(!host) Time.timeScale=50;
            yield return null;
        }
        Assert(Client.View.Phase==MatchPhase.MatchOver,"thirteen-round-match-complete");
        Assert(Client.View.RoundResults.Length==13 && Client.View.Winners.Length>0,"scores-and-winners");
        Mark($"COUNTS actions={actions} placements={placements} draws={draws} passes={passes} undos={undos} endTurns={ends} announcements={announcements}");
        File.WriteAllText(Prefix+"-"+role+"-complete","complete");
        if(host)
        {
            yield return Wait(()=>File.Exists(Prefix+"-client-complete"),"client-match-complete");
            yield return PresentationProbes();
            Client.NewMatch(); yield return Wait(()=>Client.View.Token.MatchId!="cli-"+(draft?"draft":"immediate"),"fresh-match");
            Assert(Client.View.RoundResults.Length==0 && Client.View.Totals.All(t=>t.Count==0),"fresh-match-clears-scores");
            File.WriteAllText(Prefix+"-reset","reset");
            yield return Wait(()=>File.Exists(Prefix+"-client-reset"),"client-reset");
            net.Shutdown();
        }
        else
        {
            yield return Wait(()=>File.Exists(Prefix+"-reset") && Client.View.RoundResults.Length==0,"reset-received");
            Assert(Client.View.Totals.All(t=>t.Count==0),"remote-reset-clears-scores");
            File.WriteAllText(Prefix+"-client-reset","reset");
            yield return Wait(()=>!Client.IsAvailable,"disconnect-suspends-input");
            Assert(!Client.BoardEnabled,"disconnected-board-disabled");
        }
        Mark("PASS ALL"); yield return new WaitForSecondsRealtime(1); Application.Quit(0);
    }
        void InvokeButton(string name)
    {
        var input=FindFirstObjectByType<InputManager>();
        var buttons=(Dictionary<string,UnityEngine.UI.Button>)typeof(InputManager).GetField("actions",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(input);
        Assert(buttons[name].interactable,"button-enabled-"+name);buttons[name].onClick.Invoke();
    }
    IEnumerator ReadyUntilPlaying()
    {
        float start=Time.realtimeSinceStartup;
        while(Client.View.Phase!=MatchPhase.Playing)
        {
            if(Time.realtimeSinceStartup-start>30)throw new Exception("TIMEOUT readiness");
            if(Client.CanSubmit && Client.View.Allowed.CanReady)InvokeButton("Ready");
            yield return null;
        }
    }
    static PlayerViewDto Copy(PlayerViewDto view)=>JsonUtility.FromJson<PlayerViewDto>(JsonUtility.ToJson(view));
    IEnumerator RevisionGapProbe()
    {
        Mark("PRESENTATION-ONLY revision-gap probe begins");
        var before=Copy(initialView);before.Token.MatchId="gap-probe";before.Token.Revision=10;
        int tile=before.Hand[0];before.Hand=before.Hand.Skip(1).ToArray();var d=DominoCatalog.Get(tile);
        before.Trains[0].Placements=new[]{new PlacementDto{DominoId=tile,Inward=d.Low,Outward=d.High,PlayedBy=before.Player}};
        Client.Receive(new ProjectionPacket{View=before});yield return Wait(()=>!Client.IsPresentationBusy,"gap-before-ready");
        AssertPlacement(tile);long generation=Client.PresentationGeneration;
        var after=Copy(before);after.Token.Revision=12;after.Trains[1].Placements=after.Trains[0].Placements;after.Trains[0].Placements=Array.Empty<PlacementDto>();
        Client.Receive(new ProjectionPacket{View=after});yield return Wait(()=>!Client.IsPresentationBusy,"gap-after-ready");
        Assert(Client.PresentationGeneration>generation,"revision-gap-invalidates-incremental-generation");
        AssertPlacement(tile);Mark("PASS revision-gap-same-tile-moved-to-other-train");
    }
    IEnumerator PresentationProbes()
    {
        Mark("PRESENTATION-ONLY synthetic probes begin");
        var probe=Copy(initialView);probe.Token.MatchId="presentation-probe";probe.Token.Revision=1;
        Client.Receive(new ProjectionPacket{View=probe},true);
        yield return Wait(()=>!Client.IsPresentationBusy,"probe-setup");
        var available=DominoCatalog.All.Select(d=>d.Id).Where(id=>id!=probe.EngineId&&!probe.Hand.Contains(id)).Take(40).ToArray();
        for(int n=1;n<=40;n++)
        {
            var next=Copy(probe);next.Token.Revision=n+1;
            next.Trains.Last().Placements=available.Take(n).Select(id=> {var d=DominoCatalog.Get(id);return new PlacementDto{DominoId=id,Inward=d.Low,Outward=d.High,PlayedBy=0};}).ToArray();
            Client.Receive(new ProjectionPacket{View=next});
        }
        yield return Wait(()=>!Client.IsPresentationBusy,"overflow-complete");
        foreach(int id in available) AssertPlacement(id);
        Mark("PASS presentation-overflow-final-board");
        var draw=Copy(Client.View);draw.Token.Revision++;int added=DominoCatalog.All.Select(d=>d.Id).First(id=>id!=draw.EngineId&&!draw.Hand.Contains(id)&&!available.Contains(id));draw.Hand=draw.Hand.Concat(new[]{added}).ToArray();
        Client.Receive(new ProjectionPacket{View=draw});
        var replacement=Copy(initialView);replacement.Token.MatchId="presentation-replacement";replacement.Token.Revision=1;replacement.Hand=replacement.Hand.Take(1).ToArray();
        Client.Receive(new ProjectionPacket{View=replacement});
        yield return Wait(()=>!Client.IsPresentationBusy,"cancel-replacement-complete");
        yield return new WaitForSecondsRealtime(1);
        var mesh=FindFirstObjectByType<MeshManager>();var position=mesh.GetDominoMeshById(replacement.Hand[0]).transform.position;
        var expected=FindFirstObjectByType<LayoutManager>().GetHandPositions(1)[0];
        Assert(Vector3.Distance(position,expected)<0.001f&&mesh.GetDominoMeshById(added)==null,"presentation-cancel-old-draw-one-tile-layout");
        Mark("PRESENTATION-ONLY synthetic probes end");
    }
    static IEnumerable<int> Deck(int round)
    {
        var remaining=DominoCatalog.All.Where(d=>d.Id!=DominoCatalog.EngineForRound(round)).Select(d=>d.Id).ToList();
        if(round==0) return remaining; // Initial round also exercises draw and pass.
        int engine=DominoCatalog.Get(DominoCatalog.EngineForRound(round)).Low;
        var random=new System.Random(837+round);
        for(int attempt=0;attempt<100;attempt++)
        {
            var pool=new List<int>(remaining);
            var hands=new[]{new List<int>(),new List<int>()};bool valid=true;
            foreach(var hand in hands)
            {
                int end=engine;
                for(int i=0;i<DominoCatalog.HandSize(2);i++)
                {
                    var candidates=pool.Where(t=> {var d=DominoCatalog.Get(t);return d.Low!=d.High && (d.Low==end || d.High==end);}).ToArray();
                    if(candidates.Length==0) {valid=false;break;}
                    int id=candidates[random.Next(candidates.Length)];var tile=DominoCatalog.Get(id);end=tile.Low==end?tile.High:tile.Low;
                    hand.Add(id);pool.Remove(id);
                }
                if(!valid)break;
            }
            if(!valid)continue;
            var deck=new List<int>();
            for(int i=0;i<hands[0].Count;i++) {deck.Add(hands[0][i]);deck.Add(hands[1][i]);}
            deck.AddRange(pool);return deck;
        }
        return remaining;
    }
    void AssertPlacement(int tile)
    {
        var view=Client.View;
        var train=view.Trains.FirstOrDefault(t=>t.Placements.Any(p=>p.DominoId==tile));
        if(train==null) return;
        int index=Array.FindIndex(train.Placements,p=>p.DominoId==tile);
        var expected=FindFirstObjectByType<LayoutManager>().GetTrackPosition(index,GameplayPresenter.TrainSlot(view,train.Id),view.Players.Length+1);
        var actual=FindFirstObjectByType<MeshManager>().GetDominoMeshById(tile).transform.position;
        Assert(!float.IsNaN(actual.x) && !float.IsNaN(actual.y) && !float.IsNaN(actual.z) && Vector3.Distance(expected,actual)<0.001f,"placement-final-transform");
    }
    void Raw(CommandRequest request)=>typeof(GameSession).GetMethod("SubmitCommandServerRpc",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(session,new object[]{JsonUtility.ToJson(request),default(ServerRpcParams)});
}
