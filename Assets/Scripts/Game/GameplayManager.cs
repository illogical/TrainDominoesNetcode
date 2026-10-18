using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Game;
using Assets.Scripts.Game.Integration;
using TrainDominoesRulesEngine;
using UnityEngine;

/// <summary>Recipient presentation only. Never validates moves, deals tiles, advances turns, or calculates scores.</summary>
public class GameplayManager : MonoBehaviour
{
    [SerializeField] private MeshManager meshManager;
    [SerializeField] private LayoutManager layoutManager;
    [SerializeField] public InputManager InputManager;
    [SerializeField] public SoundManager SoundManager;
    [SerializeField] private RoundOverUI roundOverUI;
    [SerializeField] private GameOverUI gameOverUI;
    [SerializeField] private DevMode devMode; // Preserved serialized development reference; never overrides core legality.
    [SerializeField] private float presentationBudgetSeconds=10f;
    private ClientSession client;
    private GameplayPresenter presenter;
    private Coroutine running;
    private float batchStarted;
    private bool initialized, draining;
    private int? selected;
    private readonly Queue<Batch> queue=new Queue<Batch>();
    private sealed class Batch { public PlayerViewDto Before,After; public PresentationEventDto[] Events; public long Generation; public bool Replace; }
    public void Initialize(ClientSession session)
    {
        if(initialized) return;
        initialized=true; client=session;
        presenter=gameObject.AddComponent<GameplayPresenter>(); presenter.Initialize(meshManager,layoutManager,SoundManager);
        client.PresentationRequested+=QueuePresentation;
        client.ViewChanged+=ViewChanged;
        client.SelectionChanged+=SelectionChanged;
        client.SessionAvailabilityChanged+=AvailabilityChanged;
        InputManager.Bind(client);
    }
    private void QueuePresentation(PlayerViewDto before,PlayerViewDto after,PresentationEventDto[] events,long generation,bool replace)
    {
        // Accepted placement owns this transform now; do not launch a late deselection on it.
        if(selected.HasValue && !after.Hand.Contains(selected.Value)) selected=null;
        bool terminal=after.IsSuspended || after.Phase==MatchPhase.RoundOver || after.Phase==MatchPhase.MatchOver;
        if(queue.Count>=32) { CancelPresentation(); Snap(after,generation); return; }
        if(replace)
        {
            CancelPresentation();
            if(terminal) { Snap(after,generation); return; }
        }
        queue.Enqueue(new Batch { Before=before,After=after,Events=events,Generation=generation,Replace=replace });
        // Coroutines can finish synchronously. Set draining before starting and never overwrite its completion flag.
        if(!draining) { draining=true; running=StartCoroutine(Drain()); }
    }
    private IEnumerator Drain()
    {
        while(queue.Count>0)
        {
            var batch=queue.Dequeue(); batchStarted=Time.realtimeSinceStartup;
            if(batch.Replace && batch.Before!=null && batch.Before.Token.MatchId==batch.After.Token.MatchId && batch.Before.Token.RoundIndex==batch.After.Token.RoundIndex && batch.Before.EngineId==batch.After.EngineId)
                presenter.Snap(batch.After);
            else yield return presenter.Present(batch.Before,batch.After,batch.Events);
            if(queue.Count==0) client.CompletePresentation(batch.Generation);
        }
        draining=false; running=null;
    }
    private void Update()
    {
        if(draining && Time.realtimeSinceStartup-batchStarted>Mathf.Max(1,presentationBudgetSeconds))
        {
            Debug.LogWarning("Presentation exceeded its local budget; reconciling the visible board.",this);
            CancelPresentation(); Snap(client.View,client.PresentationGeneration);
        }
    }
    private void Snap(PlayerViewDto view,long generation)
    {
        try { presenter.Snap(view); }
        catch(Exception error) { Debug.LogException(error,this); }
        finally { client.CompletePresentation(generation); }
    }
    public void CancelPresentation()
    {
        if(running!=null) StopCoroutine(running);
        running=null; draining=false; queue.Clear();
        presenter?.Cancel(); selected=null;
    }
    private void SelectionChanged(object sender,EventArgs e)
    {
        presenter.Select(selected,client.SelectedTile);
        selected=client.SelectedTile;
        if(selected.HasValue) SoundManager?.PlayRandomClickSound();
    }
    private void AvailabilityChanged(object sender,EventArgs e)
    {
        if(!client.IsAvailable) CancelPresentation();
    }
    private void ViewChanged(object sender,EventArgs e)
    {
        var view=client.View;
        if(view==null) { roundOverUI.Hide(); gameOverUI.Hide(); presenter.Snap(null); return; }
        if(view.Phase==MatchPhase.RoundOver || view.Phase==MatchPhase.MatchOver)
        {
            var last=view.RoundResults.LastOrDefault();
            var scores=(last?.Scores ?? Array.Empty<PlayerCountDto>()).ToDictionary(p=>(ulong)p.Player,p=>p.Count);
            var totals=(view.Totals ?? Array.Empty<PlayerCountDto>()).ToDictionary(p=>(ulong)p.Player,p=>p.Count);
            string ending=last==null || last.EndingPlayer<0 ? "Blocked round" : "Player "+(last.EndingPlayer+1);
            if(view.Phase==MatchPhase.MatchOver)
            {
                roundOverUI.Hide();
                gameOverUI.Show(string.Join(" & ",view.Winners.Select(p=>"Player "+(p+1))),scores,totals);
            }
            else { gameOverUI.Hide(); roundOverUI.Show(ending,scores,totals); }
        }
        else { roundOverUI.Hide(); gameOverUI.Hide(); }
        if(selected!=client.SelectedTile) { presenter.Select(selected,client.SelectedTile); selected=client.SelectedTile; }
    }
    public void DisposePresentation()
    {
        if(!initialized) return;
        CancelPresentation();
        client.PresentationRequested-=QueuePresentation; client.ViewChanged-=ViewChanged;
        client.SelectionChanged-=SelectionChanged; client.SessionAvailabilityChanged-=AvailabilityChanged;
        InputManager.Unbind(); initialized=false;
    }
    private void OnDestroy() { DisposePresentation(); }
}
