using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Game.Integration;
using Assets.Scripts.Game.Navigation;
using TrainDominoesRulesEngine;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InputManager : MonoBehaviour,IInteractionInput
{
    public event EventHandler<int> DominoClicked, ChallengeClicked;
    public event EventHandler<TrainIdDto> TrainChosen;
    public event EventHandler DrawButtonClicked,EndTurnClicked,ReadyButtonClicked,NewGameButtonClicked,PassClicked,UndoClicked,AnnounceClicked,RetryClicked,ResumeClicked;
    public Camera MainCamera;
    // Preserved serialized reference. Board scrolling and zoom now come from BoardGestureController (VIEW-001).
    [SerializeField] internal ControlsManager ControlsManager;
    [SerializeField] private Button DrawButton,EndTurnButton,RoundReadyButton,RestartReadyButton,NewGameButton,QuitButton;
    [SerializeField] private float noticeSeconds=5f;
    private ClientSession client;
    private GameObject panel;
    private TextMeshProUGUI status,notice;
    private float noticeUntil;
    private readonly Dictionary<string,Button> actions=new Dictionary<string,Button>();
    private readonly List<string> actionOrder=new List<string>();
    private readonly List<Button> challenges=new List<Button>();
    private readonly List<string> notices=new List<string>();
    private string rosterKey="";
    private BoardNavigator navigator;
    private LayoutManager layout;
    private BoardGestureController gestures;
    private BoardRowControls rowControls;
    public BoardRowControls RowControls => rowControls;
    public BoardGestureController Gestures => gestures;
    /// <summary>Notices shown since binding, oldest first (VIEW-004). For review and checks; the banner shows the latest.</summary>
    public IReadOnlyList<string> NoticeHistory => notices;
    public string StatusText => status!=null ? status.text : "";

    /// <summary>Connects board navigation. Called before <see cref="Bind"/>.</summary>
    public void AttachBoard(LayoutManager layoutManager)
    {
        layout=layoutManager; navigator=layoutManager.Navigator;
        if(gestures==null) { gestures=gameObject.AddComponent<BoardGestureController>(); gestures.Tapped+=Tap; }
        gestures.Initialize(navigator);
    }
    public void Bind(ClientSession session)
    {
        Unbind(); client=session;
        var canvas=DrawButton.GetComponentInParent<Canvas>();
        panel=new GameObject("Match controls",typeof(RectTransform)); panel.transform.SetParent(canvas.transform,false);
        var rect=(RectTransform)panel.transform; rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=rect.offsetMax=Vector2.zero;
        status=Label("Match status",18,new Vector2(0,1),new Vector2(12,-72),new Vector2(-224,-8),TextAlignmentOptions.TopLeft);
        // Announcements, challenges and penalties appear here, above the board and clear of the hand.
        notice=Label("Match notice",20,new Vector2(0,1),new Vector2(12,-122),new Vector2(-224,-76),TextAlignmentOptions.Center);
        notice.color=new Color(1f,0.86f,0.4f);
        AddAction("Ready",()=>ReadyButtonClicked?.Invoke(this,EventArgs.Empty));
        AddAction("Draw",()=>DrawButtonClicked?.Invoke(this,EventArgs.Empty));
        AddAction("Pass",()=>PassClicked?.Invoke(this,EventArgs.Empty));
        AddAction("Undo",()=>UndoClicked?.Invoke(this,EventArgs.Empty));
        AddAction("End Turn",()=>EndTurnClicked?.Invoke(this,EventArgs.Empty));
        AddAction("Announce",()=>AnnounceClicked?.Invoke(this,EventArgs.Empty));
        AddAction("Retry",()=>RetryClicked?.Invoke(this,EventArgs.Empty));
        AddAction("Resume",()=>ResumeClicked?.Invoke(this,EventArgs.Empty));
        AddAction("New Match",()=>NewGameButtonClicked?.Invoke(this,EventArgs.Empty));
        AddAction("Fit board",()=>navigator?.Fit());
        foreach(var button in new[]{DrawButton,EndTurnButton,RoundReadyButton,RestartReadyButton,NewGameButton}) if(button!=null) button.gameObject.SetActive(false);
        if(QuitButton!=null) QuitButton.onClick.AddListener(Quit);
        if(navigator!=null)
        {
            rowControls=panel.AddComponent<BoardRowControls>();
            rowControls.Initialize(navigator,layout,rect,(label,callback)=>Button(label,callback,new Vector2(0.5f,0.5f),Vector2.zero),train=>TrainChosen?.Invoke(this,train));
        }
        client.ViewChanged+=Refresh; client.RequestChanged+=Refresh; client.PresentationChanged+=Refresh; client.SessionAvailabilityChanged+=Refresh; client.SelectionChanged+=Refresh;
        Refresh(this,EventArgs.Empty);
    }
    private TextMeshProUGUI Label(string name,float size,Vector2 anchor,Vector2 min,Vector2 max,TextAlignmentOptions alignment)
    {
        var text=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.transform.SetParent(panel.transform,false); text.font=DrawButton.GetComponentInChildren<TextMeshProUGUI>(true)?.font;
        text.fontSize=size; text.color=Color.white; text.raycastTarget=false; text.alignment=alignment;
        var rt=text.rectTransform; rt.anchorMin=new Vector2(0,anchor.y); rt.anchorMax=new Vector2(1,anchor.y); rt.pivot=new Vector2(0,1); rt.offsetMin=min; rt.offsetMax=max;
        return text;
    }
    private void AddAction(string label,Action callback) { actions[label]=Button(label,callback,new Vector2(1,1),Vector2.zero); actionOrder.Add(label); }
    private Button Button(string label,Action callback,Vector2 anchor,Vector2 position)
    {
        var button=Instantiate(DrawButton,panel.transform); button.name=label; button.gameObject.SetActive(true); button.onClick=new Button.ButtonClickedEvent(); button.onClick.AddListener(()=>callback());
        var text=button.GetComponentInChildren<TextMeshProUGUI>(true); if(text!=null) {text.text=label; text.fontSize=16;}
        var rt=(RectTransform)button.transform; rt.anchorMin=rt.anchorMax=anchor; rt.pivot=anchor; rt.anchoredPosition=position; rt.sizeDelta=new Vector2(185,36); rt.localScale=Vector3.one;
        return button;
    }
    /// <summary>Shows event notices (VIEW-004). The banner keeps the latest few for a few seconds.</summary>
    public void ShowNotices(IEnumerable<string> lines)
    {
        foreach(var line in lines) { notices.Add(line); Debug.Log("[Notice] "+line); }
        if(notice==null) return;
        notice.text=string.Join("\n",notices.Skip(Math.Max(0,notices.Count-2)));
        noticeUntil=Time.unscaledTime+noticeSeconds;
    }
    private void Refresh(object sender,EventArgs e)
    {
        if(client==null) return;
        var view=client.View;
        string key=view==null ? "" : view.Token.MatchId+":"+view.Player+":"+string.Join(",",view.Players);
        if(key!=rosterKey)
        {
            foreach(var button in challenges) Destroy(button.gameObject);
            challenges.Clear(); rosterKey=key;
            rowControls?.Build(view);
            if(view!=null)
                foreach(int player in view.Players)
                {
                    int target=player;
                    challenges.Add(Button("Challenge "+PlayerText.Name(player),()=>ChallengeClicked?.Invoke(this,target),new Vector2(1,1),Vector2.zero));
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
        Set("Fit board",view?.Phase==MatchPhase.Playing,true);
        // Visible actions stack without gaps, so the column stays short at small window sizes.
        int slot=0;
        foreach(var label in actionOrder) if(actions[label].gameObject.activeSelf) ((RectTransform)actions[label].transform).anchoredPosition=new Vector2(-12,-80-slot++*40);
        // Challenges are actions too: they continue the same column, below the other actions.
        for(int i=0;i<challenges.Count;i++)
        {
            bool can=view.Allowed.ChallengeTargets.Contains(view.Players[i]);
            if(can) ((RectTransform)challenges[i].transform).anchoredPosition=new Vector2(-12,-80-slot++*40);
            challenges[i].gameObject.SetActive(can); challenges[i].interactable=usable;
        }
        rowControls?.Refresh(view,board,client.SelectedTile);
        string phase=!client.IsAvailable && client.Credential!=null ? "Disconnected from the host · press Retry to reconnect to your seat" : view==null ? "Connect players, then Ready to create the match." : view.IsSuspended || !client.IsAvailable ? SuspendedText(client.MissingSeats) : view.Phase==MatchPhase.Playing ? TurnText(view)+" · "+RequirementText(view) : view.Phase==MatchPhase.AwaitingDeck ? "Preparing the next round" : view.Phase==MatchPhase.AwaitingReady ? "Ready for the next round" : "Results";
        string pending=client.Pending==null ? "" : client.IsUncertain ? "Waiting for the host… press Retry if this persists. " : "Sending… ";
        status.text=phase+"\n"+(view==null ? "" : "You: "+PlayerText.Name(view.Player)+" · Boneyard: "+view.BoneyardCount+" · "+string.Join("  ",view.HandCounts.Select(p=>"P"+(p.Player+1)+": "+p.Count)))+"\n"+pending+client.Feedback;
    }
    private static string TurnText(PlayerViewDto view) => view.ActivePlayer==view.Player ? "Your turn" : PlayerText.Name(view.ActivePlayer)+"'s turn";
    private static string SuspendedText(int[] missing)
    {
        if(missing.Length==0) return "Match suspended · everyone is back; the host can resume";
        return "Match suspended · waiting for "+string.Join(", ",missing.Select(seat=>"Player "+(seat+1)))+" to reconnect";
    }
    private static string RequirementText(PlayerViewDto view)
    {
        if(view.ActivePlayer!=view.Player) return view.OpenDoubles!=null && view.OpenDoubles.Length>0 ? "An open double must be covered" : "Waiting";
        switch(view.Requirement)
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
        if(notice!=null && !string.IsNullOrEmpty(notice.text) && Time.unscaledTime>noticeUntil) notice.text="";
    }
    /// <summary>A click or tap that did not become a drag: the single selection path for hand and board tiles.</summary>
    private void Tap(Vector2 screen)
    {
        var tile=TileAt(screen);
        if(tile.HasValue) DominoClicked?.Invoke(this,tile.Value);
    }
    /// <summary>The tile under a screen point through the production raycast, or null.</summary>
    public int? TileAt(Vector2 screen)
    {
        if(MainCamera==null) return null;
        // Board roots move every frame while navigating; colliders must match what is drawn before hit-testing.
        Physics.SyncTransforms();
        if(Physics.Raycast(MainCamera.ScreenPointToRay(screen),out var hit))
        { var tile=hit.transform.GetComponent<DominoEntityUI>(); if(tile?.DominoInfo!=null) return tile.DominoInfo.ID; }
        return null;
    }
    private static void Quit() { Application.Quit(); }
    public void Unbind()
    {
        if(client!=null) { client.ViewChanged-=Refresh;client.RequestChanged-=Refresh;client.PresentationChanged-=Refresh;client.SessionAvailabilityChanged-=Refresh;client.SelectionChanged-=Refresh; }
        client=null; if(panel!=null) Destroy(panel); actions.Clear(); actionOrder.Clear(); challenges.Clear(); rowControls=null; rosterKey="";
        if(QuitButton!=null) QuitButton.onClick.RemoveListener(Quit);
    }
    private void OnDestroy() { Unbind(); if(gestures!=null) gestures.Tapped-=Tap; }
}
