using Assets.Scripts.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.Models;
using UnityEngine;

public class LayoutManager : MonoBehaviour
{
    [Range(0, 1)]
    [SerializeField] private float BottomYOffset = 0.01f;
    [SerializeField] private float BottomSideMargin = 0.01f;
    [SerializeField] private float SelectionRaiseAmount = 0.02f;
    [Range(0, 2)]
    [SerializeField] private float TrackMargin = 0f; // distance from engine
    [Range(0, 1.5f)]
    [SerializeField] private float CenterLeftYOffset = 0f; // can be used to prevent the tracks from running into the bottom line of boxes
    [SerializeField] private GameObject DominoPrefab;   // used for measuring domino size
    [Header("Animations")]
    public AnimationDefinition EngineSlideIn;
    public AnimationDefinition PlayerDominoSlideIn;
    public AnimationDefinition PlayerDominoSelection;
    public AnimationDefinition PlayerDominoDeselection;
    public AnimationDefinition DominoRotateToTrack;
    public AnimationDefinition DominoSlideToTrack;
    public AnimationDefinition RearrangePlayerDominoes;
    public AnimationDefinition SelectionEase;

    private Camera mainCamera;
    private float playerYPosition;
    private readonly List<Coroutine> motion = new List<Coroutine>();

    private void Awake() { mainCamera = Camera.main; }

    // Every child belongs to this layout, including parallel rotations. Cancellation never
    // stops unrelated gameplay coroutines on a shared MonoBehaviour.
    private Coroutine Run(IEnumerator routine)
    {
        Coroutine handle = null;
        bool finished = false;
        IEnumerator Owned()
        {
            try { yield return routine; }
            finally { finished = true; if (handle != null) motion.Remove(handle); }
        }
        handle = StartCoroutine(Owned());
        if (!finished) motion.Add(handle);
        return handle;
    }

    public void CancelAnimations()
    {
        var active = motion.ToArray();
        motion.Clear();
        foreach (var handle in active) StopCoroutine(handle);
    }

    private void OnDisable() { CancelAnimations(); }

    public IEnumerator Together(params IEnumerator[] routines)
    {
        var handles = new List<Coroutine>();
        foreach (var routine in routines) handles.Add(Run(routine));
        foreach (var handle in handles) yield return handle;
    }

    public List<Vector3> GetHandPositions(int count)
    {
        var positions = PositionHelper.GetLayoutAcrossScreen(PositionHelper.GetObjectDimensions(DominoPrefab), mainCamera, count, BottomSideMargin);
        if (positions.Count > 0) playerYPosition = positions[0].y;
        return positions;
    }

    public IEnumerator PlaceInitialPlayerDominoesRoutine(List<GameObject> dominoes)
    {
        if (dominoes.Count == 0) yield break;
        PositionHelper.LayoutAcrossAndUnderScreen(dominoes, mainCamera, BottomSideMargin);
        var positions = GetHandPositions(dominoes.Count);
        var routines = new List<IEnumerator>();
        for (int i = 0; i < dominoes.Count; i++)
            routines.Add(AnimationHelper.MoveOverSeconds(dominoes[i].transform, positions[i],
                PlayerDominoSlideIn != null ? PlayerDominoSlideIn.Duration : 0,
                PlayerDominoSlideIn != null ? PlayerDominoSlideIn.Delay * i : 0, PlayerDominoSlideIn));
        yield return Together(routines.ToArray());
    }

    public void PlaceInitialPlayerDominoes(List<GameObject> dominoes) { Run(PlaceInitialPlayerDominoesRoutine(dominoes)); }
    public IEnumerator PlaceEngineRoutine(GameObject engine) { yield return AnimationHelper.MoveOverSeconds(engine.transform, GetEnginePosition(engine), EngineSlideIn); }
    public void PlaceEngine(GameObject engine, Action afterComplete = null) { Run(Complete(PlaceEngineRoutine(engine), afterComplete)); }
    public IEnumerator SelectDominoRoutine(GameObject domino, bool selected)
    {
        var destination = new Vector3(domino.transform.position.x, playerYPosition + (selected ? SelectionRaiseAmount : 0), domino.transform.position.z);
        yield return AnimationHelper.MoveOverSeconds(domino.transform, destination, selected ? PlayerDominoSelection : PlayerDominoDeselection);
    }
    public void SelectDomino(GameObject domino) { Run(SelectDominoRoutine(domino, true)); }
    public void DeselectDomino(GameObject domino) { Run(SelectDominoRoutine(domino, false)); }
    public Vector3 GetEnginePosition(GameObject engine)
    {
        return PositionHelper.GetScreenLeftCenterPositionForObject(PositionHelper.GetObjectDimensions(engine), mainCamera, 0);
    }
    private IEnumerator Complete(IEnumerator routine, Action afterComplete)
    {
        yield return routine;
        afterComplete?.Invoke();
    }

    public Vector3 GetTrackLeftPosition(int trackIndex, int trackCount)
    {
        return new Vector3(GetTrackStartXPosition(), GetTrackYPosition(trackIndex, trackCount), 0);
    }

    public Vector3 GetTrackPosition(int trackDominoIndex, int trackIndex, int trackCount)
    {
        return new Vector3(GetTrackXPosition(trackDominoIndex, trackIndex, trackCount), GetTrackYPosition(trackIndex, trackCount), 0);
    }

    private float GetTrackStartXPosition()
    {
        var dominoSize = PositionHelper.GetObjectDimensions(DominoPrefab);
        Vector3 leftCenterPos = PositionHelper.GetScreenLeftCenterPositionForObject(dominoSize, mainCamera, 0);
        var xOffset = 0f;

        float centerObjectSize = dominoSize.x + dominoSize.x / 2;
        float marginFromLeftCenter = centerObjectSize + xOffset;

        return leftCenterPos.x + marginFromLeftCenter;
    }

    private float GetTrackXPosition(int trackObjectIndex, int trackIndex, int trackCount)
    {
        var objLength = PositionHelper.GetObjectDimensions(DominoPrefab);

        Vector3 trackStartPosition = GetTrackLeftPosition(trackIndex, trackCount);

        return trackStartPosition.x + (objLength.y * trackObjectIndex);
    }

    private float GetTrackYPosition(int trackIndex, int trackCount)
    {
        var objLength = PositionHelper.GetObjectDimensions(DominoPrefab).x;
        var leftSide = PositionHelper.GetScreenLeftCenter(mainCamera);

        float totalLength = ((trackCount - 1) * TrackMargin) + ((trackCount - 1) * objLength);
        float centerOffset = totalLength / 2;

        var centerPos = leftSide.y + CenterLeftYOffset;
        var trackIndexLength = trackIndex * objLength + (TrackMargin * trackIndex);

        return centerPos + trackIndexLength - centerOffset;
    }

    public IEnumerator UpdateTrackPosition(GameObject currentObj, float duration, int trackCount)
    {
        // A single transform has one destination, rather than competing moves per track.
        yield return SlideToPosition(currentObj.transform, GetTrackLeftPosition(0, trackCount), duration, 0);
    }

    public IEnumerator PlaceOnTrackRoutine(Transform domino, int index, int slot, int count)
    {
        yield return Together(
            AnimationHelper.RotateOverSeconds(domino, Quaternion.Euler(0, -90, -90),
                DominoRotateToTrack != null ? DominoRotateToTrack.Duration : 0,
                DominoRotateToTrack != null ? DominoRotateToTrack.Delay : 0, DominoRotateToTrack),
            AnimationHelper.MoveOverSeconds(domino, GetTrackPosition(index, slot, count), DominoSlideToTrack));
    }

    private IEnumerator SlideToPosition(Transform target, Vector3 end, float duration, float delay)
    {
        yield return AnimationHelper.MoveOverSeconds(target, end, duration, delay, SelectionEase);
    }

    public IEnumerator AddDominoToNewTrackAndUpdateTrackPositions(Transform domino, int id, List<List<int>> tracks, MeshManager meshes, float duration, Action afterComplete = null)
    {
        yield return Together(UpdateTrackPositions(tracks, meshes, duration, id), PlaceOnTrackRoutine(domino, 0, tracks.Count - 1, tracks.Count));
        afterComplete?.Invoke();
    }
    public IEnumerator AddDominoToExistingTrack(Transform domino, List<List<int>> tracks, int slot, Action afterComplete = null)
    {
        yield return PlaceOnTrackRoutine(domino, tracks[slot].Count - 1, slot, tracks.Count);
        afterComplete?.Invoke();
    }
    private IEnumerator UpdateTrackPositions(List<List<int>> tracks, MeshManager meshes, float duration, int? ignore = null)
    {
        var routines = new List<IEnumerator>();
        for (int slot = 0; slot < tracks.Count; slot++)
            for (int index = 0; index < tracks[slot].Count; index++)
                if (tracks[slot][index] != ignore)
                    routines.Add(SlideToPosition(meshes.GetDominoMeshById(tracks[slot][index]).transform, GetTrackPosition(index, slot, tracks.Count), duration, 0));
        yield return Together(routines.ToArray());
    }

    public void AddNewDominoForPlayer(Dictionary<int, Transform> hand, int id) { Run(AddNewDominoForPlayerRoutine(hand, id)); }
    public void ReturnDominoToPlayer(Dictionary<int, Transform> hand, int id) { Run(ReturnDominoToPlayerRoutine(hand, id)); }
    public IEnumerator AddNewDominoForPlayerRoutine(Dictionary<int, Transform> hand, int id) { yield return Introduce(hand, id, false); }
    public IEnumerator ReturnDominoToPlayerRoutine(Dictionary<int, Transform> hand, int id) { yield return Introduce(hand, id, true); }
    private IEnumerator Introduce(Dictionary<int, Transform> hand, int id, bool returning)
    {
        var target = hand[id];
        if (!returning) target.rotation = Quaternion.Euler(-90, 180, 180);
        yield return Together(
            SlideToPosition(target, new Vector3(0, 0, -0.015f), returning ? 0.25f : 1f, returning ? 0 : 0.4f),
            AnimationHelper.RotateOverSeconds(target, Quaternion.Euler(-90, 0, 180), returning ? 0.15f : 0.5f, returning ? 0.2f : 0.5f, SelectionEase));
        yield return new WaitForSeconds(returning ? 0.25f : 2f);
        var positions = GetHandPositions(hand.Count);
        var routines = new List<IEnumerator>();
        int index = 0;
        foreach (var item in hand)
        {
            var destination = positions[index++];
            routines.Add(item.Key == id
                ? AnimationHelper.MoveOverSecondsWithDepthReturn(item.Value, destination, 0.3f, 0.1f, -0.0135f, SelectionEase)
                : SlideToPosition(item.Value, destination, 0.3f, 0.1f));
        }
        yield return Together(routines.ToArray());
    }
    public IEnumerator UpdatePlayerPositions(Dictionary<int, Transform> hand)
    {
        var positions = GetHandPositions(hand.Count);
        var routines = new List<IEnumerator>();
        int index = 0;
        foreach (var item in hand)
            routines.Add(AnimationHelper.MoveOverSeconds(item.Value, positions[index++], RearrangePlayerDominoes));
        yield return Together(routines.ToArray());
    }
    public IEnumerator UpdateStationPositionsRoutine(List<List<int>> tracks, Dictionary<int, Transform> transforms)
    {
        var routines = new List<IEnumerator>();
        for (int slot = 0; slot < tracks.Count; slot++)
            for (int index = 0; index < tracks[slot].Count; index++)
                routines.Add(PlaceOnTrackRoutine(transforms[tracks[slot][index]], index, slot, tracks.Count));
        yield return Together(routines.ToArray());
    }
    public void UpdateStationPositions(List<List<int>> tracks, Dictionary<int, Transform> transforms, Action afterComplete = null)
    {
        Run(Complete(UpdateStationPositionsRoutine(tracks, transforms), afterComplete));
    }
    public Vector3 GetTrackLabelPosition(Vector3 lastPosition)
    {
        return lastPosition + new Vector3(PositionHelper.GetObjectDimensions(DominoPrefab).y / 2 + 0.003f, 0, 0);
    }
    public void MoveTrackLabelToEndOfTrack(GameObject label, Vector3 position)
    {
        Run(AnimationHelper.MoveOverSeconds(label.transform, position, DominoRotateToTrack));
    }
    public void UpdateTrackLabelPositions(Station station, Dictionary<int, GameObject> labels, Dictionary<int, Vector3> destinations)
    {
        for (int i = 0; i < station.Tracks.Count; i++)
            if (labels.TryGetValue(i, out var label) && destinations.TryGetValue(i, out var destination))
                MoveTrackLabelToEndOfTrack(label, destination);
    }
}
