using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Assets.Scripts.Game;
using Assets.Scripts.Game.Integration;
using TrainDominoesRulesEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
public sealed class EightRuntime:MonoBehaviour
{
    bool host; GameSession session; string role="starting";
    ClientSession Client=>session.Client;
    const string Prefix="/tmp/td-eight-";
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot() { if(Environment.GetCommandLineArgs().Contains("--integration-eight")) new GameObject("EightRuntime").AddComponent<EightRuntime>(); }
    void Assert(bool condition,string name) { if(!condition) throw new Exception("ASSERT "+name);Debug.Log("EIGHT "+role+" PASS "+name); }
    IEnumerator Wait(Func<bool> condition,string name) { float start=Time.realtimeSinceStartup;while(!condition()) { if(Time.realtimeSinceStartup-start>45) throw new Exception("TIMEOUT "+name+" "+Client.Feedback);yield return null; } }
    IEnumerator Start()
    {
        host=Environment.GetCommandLineArgs().Contains("--integration-host");
        foreach(var m in FindObjectsByType<SimpleMatchmaking>(FindObjectsSortMode.None))m.enabled=false;
        yield return new WaitForSecondsRealtime(2);session=FindFirstObjectByType<GameSession>();var net=NetworkManager.Singleton;
        net.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1",17899);
        Assert(host?net.StartHost():net.StartClient(),"network-start");yield return Wait(()=>net.IsConnectedClient&&session.IsSpawned,"spawn");role="seat"+net.LocalClientId;
        if(host)
        {
            yield return Wait(()=>net.ConnectedClientsIds.Count==8,"eight-peers");var ids=net.ConnectedClientsIds.OrderBy(i=>i).ToArray();
            var authority=new AuthoritativeMatchCoordinator("cli-eight",ids,0,null,round=>DominoCatalog.All.Where(d=>d.Id!=DominoCatalog.EngineForRound(round)).Select(d=>d.Id));
            typeof(GameSession).GetField("authority",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(session,authority);
            typeof(GameSession).GetField("matchParticipants",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(session,ids);File.WriteAllText(Prefix+"ready","ready");
        }
        else yield return Wait(()=>File.Exists(Prefix+"ready"),"authority");
        Client.Synchronize();yield return Wait(()=>Client.View!=null,"view");while(Client.View.Phase!=MatchPhase.Playing) { if(Client.CanSubmit&&Client.View.Allowed.CanReady)Client.Ready();yield return null; }
        yield return Wait(()=>!Client.IsPresentationBusy,"deal");
        Assert(Client.View.Hand.Length==9&&Client.View.Trains.Length==9&&Client.View.Players.Length==8,"nine-tile-hand-nine-trains-eight-seats");
        File.WriteAllText(Prefix+role+"-dealt","ready");
        if(host)
        {
            yield return Wait(()=>Directory.GetFiles("/tmp","td-eight-seat*-dealt").Length==8,"all-deals");
            if(Client.View.LegalPlacements.Length>0) {var p=Client.View.LegalPlacements[0];Client.ClickTile(p.DominoId);Client.ChooseTrain(p.Train);}else Client.Submit(CommandKind.Draw);
            yield return Wait(()=>Client.Pending==null&&!Client.IsPresentationBusy,"action");Assert(string.IsNullOrEmpty(Client.Feedback),"accepted-eight-player-action");
            File.WriteAllText(Prefix+"disconnect","go");yield return Wait(()=>Client.View.IsSuspended,"seat-loss-suspends-match");
            Assert(!Client.BoardEnabled&&!Client.CanSubmit,"suspended-input-disabled");
            yield return Wait(()=>net.ConnectedClientsIds.Count==7,"seven-remain");Client.NewMatch();yield return Wait(()=>Client.View.Token.MatchId!="cli-eight","fresh-seven-player-match");
            Assert(Client.View.Players.Length==7&&Client.View.Trains.Length==0&&Client.View.RoundResults.Length==0,"new-match-seven-fixed-seats");File.WriteAllText(Prefix+"done","done");
        }
        else if(net.LocalClientId==7) {yield return Wait(()=>File.Exists(Prefix+"disconnect"),"disconnect-signal");Assert(true,"intentional-seat-disconnect");net.Shutdown();}
        else {yield return Wait(()=>File.Exists(Prefix+"done") && Client.View.Players.Length==7,"completion");Assert(Client.View.Players.Length==7,"seven-seat-view-received");}
        Debug.Log("EIGHT "+role+" PASS ALL");yield return new WaitForSecondsRealtime(1);Application.Quit();
    }
}
