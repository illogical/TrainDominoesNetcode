using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Assets.Scripts.Game;
public class LegacyBaseline:MonoBehaviour {
 bool host; string role; GameplayManager gm; GameSession gs;
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot(){new GameObject("LegacyBaseline").AddComponent<LegacyBaseline>();}
 void Mark(string s){Debug.Log("BASELINE "+role+" "+s);}
 IEnumerator Start(){host=Environment.GetCommandLineArgs().Contains("--baseline-host"); role=host?"host":"client"; foreach(var m in FindObjectsByType<SimpleMatchmaking>(FindObjectsSortMode.None)) m.enabled=false; yield return new WaitForSecondsRealtime(3); gm=FindFirstObjectByType<GameplayManager>();gs=FindFirstObjectByType<GameSession>(); var dev=FindFirstObjectByType<DevMode>(); Mark($"settings bypass={dev.IgnoreDominoComparisons} round={dev.StartAtRound} hand={dev.DominoStartCount}"); var nm=NetworkManager.Singleton;nm.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1",17897);Mark("start="+(host?nm.StartHost():nm.StartClient())); yield return new WaitForSecondsRealtime(8); Mark("connected="+nm.IsConnectedClient+" peers="+nm.ConnectedClientsIds.Count); yield return new WaitForSecondsRealtime(3); Snap("startup");
 if(host){File.WriteAllText("/tmp/td-baseline-ready","ready");} else {yield return new WaitUntil(()=>File.Exists("/tmp/td-baseline-ready"));} 
 gs.DrawDominoesServerRpc();yield return new WaitForSecondsRealtime(3);Snap("draw-initial");
 if(host){gs.DrawDominoesServerRpc();gs.DrawDominoesServerRpc();yield return new WaitForSecondsRealtime(3);Snap("repeat-draw");}
 gs.EndTurnServerRpc();yield return new WaitForSecondsRealtime(6);Snap("group-submit");
 if(host){var ids=gm.DominoTracker.GetPlayerDominoes(nm.LocalClientId); if(ids.Count>0){var id=ids[0];gs.SelectDominoServerRpc(id);yield return new WaitForSecondsRealtime(1);gs.SelectDominoServerRpc(gm.DominoTracker.GetEngineDominoID());yield return new WaitForSecondsRealtime(3);Snap("placement");gs.SelectDominoServerRpc(id);yield return new WaitForSecondsRealtime(3);Snap("undo");} gs.DrawDominoesServerRpc();yield return new WaitForSecondsRealtime(2);Snap("individual-draw");gs.EndTurnServerRpc();yield return new WaitForSecondsRealtime(3);Snap("individual-end");}
 yield return new WaitForSecondsRealtime(8);Snap("final");Application.Quit(); }
 void Snap(string phase){try{var nm=NetworkManager.Singleton;Mark(phase+" state="+gs.PlayerStateContext.CurrentState.GetType().Name+" hand="+gm.DominoTracker.GetPlayerDominoes(nm.LocalClientId).Count+" group="+gm.TurnManager.IsGroupTurn);}catch(Exception e){Mark(phase+" snapshot-error="+e.GetType().Name+":"+e.Message);}}
}
