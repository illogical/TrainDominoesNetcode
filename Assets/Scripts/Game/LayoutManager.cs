using Assets.Scripts.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.Game.Navigation;
using UnityEngine;

/// <summary>
/// Board and hand layout. Track and engine positions are local to the navigation roots (VIEW-001), so panning or
/// zooming during an animation carries a moving tile along with its row. World-space queries remain as wrappers.
/// </summary>
public class LayoutManager : MonoBehaviour
{
    [Range(0, 1)]
    [SerializeField] private float BottomYOffset = 0.01f;
    [SerializeField] private float BottomSideMargin = 0.01f;
    [SerializeField] private float SelectionRaiseAmount = 0.02f;
    [Range(0, 2)]
    [SerializeField] private float TrackMargin = 0f; // distance between rows
    [Range(0, 1.5f)]
    [SerializeField] private float CenterLeftYOffset = 0f; // vertical offset of the stacked rows
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

    public static readonly Quaternion HandRotation = Quaternion.Euler(-90, 0, 180);
    public static readonly Quaternion TrackRotation = Quaternion.Euler(0, -90, -90);
    /// <summary>Board units of space after a row's last tile, for its label.</summary>
    private const float EndPaddingTiles = 3f;

    private Camera mainCamera;
    private Vector3 tileSize, baseScale = Vector3.one;
    private readonly List<Coroutine> motion = new List<Coroutine>();

    public BoardNavigator Navigator { get; private set; }
    public Vector3 TileSize => tileSize;
    public float SelectionRaise => SelectionRaiseAmount;
    public float HandSideMargin => BottomSideMargin;
    /// <summary>Distance between neighbouring rows in board units.</summary>
    public float RowSpacing => tileSize.x + TrackMargin;
    public float HandLocalY => PositionHelper.GetPlayerDominoYPosition(tileSize, mainCamera);

    private void Awake()
    {
        mainCamera = Camera.main;
        tileSize = PositionHelper.GetObjectDimensions(DominoPrefab);
        baseScale = DominoPrefab.transform.localScale;
        Navigator = gameObject.AddComponent<BoardNavigator>();
        Navigator.Initialize(this, mainCamera);
    }

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

    private static void Attach(Transform tile, Transform parent)
    {
        if (tile.parent != parent) tile.SetParent(parent, true);
    }

    // ---- Local board layout (board units; one unit is one world unit at zoom 1) ----

    public float RowLocalY(int slot, int count)
    {
        float centerOffset = (count - 1) * RowSpacing / 2;
        return CenterLeftYOffset + slot * RowSpacing - centerOffset;
    }

    /// <summary>The engine's centre: half a tile in from the board's left edge.</summary>
    public Vector3 EngineLocalPosition => new Vector3(tileSize.x / 2, 0, 0);

    /// <summary>Centre of the tile at <paramref name="index"/> along a row, after the engine and a one-and-a-half tile gap.</summary>
    public float TrackLocalX(int index) => 2f * tileSize.x + tileSize.y * index;

    /// <summary>From the board's left edge to the right edge of the last tile (or where the first would go).</summary>
    public float RowContentLength(int tiles) => TrackLocalX(Mathf.Max(1, tiles) - 1) + tileSize.y / 2;

    /// <summary>Space shown after a row's last tile, for its label.</summary>
    public float RowTrailingSpace => tileSize.y * EndPaddingTiles;

    public Vector3 TrackLocalPosition(int index) => new Vector3(TrackLocalX(index), 0, 0);

    public Vector3 LabelLocalPosition(int tiles) => tiles <= 0 ? TrackLocalPosition(0) : TrackLocalPosition(tiles - 1) + new Vector3(tileSize.y / 2 + 0.003f, 0, 0);

    public Transform Row(int slot, int count)
    {
        Navigator.SetRowCount(count);
        return Navigator.Row(slot);
    }

    // ---- World-space queries (wrappers through the current roots) ----

    public Vector3 GetEnginePosition(GameObject engine) => Navigator.BoardRoot.TransformPoint(EngineLocalPosition);
    public Vector3 GetTrackLeftPosition(int trackIndex, int trackCount) => Row(trackIndex, trackCount).TransformPoint(TrackLocalPosition(0));
    public Vector3 GetTrackPosition(int trackDominoIndex, int trackIndex, int trackCount) => Row(trackIndex, trackCount).TransformPoint(TrackLocalPosition(trackDominoIndex));

    public List<Vector3> GetHandPositions(int count)
    {
        var positions = new List<Vector3>();
        foreach (var local in GetHandLocalPositions(count)) positions.Add(Navigator.HandRoot.TransformPoint(local));
        return positions;
    }

    /// <summary>Hand positions under the hand root. Overfull hands keep a minimum spacing and scroll (VIEW-001/U4).</summary>
    public List<Vector3> GetHandLocalPositions(int count)
    {
        Navigator.ConfigureHand(count);
        var positions = new List<Vector3>();
        float y = HandLocalY;
        for (int i = 0; i < count; i++) positions.Add(new Vector3(Navigator.Hand.LocalX(i), y, 0));
        return positions;
    }

    // ---- Immediate placement (snap to the latest view) ----

    public void SnapEngine(Transform engine)
    {
        Attach(engine, Navigator.BoardRoot);
        engine.localPosition = EngineLocalPosition; engine.localRotation = HandRotation; engine.localScale = baseScale;
    }

    public void SnapToTrack(Transform tile, int index, int slot, int count)
    {
        Attach(tile, Row(slot, count));
        tile.localPosition = TrackLocalPosition(index); tile.localRotation = TrackRotation; tile.localScale = baseScale;
    }

    public void SnapToHand(Transform tile, Vector3 local)
    {
        Attach(tile, Navigator.HandRoot);
        tile.localPosition = local; tile.localRotation = HandRotation; tile.localScale = baseScale;
    }

    public void PlaceLabel(GameObject label, int slot, int count, int tiles)
    {
        var row = Row(slot, count);
        if (label.transform.parent != row) label.transform.SetParent(row, false);
        label.transform.localPosition = LabelLocalPosition(tiles);
    }

    // ---- Animated placement ----

    public IEnumerator PlaceInitialPlayerDominoesRoutine(List<GameObject> dominoes)
    {
        if (dominoes.Count == 0) yield break;
        var positions = GetHandLocalPositions(dominoes.Count);
        float under = PositionHelper.GetPlayerDominoYPosition(tileSize, mainCamera) - tileSize.y * 1.5f;
        var routines = new List<IEnumerator>();
        for (int i = 0; i < dominoes.Count; i++)
        {
            var tile = dominoes[i].transform;
            SnapToHand(tile, new Vector3(positions[i].x, under, 0));
            routines.Add(AnimationHelper.MoveLocalOverSeconds(tile, positions[i], null, null,
                PlayerDominoSlideIn != null ? PlayerDominoSlideIn.Duration : 0,
                PlayerDominoSlideIn != null ? PlayerDominoSlideIn.Delay * i : 0, PlayerDominoSlideIn));
        }
        yield return Together(routines.ToArray());
    }

    public IEnumerator PlaceEngineRoutine(GameObject engine)
    {
        Attach(engine.transform, Navigator.BoardRoot);
        yield return AnimationHelper.MoveLocalOverSeconds(engine.transform, EngineLocalPosition, HandRotation, baseScale,
            EngineSlideIn != null ? EngineSlideIn.Duration : 0, EngineSlideIn != null ? EngineSlideIn.Delay : 0, EngineSlideIn);
    }

    public IEnumerator SelectDominoRoutine(GameObject domino, bool selected)
    {
        var tile = domino.transform;
        if (tile.parent != Navigator.HandRoot) yield break; // only hand tiles lift
        var destination = new Vector3(tile.localPosition.x, HandLocalY + (selected ? SelectionRaiseAmount : 0), tile.localPosition.z);
        yield return AnimationHelper.MoveLocalOverSeconds(tile, destination, null, selected ? PlayerDominoSelection : PlayerDominoDeselection);
    }

    public IEnumerator PlaceOnTrackRoutine(Transform domino, int index, int slot, int count)
    {
        // Reparent first, keeping the world pose; the move then targets the row's local space.
        Attach(domino, Row(slot, count));
        yield return Together(
            AnimationHelper.RotateLocalOverSeconds(domino, TrackRotation,
                DominoRotateToTrack != null ? DominoRotateToTrack.Duration : 0,
                DominoRotateToTrack != null ? DominoRotateToTrack.Delay : 0, DominoRotateToTrack),
            AnimationHelper.MoveLocalOverSeconds(domino, TrackLocalPosition(index), baseScale, DominoSlideToTrack));
    }

    private IEnumerator SlideLocal(Transform target, Vector3 end, float duration, float delay, Vector3? scale = null)
    {
        yield return AnimationHelper.MoveLocalOverSeconds(target, end, null, scale, duration, delay, SelectionEase);
    }

    public IEnumerator AddNewDominoForPlayerRoutine(Dictionary<int, Transform> hand, int id) { yield return Introduce(hand, id, false); }
    public IEnumerator ReturnDominoToPlayerRoutine(Dictionary<int, Transform> hand, int id) { yield return Introduce(hand, id, true); }

    private IEnumerator Introduce(Dictionary<int, Transform> hand, int id, bool returning)
    {
        var target = hand[id];
        Attach(target, Navigator.HandRoot);
        if (!returning) target.localRotation = Quaternion.Euler(-90, 180, 180);
        // The reveal point is the centre of the screen, close to the camera.
        var reveal = Navigator.HandRoot.InverseTransformPoint(new Vector3(0, 0, -0.015f));
        yield return Together(
            SlideLocal(target, reveal, returning ? 0.25f : 1f, returning ? 0 : 0.4f, baseScale),
            AnimationHelper.RotateLocalOverSeconds(target, HandRotation, returning ? 0.15f : 0.5f, returning ? 0.2f : 0.5f, SelectionEase));
        yield return new WaitForSeconds(returning ? 0.25f : 2f);
        var positions = GetHandLocalPositions(hand.Count);
        var routines = new List<IEnumerator>();
        int index = 0;
        foreach (var item in hand)
        {
            var destination = positions[index++];
            Attach(item.Value, Navigator.HandRoot);
            routines.Add(item.Key == id ? ReturnWithDepth(item.Value, destination) : SlideLocal(item.Value, destination, 0.3f, 0.1f, baseScale));
        }
        yield return Together(routines.ToArray());
    }

    private IEnumerator ReturnWithDepth(Transform tile, Vector3 destination)
    {
        yield return SlideLocal(tile, destination + new Vector3(0, 0, -0.0135f), 0.3f, 0.1f, baseScale);
        yield return SlideLocal(tile, destination, 0.15f, 0);
    }

    public IEnumerator UpdatePlayerPositions(Dictionary<int, Transform> hand)
    {
        var positions = GetHandLocalPositions(hand.Count);
        var routines = new List<IEnumerator>();
        int index = 0;
        foreach (var item in hand)
        {
            Attach(item.Value, Navigator.HandRoot);
            routines.Add(AnimationHelper.MoveLocalOverSeconds(item.Value, positions[index++], baseScale, RearrangePlayerDominoes));
        }
        yield return Together(routines.ToArray());
    }
}
