using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Assets.Scripts.Game.Integration;
using Assets.Scripts.Game;
using TrainDominoesRulesEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

// Temporary-project only. Uses production networking, projections and presentation.
// File barriers intentionally have no timeout: a human can inspect/take over indefinitely.
public sealed class ReviewRuntime : MonoBehaviour
{
    string root, role, checkpoint="starting", captureRequest;
    bool host, recording, complete, checkpointPaused; float excludedPause, excludedScaledPause; int frame, step; float nextFrame;
    GameSession session; ClientSession Client=>session.Client;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() { if(Environment.GetCommandLineArgs().Contains("--integration-review")) new GameObject("ReviewRuntime").AddComponent<ReviewRuntime>(); }
    string Arg(string name,string fallback) { var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,name);return i>=0&&i+1<a.Length?a[i+1]:fallback; }
    string PathFor(string name)=>Path.Combine(root,name);
    void Check(bool ok,string name) { if(!ok) throw new Exception("ASSERT review "+name); Debug.Log("REVIEW "+role+" PASS "+name); }
    IEnumerator Wait(Func<bool> test,string name,float seconds=30)
    { float start=Time.realtimeSinceStartup;while(!test()) { if(Time.realtimeSinceStartup-start>seconds) throw new Exception("TIMEOUT review "+name);yield return null; } }
    IEnumerator Start()
    {
        root=Arg("--review-dir","/tmp/td-review");host=Environment.GetCommandLineArgs().Contains("--integration-host");role=host?"host":"client";
        Directory.CreateDirectory(PathFor(role+"-frames"));Time.timeScale=1;Application.runInBackground=true;
        foreach(var m in FindObjectsByType<SimpleMatchmaking>())m.enabled=false;
        yield return new WaitForSecondsRealtime(2);
        session=FindAnyObjectByType<GameSession>();
        var net=NetworkManager.Singleton;net.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1",17901);
        Check(host?net.StartHost():net.StartClient(),"network-start");
        yield return Wait(()=>net.IsConnectedClient&&session.IsSpawned,"spawn");
        if(host)
        {
            yield return Wait(()=>net.ConnectedClientsIds.Count==2,"two-peers");
            var authority=new AuthoritativeMatchCoordinator("cli-review-fixture",net.ConnectedClientsIds.OrderBy(i=>i).ToArray(),0,new MatchSettings(),IntegrationRuntime.Deck);
            typeof(GameSession).GetField("authority",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(session,authority);
            File.WriteAllText(PathFor("authority-ready"),"deterministic fixture; not normal startup evidence");
        }
        else yield return Wait(()=>File.Exists(PathFor("authority-ready")),"authority");
        Client.Synchronize();yield return Wait(()=>Client.View!=null,"initial-view");
        recording=true;StartCoroutine(CaptureFrames());
        yield return Checkpoint("readiness");
        float readyStart=Time.realtimeSinceStartup;
        while(Client.View.Phase!=MatchPhase.Playing)
        {
            if(Time.realtimeSinceStartup-readyStart>30)throw new Exception("TIMEOUT review readiness");
            if(Client.CanSubmit&&Client.View.Allowed.CanReady)Client.Ready();yield return null;
        }
        yield return Wait(()=>!Client.IsPresentationBusy,"deal");
        Check(Client.View.Hand.Length==DominoCatalog.HandSize(2),"deal-count");
        Check(Client.View.Trains.Length==3,"own-other-shared-trains");
        yield return Checkpoint("deal");
        int count=int.Parse(Arg("--review-actions","8"));
        for(int action=0;action<count;action++)
        {
            var v=Client.View;
            if(v.Phase!=MatchPhase.Playing)break;
            long revision=v.Token.Revision;
            bool actor=v.ActivePlayer==v.Player;
            if(!actor)Check(!Client.BoardEnabled,"waiting-board-disabled");
            if(actor&&v.LegalPlacements.Length>0)Client.ClickTile(v.LegalPlacements[0].DominoId);
            yield return Checkpoint("before-action-"+action);
            // Manual takeover invalidates the script. The external controller terminates both
            // peers and restarts a fresh deterministic run before any scripted assertion resumes.
            if(actor)
            {
                Check(Client.BoardEnabled,"actor-board-enabled");
                if(v.LegalPlacements.Length>0)Client.ChooseTrain(v.LegalPlacements[0].Train);
                else if(v.Allowed.CanDraw)Client.Submit(CommandKind.Draw);
                else if(v.Allowed.CanPass)Client.Submit(CommandKind.Pass);
                else throw new Exception("ASSERT review actor-has-no-progress-action");
            }
            yield return Wait(()=>Client.View.Token.Revision>revision&&Client.Pending==null,"action-accepted");
            Check(Time.timeScale==1,"normal-speed-timescale");
            yield return Wait(()=>!Client.IsPresentationBusy,"motion-settled");
            Check(Client.View.HandCounts.Single(p=>p.Player==Client.View.Player).Count==Client.View.Hand.Length,"recipient-own-count");
            Check(!Client.View.Trains.Any(t=>t.Placements.Any(p=>p.IsProvisional&&p.PlayedBy!=Client.View.Player)),"no-foreign-provisional");
            yield return Checkpoint("after-action-"+action);
        }
        yield return Checkpoint("finished");
        recording=false;complete=true;File.WriteAllText(PathFor(role+"-complete"),"automated-pass; visual review unreviewed");
        Debug.Log("REVIEW "+role+" PASS ALL");yield return new WaitForSecondsRealtime(.3f);Application.Quit();
    }
    IEnumerator Checkpoint(string name)
    {
        checkpoint=name;string id=(step++).ToString("D3")+"-"+name;
        captureRequest=PathFor(id+"-"+role+".png");
        yield return Wait(()=>captureRequest==null,"checkpoint-capture");
        var view=Client.View;
        File.WriteAllText(PathFor(id+"-"+role+".json"),JsonUtility.ToJson(new CheckpointInfo {
            checkpoint=id,seat=view.Player,actor=view.ActivePlayer,phase=view.Phase.ToString(),revision=view.Token.Revision,
            width=Screen.width,height=Screen.height,timeScale=Time.timeScale,
            uiText=FindObjectsByType<TMPro.TMP_Text>().Where(t=>t.isActiveAndEnabled).Select(t=>t.text).ToArray(),
            state=session.PlayerStateContext.CurrentState.GetType().Name,boardEnabled=Client.BoardEnabled
        },true));
        File.WriteAllText(PathFor(id+"-"+role+"-ready"),"ready");
        float pauseStart=Time.realtimeSinceStartup, scaledPauseStart=Time.time; checkpointPaused=true;
        while(!File.Exists(PathFor(id+"-continue")))yield return null;
        excludedPause+=Time.realtimeSinceStartup-pauseStart;excludedScaledPause+=Time.time-scaledPauseStart;checkpointPaused=false;nextFrame=0;
    }
    IEnumerator CaptureFrames()
    {
        while(recording)
        {
            yield return new WaitForEndOfFrame();
            if((checkpointPaused||Time.realtimeSinceStartup<nextFrame)&&captureRequest==null)continue;
            var texture=ScreenCapture.CaptureScreenshotAsTexture();
            var bytes=texture.EncodeToPNG();Destroy(texture);
            if(captureRequest!=null) {File.WriteAllBytes(captureRequest,bytes);captureRequest=null;}
            File.WriteAllBytes(PathFor(role+"-frames/"+(frame++).ToString("D6")+".png"),bytes);
            var mesh=FindAnyObjectByType<MeshManager>();
            var moving=DominoCatalog.All.Select(d=>new {id=d.Id,obj=mesh.GetDominoMeshById(d.Id)}).Where(d=>d.obj!=null&&d.obj.activeInHierarchy)
                .Select(d=>new TileSample{id=d.id,position=d.obj.transform.position,rotation=d.obj.transform.rotation,scale=d.obj.transform.lossyScale}).ToArray();
            File.AppendAllText(PathFor(role+"-motion.jsonl"),JsonUtility.ToJson(new MotionSample {
                frame=frame-1,realTime=Time.realtimeSinceStartup-excludedPause,scaledTime=Time.time-excludedScaledPause,
                busy=Client.IsPresentationBusy,tiles=moving
            })+"\n");
            File.AppendAllText(PathFor(role+"-frames.csv"),(frame-1)+","+(Time.realtimeSinceStartup-excludedPause).ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"\n");
            nextFrame=Time.realtimeSinceStartup+.1f;
        }
    }
    void OnGUI()
    {
        if(root==null||complete)return;
        // This stamp is rasterized into the ACTUAL frame, beside the production UI.
        // The gallery validates the stamp with OCR; production UI text is checked separately.
        var style=new GUIStyle(GUI.skin.label){fontSize=18,normal={textColor=Color.white}};
        GUI.Box(new Rect(8,50,760,64),"");
        GUI.Label(new Rect(16,52,750,30),"REVIEW FRAME TEXT "+role+" | "+checkpoint,style);
        GUI.Label(new Rect(16,80,750,30),"Fixture deck / localhost / 1x | visual acceptance UNREVIEWED",style);
    }
    [Serializable] sealed class TileSample { public int id;public Vector3 position,scale;public Quaternion rotation; }
    [Serializable] sealed class MotionSample { public int frame; public float realTime,scaledTime;public bool busy;public TileSample[] tiles; }
    [Serializable] sealed class CheckpointInfo
    { public string checkpoint,phase,state;public string[] uiText;public int seat,actor,width,height;public long revision;public float timeScale;public bool boardEnabled; }
}
