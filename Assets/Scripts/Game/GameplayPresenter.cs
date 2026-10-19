using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Game;
using Assets.Scripts.Game.Integration;
using TrainDominoesRulesEngine;
using UnityEngine;

/// <summary>Projects recipient-visible data only. Completion means motion finished, never a rules action.</summary>
public sealed class GameplayPresenter : MonoBehaviour
{
    private MeshManager meshes;
    private LayoutManager layout;
    private SoundManager sounds;
    private readonly List<Coroutine> selections = new List<Coroutine>();
    private string presentedRound;
    private bool presenting;
    private int? selected;
    private int selectionMotions;
    public Func<int?> SelectionSource { get; set; }
    public Func<bool> HandReflowAllowed { get; set; }
    private int? Selection => SelectionSource != null ? SelectionSource() : selected;
    public PlayerViewDto Current { get; private set; }

    public void Initialize(MeshManager meshManager, LayoutManager layoutManager, SoundManager soundManager)
    {
        meshes = meshManager;
        layout = layoutManager;
        sounds = soundManager;
        layout.HandReflow.Bind(() => Current == null ? new List<Transform>() : Hand(Current).Values.ToList(),
            () => Selection.HasValue && Current?.Hand != null ? Array.IndexOf(Current.Hand, Selection.Value) : -1,
            () => presenting || selectionMotions > 0 || (HandReflowAllowed != null && !HandReflowAllowed()));
    }

    public static int TrainSlot(PlayerViewDto view, TrainIdDto id)
    {
        if (id.IsShared) return view.Players.Length;
        int slot = Array.IndexOf(view.Players, id.Owner);
        if (slot < 0) throw new InvalidOperationException("Train owner is not a match participant.");
        return slot;
    }

    private static DominoEntity Entity(int id, PurposeType purpose, PlacementDto placement = null)
    {
        var tile = DominoCatalog.Get(id);
        return new DominoEntity { ID = id, TopScore = tile.Low, BottomScore = tile.High,
            Flipped = placement != null && placement.Inward != tile.Low, Purpose = purpose };
    }

    private GameObject Ensure(int id, PurposeType purpose, PlacementDto placement = null)
    {
        var entity = Entity(id, purpose, placement);
        var mesh = meshes.GetDominoMeshById(id);
        if (mesh == null)
            mesh = purpose == PurposeType.Engine ? meshes.CreateEngineDomino(entity, new Vector3(-1, 0, 0))
                : meshes.CreatePlayerDominoFromInfo(entity, new Vector3(0, 1, 0), purpose);
        else meshes.UpdateDomino(entity);
        return mesh;
    }

    private HashSet<int> Prepare(PlayerViewDto view)
    {
        var visible = new HashSet<int>();
        if (view.EngineId >= 0) { Ensure(view.EngineId, PurposeType.Engine); visible.Add(view.EngineId); }
        foreach (int id in view.Hand ?? Array.Empty<int>()) { Ensure(id, PurposeType.Player); visible.Add(id); }
        foreach (var train in view.Trains ?? Array.Empty<TrainViewDto>())
            foreach (var placement in train.Placements ?? Array.Empty<PlacementDto>())
            { Ensure(placement.DominoId, PurposeType.Track, placement); visible.Add(placement.DominoId); }
        meshes.RetainDominoMeshes(visible);
        return visible;
    }

    private Dictionary<int, Transform> Hand(PlayerViewDto view)
    {
        return meshes.GetDominoTransformsByIds(view.Hand ?? Array.Empty<int>());
    }

    private void Labels(PlayerViewDto view)
    {
        meshes.DisableAllTrackLabels();
        int count = view.Players.Length + 1;
        foreach (var train in view.Trains ?? Array.Empty<TrainViewDto>())
        {
            int slot = TrainSlot(view, train.Id);
            int tiles = train.Placements?.Length ?? 0;
            string title = train.Id.IsShared ? "Mexican Train" : train.Id.Owner == view.Player ? "Your train" : "Player " + (train.Id.Owner + 1);
            if (train.IsMarked) title += " (open)";
            // Labels live under their row, so they scroll and zoom with it.
            layout.PlaceLabel(meshes.SetTrackLabelForTrack(slot, Vector3.zero, title), slot, count, tiles);
        }
    }

    private static string RoundKey(PlayerViewDto view) => view == null ? null : view.Token.MatchId + "/" + view.Token.RoundIndex + "/" + view.EngineId;

    /// <summary>Navigation is local and resets on a new round or match (D4). Row count follows the roster.</summary>
    private void EnterRound(PlayerViewDto view)
    {
        layout.Navigator.SetRowCount(view.Players.Length + 1);
        string key = RoundKey(view);
        if (key == presentedRound) return;
        presentedRound = key;
        layout.Navigator.ResetNavigation();
    }

    /// <summary>Row lengths drive the follow policy: a row showing its end keeps following new placements.</summary>
    private void UpdateRowLengths(PlayerViewDto view)
    {
        foreach (var train in view.Trains ?? Array.Empty<TrainViewDto>())
            layout.Navigator.SetRowTiles(TrainSlot(view, train.Id), train.Placements?.Length ?? 0);
    }

    public void Snap(PlayerViewDto view)
    {
        Cancel();
        Current = view;
        if (view == null) { meshes.ResetDominoMeshes(); meshes.DisableAllTrackLabels(); presentedRound = null; layout.Navigator.ResetNavigation(); return; }
        EnterRound(view);
        Prepare(view);
        if (view.EngineId >= 0) layout.SnapEngine(meshes.GetDominoMeshById(view.EngineId).transform);
        var positions = layout.GetHandLocalPositions(view.Hand?.Length ?? 0);
        for (int i = 0; i < positions.Count; i++)
            layout.SnapToHand(meshes.GetDominoMeshById(view.Hand[i]).transform, positions[i]);
        foreach (var train in view.Trains ?? Array.Empty<TrainViewDto>())
            for (int i = 0; i < (train.Placements?.Length ?? 0); i++)
                layout.SnapToTrack(meshes.GetDominoMeshById(train.Placements[i].DominoId).transform, i, TrainSlot(view, train.Id), view.Players.Length + 1);
        UpdateRowLengths(view);
        Labels(view);
    }

    public IEnumerator Present(PlayerViewDto before, PlayerViewDto after, PresentationEventDto[] events)
    {
        Cancel();
        if (after == null) { Snap(null); yield break; }
        Current = after;
        presenting = true;
        bool newRound = before == null || before.Token.MatchId != after.Token.MatchId || before.Token.RoundIndex != after.Token.RoundIndex || before.EngineId != after.EngineId;
        if (newRound) presentedRound = null;
        EnterRound(after);
        if (newRound)
        {
            meshes.ResetDominoMeshes();
            Prepare(after);
            var setup = new List<IEnumerator>();
            if (after.EngineId >= 0) setup.Add(layout.PlaceEngineRoutine(meshes.GetDominoMeshById(after.EngineId)));
            setup.Add(layout.PlaceInitialPlayerDominoesRoutine((after.Hand ?? Array.Empty<int>()).Select(meshes.GetDominoMeshById).ToList()));
            yield return layout.Together(setup.ToArray());
        }
        else Prepare(after);

        var oldHand = new HashSet<int>(before?.Hand ?? Array.Empty<int>());
        var oldBoard = new HashSet<int>((before?.Trains ?? Array.Empty<TrainViewDto>()).SelectMany(t => t.Placements ?? Array.Empty<PlacementDto>()).Select(p => p.DominoId));
        // Compare visible placement identity, not event kind: a seal does not replay the actor's draft motion.
        var placements = new List<IEnumerator>();
        foreach (var train in after.Trains ?? Array.Empty<TrainViewDto>())
            for (int i = 0; i < (train.Placements?.Length ?? 0); i++)
                if (newRound || !oldBoard.Contains(train.Placements[i].DominoId))
                {
                    placements.Add(layout.PlaceOnTrackRoutine(meshes.GetDominoMeshById(train.Placements[i].DominoId).transform,
                        i, TrainSlot(after, train.Id), after.Players.Length + 1));
                }
        // Rows grow as their placements start, so a following row eases to its new end with the tile in flight (D4).
        UpdateRowLengths(after);
        yield return layout.Together(placements.ToArray());
        if (!newRound)
        {
            var hand = Hand(after);
            foreach (var id in after.Hand ?? Array.Empty<int>())
                if (!oldHand.Contains(id))
                {
                    if (oldBoard.Contains(id)) yield return layout.ReturnDominoToPlayerRoutine(hand, id);
                    else yield return layout.AddNewDominoForPlayerRoutine(hand, id);
                }
            if (!(before.Hand ?? Array.Empty<int>()).SequenceEqual(after.Hand ?? Array.Empty<int>()))
                yield return layout.UpdatePlayerPositions(hand);
        }
        Labels(after);
        presenting = false;
        layout.HandReflow.Invalidate();
    }

    public void Select(int? previous, int? current)
    {
        selected = current;
        layout.HandReflow.SelectionChanged();
        layout.HandReflow.Invalidate();
        foreach (var routine in selections) if (routine != null) StopCoroutine(routine);
        selections.Clear();
        selectionMotions = 0;
        if (previous.HasValue && meshes.GetDominoMeshById(previous.Value) is GameObject oldMesh)
            Track(StartCoroutine(SelectionMotion(oldMesh, false)));
        if (current.HasValue && meshes.GetDominoMeshById(current.Value) is GameObject newMesh)
        {
            // An overfull hand scrolls to keep the selected tile visible.
            int index = Current?.Hand == null ? -1 : Array.IndexOf(Current.Hand, current.Value);
            if (index >= 0) layout.Navigator.EnsureHandVisible(index);
            Track(StartCoroutine(SelectionMotion(newMesh, true)));
        }
    }

    private IEnumerator SelectionMotion(GameObject tile, bool lift)
    {
        selectionMotions++;
        try { yield return layout.SelectDominoRoutine(tile, lift); }
        finally { selectionMotions = Math.Max(0, selectionMotions - 1); }
    }

    // A selection routine that finishes at once (a tile no longer in the hand) returns no handle.
    private void Track(Coroutine handle) { if (handle != null) selections.Add(handle); }

    public void Cancel()
    {
        presenting = false;
        if (layout != null) layout.HandReflow.Cancel();
        foreach (var routine in selections) if (routine != null) StopCoroutine(routine);
        selections.Clear();
        selectionMotions = 0;
        if (layout != null) layout.CancelAnimations();
    }
    private void OnDisable() { Cancel(); }
}
