using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Game.Integration;
using TrainDominoesRulesEngine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class InputManager : MonoBehaviour,IInteractionInput
{
    public event EventHandler<int> DominoClicked, ChallengeClicked;
    public event EventHandler<TrainIdDto> TrainChosen;
    public event EventHandler DrawButtonClicked,EndTurnClicked,ReadyButtonClicked,NewGameButtonClicked,PassClicked,UndoClicked,AnnounceClicked,RetryClicked,ResumeClicked;
    public event EventHandler<DominoScrollEventArgs> ScrollOverDomino;
    public class DominoScrollEventArgs : EventArgs { public Vector2 ScrollAmount; public int DominoId; }
    public Camera MainCamera;
    [SerializeField] internal ControlsManager ControlsManager;
    [SerializeField] private Button DrawButton,EndTurnButton,RoundReadyButton,RestartReadyButton,NewGameButton,QuitButton;
    private ClientSession client;
    private GameObject panel;
    private TextMeshProUGUI status;
    private readonly Dictionary<string,Button> actions=new Dictionary<string,Button>();
    private readonly List<Button> trains=new List<Button>(),challenges=new List<Button>();
    private string rosterKey="";
    private bool scrollHooked;
    private void Start()
    {
        if(ControlsManager?.ScrollAction!=null) { ControlsManager.ScrollAction.performed+=ScrollActionOnperformed; scrollHooked=true; }
    }
    public void Bind(ClientSession session)
    {
        Unbind(); client=session;
        var canvas=DrawButton.GetComponentInParent<Canvas>();
        panel=new GameObject("Match controls",typeof(RectTransform)); panel.transform.SetParent(canvas.transform,false);
        var rect=(RectTransform)panel.transform; rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=rect.offsetMax=Vector2.zero;
        status=new GameObject("Match status",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        status.transform.SetParent(panel.transform,false); status.font=DrawButton.GetComponentInChildren<TextMeshProUGUI>(true)?.font;
        status.fontSize=18; status.color=Color.white; status.raycastTarget=false;
        var sr=status.rectTransform; sr.anchorMin=new Vector2(0,1); sr.anchorMax=new Vector2(1,1); sr.pivot=new Vector2(0,1); sr.offsetMin=new Vector2(12,-72); sr.offsetMax=new Vector2(-210,-8);
        AddAction("Ready",()=>ReadyButtonClicked?.Invoke(this,EventArgs.Empty),0);
        AddAction("Draw",()=>DrawButtonClicked?.Invoke(this,EventArgs.Empty),1);
        AddAction("Pass",()=>PassClicked?.Invoke(this,EventArgs.Empty),2);
        AddAction("Undo",()=>UndoClicked?.Invoke(this,EventArgs.Empty),3);
        AddAction("End Turn",()=>EndTurnClicked?.Invoke(this,EventArgs.Empty),4);
        AddAction("Announce",()=>AnnounceClicked?.Invoke(this,EventArgs.Empty),5);
        AddAction("Retry",()=>RetryClicked?.Invoke(this,EventArgs.Empty),6);
        AddAction("Resume",()=>ResumeClicked?.Invoke(this,EventArgs.Empty),7);
        AddAction("New Match",()=>NewGameButtonClicked?.Invoke(this,EventArgs.Empty),8);
        foreach(var button in new[]{DrawButton,EndTurnButton,RoundReadyButton,RestartReadyButton,NewGameButton}) if(button!=null) button.gameObject.SetActive(false);
        if(QuitButton!=null) QuitButton.onClick.AddListener(Quit);
        client.ViewChanged+=Refresh; client.RequestChanged+=Refresh; client.PresentationChanged+=Refresh; client.SessionAvailabilityChanged+=Refresh; client.SelectionChanged+=Refresh;
        Refresh(this,EventArgs.Empty);
    }
    private void AddAction(string label,Action callback,int index) { actions[label]=Button(label,callback,new Vector2(1,1),new Vector2(-12,-80-index*42)); }
    private Button Button(string label,Action callback,Vector2 anchor,Vector2 position)
    {
        var button=Instantiate(DrawButton,panel.transform); button.name=label; button.gameObject.SetActive(true); button.onClick=new Button.ButtonClickedEvent(); button.onClick.AddListener(()=>callback());
        var text=button.GetComponentInChildren<TextMeshProUGUI>(true); if(text!=null) {text.text=label; text.fontSize=16;}
        var rt=(RectTransform)button.transform; rt.anchorMin=rt.anchorMax=anchor; rt.pivot=anchor; rt.anchoredPosition=position; rt.sizeDelta=new Vector2(185,36); rt.localScale=Vector3.one;
        return button;
    }
    private void Refresh(object sender,EventArgs e)
    {
        if(client==null) return;
        var view=client.View;
        string key=view==null ? "" : view.Token.MatchId+":"+string.Join(",",view.Players);
        if(key!=rosterKey)
        {
            foreach(var button in trains.Concat(challenges)) Destroy(button.gameObject);
            trains.Clear(); challenges.Clear(); rosterKey=key;
            if(view!=null)
            {
                for(int i=0;i<=view.Players.Length;i++)
                {
                    var train=i==view.Players.Length ? new TrainIdDto {IsShared=true} : new TrainIdDto {Owner=view.Players[i]};
                    string title=train.IsShared ? "Mexican Train" : "Player "+(train.Owner+1)+" train";
                    trains.Add(Button(title,()=>TrainChosen?.Invoke(this,train),new Vector2(0,1),new Vector2(12,-85-i*38)));
                }
                foreach(int player in view.Players)
                {
                    int target=player;
                    var challenge=Button("Challenge Player "+(player+1),()=>ChallengeClicked?.Invoke(this,target),new Vector2(0,0),new Vector2(12+challenges.Count*195,12));
                    challenges.Add(challenge);
                }
            }
        }
        bool usable=client.CanSubmit,board=client.BoardEnabled;
        Set("Ready",view==null || view.Phase==MatchPhase.AwaitingReady || view.Phase==MatchPhase.RoundOver,view==null || usable && view.Allowed.CanReady);
        Set("Draw",view?.Phase==MatchPhase.Playing,board && view.Allowed.CanDraw);
        Set("Pass",view?.Phase==MatchPhase.Playing,board && view.Allowed.CanPass);
        Set("Undo",view?.Settings?.Commitment==CommitmentMode.Draft && view.Phase==MatchPhase.Playing,board && view.Allowed.CanUndo);
        Set("End Turn",view?.Settings?.Commitment==CommitmentMode.Draft && view.Phase==MatchPhase.Playing,board && view.Allowed.CanEndTurn);
        Set("Announce",view?.Phase==MatchPhase.Playing,usable);
        Set("Retry",client.Pending!=null || !client.IsAvailable,true);
        // Resume is the host's action and only works once every seat is connected again (NET-004).
        Set("Resume",view!=null && view.IsSuspended,client.MissingSeats.Length==0 && Unity.Netcode.NetworkManager.Singleton!=null && Unity.Netcode.NetworkManager.Singleton.IsHost);
        Set("New Match",view!=null && (view.IsSuspended || view.Phase==MatchPhase.MatchOver),true);
        foreach(var button in trains) { button.gameObject.SetActive(view?.Phase==MatchPhase.Playing); button.interactable=board && client.SelectedTile.HasValue; }
        int visibleChallenge=0;
        for(int i=0;i<challenges.Count;i++)
        {
            bool can=view.Allowed.ChallengeTargets.Contains(view.Players[i]);
            if(can) ((RectTransform)challenges[i].transform).anchoredPosition=new Vector2(12+visibleChallenge++*195,12);
            challenges[i].gameObject.SetActive(can); challenges[i].interactable=usable;
        }
        string phase=!client.IsAvailable && client.Credential!=null ? "Disconnected from the host · press Retry to reconnect to your seat" : view==null ? "Connect players, then Ready to create the match." : view.IsSuspended || !client.IsAvailable ? SuspendedText(client.MissingSeats) : view.Phase==MatchPhase.Playing ? "Player "+(view.ActivePlayer+1)+"'s turn · "+RequirementText(view.Requirement) : view.Phase==MatchPhase.AwaitingDeck ? "Preparing the next round" : view.Phase==MatchPhase.AwaitingReady ? "Ready for the next round" : "Results";
        status.text=phase+"\n"+(view==null ? "" : "You: Player "+(view.Player+1)+" · Boneyard: "+view.BoneyardCount+" · "+string.Join("  ",view.HandCounts.Select(p=>"P"+(p.Player+1)+": "+p.Count)))+"\n"+client.Feedback;
    }
    private static string SuspendedText(int[] missing)
    {
        if(missing.Length==0) return "Match suspended · everyone is back; the host can resume";
        return "Match suspended · waiting for "+string.Join(", ",missing.Select(seat=>"Player "+(seat+1)))+" to reconnect";
    }
    private static string RequirementText(PlayRequirement requirement)
    {
        switch(requirement)
        {
            case PlayRequirement.AdditionalPlay:return "Play another tile";
            case PlayRequirement.CoverInheritedDouble:return "Cover the open double";
            case PlayRequirement.CoverOneOfTwo:return "Cover one of the two doubles";
            case PlayRequirement.Complete:return "Commit your turn or undo";
            default:return "Choose a tile, then a train";
        }
    }
    private void Set(string label,bool visible,bool enabled) { actions[label].gameObject.SetActive(visible); actions[label].interactable=enabled; }
    private void Update()
    {
        if(!UnityEngine.Input.GetMouseButtonDown(0) || MainCamera==null) return;
        if(UnityEngine.EventSystems.EventSystem.current!=null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;
        if(Physics.Raycast(MainCamera.ScreenPointToRay(UnityEngine.Input.mousePosition),out var hit))
        { var tile=hit.transform.GetComponent<DominoEntityUI>(); if(tile?.DominoInfo!=null) DominoClicked?.Invoke(this,tile.DominoInfo.ID); }
    }
    private void ScrollActionOnperformed(InputAction.CallbackContext context)
    {
        if(MainCamera!=null && Physics.Raycast(MainCamera.ScreenPointToRay(UnityEngine.Input.mousePosition),out var hit))
        { var tile=hit.transform.GetComponent<DominoEntityUI>(); if(tile?.DominoInfo!=null) ScrollOverDomino?.Invoke(this,new DominoScrollEventArgs {DominoId=tile.DominoInfo.ID,ScrollAmount=context.ReadValue<Vector2>()}); }
    }
    private static void Quit() { Application.Quit(); }
    public void Unbind()
    {
        if(client!=null) { client.ViewChanged-=Refresh;client.RequestChanged-=Refresh;client.PresentationChanged-=Refresh;client.SessionAvailabilityChanged-=Refresh;client.SelectionChanged-=Refresh; }
        client=null; if(panel!=null) Destroy(panel); actions.Clear(); trains.Clear(); challenges.Clear();rosterKey="";
        if(QuitButton!=null) QuitButton.onClick.RemoveListener(Quit);
    }
    private void OnDestroy() { Unbind(); if(scrollHooked && ControlsManager?.ScrollAction!=null) ControlsManager.ScrollAction.performed-=ScrollActionOnperformed; }
}
