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
    private static readonly Quaternion HandRotation = Quaternion.Euler(-90, 0, 180);
    private static readonly Quaternion TrackRotation = Quaternion.Euler(0, -90, -90);

    public void Initialize(MeshManager meshManager, LayoutManager layoutManager, SoundManager soundManager)
    {
        meshes = meshManager;
        layout = layoutManager;
        sounds = soundManager;
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
            int last = (train.Placements?.Length ?? 0) - 1;
            var position = last < 0 ? layout.GetTrackLeftPosition(slot, count)
                : layout.GetTrackLabelPosition(layout.GetTrackPosition(last, slot, count));
            string title = train.Id.IsShared ? "Mexican Train" : "Player " + (train.Id.Owner + 1);
            if (train.IsMarked) title += " (open)";
            meshes.SetTrackLabelForTrack(slot, position, title).transform.position = position;
        }
    }

    public void Snap(PlayerViewDto view)
    {
        Cancel();
        if (view == null) { meshes.ResetDominoMeshes(); meshes.DisableAllTrackLabels(); return; }
        Prepare(view);
        if (view.EngineId >= 0)
        {
            var engine = meshes.GetDominoMeshById(view.EngineId);
            engine.transform.SetPositionAndRotation(layout.GetEnginePosition(engine), HandRotation);
        }
        var positions = layout.GetHandPositions(view.Hand?.Length ?? 0);
        for (int i = 0; i < positions.Count; i++)
            meshes.GetDominoMeshById(view.Hand[i]).transform.SetPositionAndRotation(positions[i], HandRotation);
        foreach (var train in view.Trains ?? Array.Empty<TrainViewDto>())
            for (int i = 0; i < (train.Placements?.Length ?? 0); i++)
                meshes.GetDominoMeshById(train.Placements[i].DominoId).transform.SetPositionAndRotation(
                    layout.GetTrackPosition(i, TrainSlot(view, train.Id), view.Players.Length + 1), TrackRotation);
        Labels(view);
    }

    public IEnumerator Present(PlayerViewDto before, PlayerViewDto after, PresentationEventDto[] events)
    {
        Cancel();
        if (after == null) { Snap(null); yield break; }
        bool newRound = before == null || before.Token.MatchId != after.Token.MatchId || before.Token.RoundIndex != after.Token.RoundIndex || before.EngineId != after.EngineId;
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
    }

    public void Select(int? previous, int? current)
    {
        foreach (var routine in selections) StopCoroutine(routine);
        selections.Clear();
        if (previous.HasValue && meshes.GetDominoMeshById(previous.Value) is GameObject oldMesh)
            selections.Add(StartCoroutine(layout.SelectDominoRoutine(oldMesh, false)));
        if (current.HasValue && meshes.GetDominoMeshById(current.Value) is GameObject newMesh)
            selections.Add(StartCoroutine(layout.SelectDominoRoutine(newMesh, true)));
    }

    public void Cancel()
    {
        foreach (var routine in selections) StopCoroutine(routine);
        selections.Clear();
        if (layout != null) layout.CancelAnimations();
    }
    private void OnDisable() { Cancel(); }
}
