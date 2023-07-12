using Assets.Scripts.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.UI.Image;

public class LayoutManager : MonoBehaviour
{
    [Range(0, 1)]
    [SerializeField] private float BottomYOffset = 0.01f;
    [SerializeField] private float BottomSideMargin = 0.01f;
    [SerializeField] private float SelectionRaiseAmount = 0.02f;
    [Range(0, 2)]
    [SerializeField] private float TrackMargin = 0f; // distance from left side box
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
    public AnimationDefinition SelectionEase;

    private Camera mainCamera;
    private float playerYPosition = 0;


    private void Awake()
    {
        mainCamera = Camera.main;
    }

    public void PlacePlayerDominoes(List<GameObject> playerDominoes)
    {
        PositionHelper.LayoutAcrossAndUnderScreen(playerDominoes, mainCamera, BottomSideMargin);  //place them outside of the camera's view to allow them to slide in

        var objectSize = PositionHelper.GetObjectDimensions(playerDominoes[0]);
        var positions = PositionHelper.GetLayoutAcrossScreen(objectSize, mainCamera, playerDominoes.Count, BottomSideMargin);

        playerYPosition = positions[0].y;

        for (int i = 0; i < playerDominoes.Count; i++)
        {
            var domino = playerDominoes[i];
            var mover = domino.GetComponent<Mover>();

            var staggerDelay = PlayerDominoSlideIn.Delay * i;

            StartCoroutine(mover.MoveOverSeconds(positions[i], PlayerDominoSlideIn.Duration, staggerDelay, PlayerDominoSlideIn.Curve));
        }
    }

    public void PlaceEngine(GameObject engine, Action afterComplete = null)
    {
        var destination = GetEnginePosition(engine);

        var mover = engine.GetComponent<Mover>();
        StartCoroutine(mover.MoveOverSeconds(destination, EngineSlideIn, afterComplete));
    }

    public void SelectDomino(GameObject domino)
    {
        var destination = new Vector3(domino.transform.position.x, playerYPosition + SelectionRaiseAmount, domino.transform.position.z);

        var mover = domino.GetComponent<Mover>();
        StartCoroutine(mover.MoveOverSeconds(destination, PlayerDominoSelection));
    }

    public void DeselectDomino(GameObject domino)
    {
        var destination = new Vector3(domino.transform.position.x, playerYPosition, domino.transform.position.z);

        var mover = domino.GetComponent<Mover>();
        StartCoroutine(mover.MoveOverSeconds(destination, PlayerDominoDeselection));
    }

    private Vector3 GetEnginePosition(GameObject engine)
    {
        var objectSize = PositionHelper.GetObjectDimensions(engine);
        return PositionHelper.GetScreenLeftCenterPositionForObject(objectSize, mainCamera, 0);
    }

    public void PlaceDominoOnTrack(GameObject domino, int trackDominoIndex, Action afterComplete = null)
    {
        var mover = domino.GetComponent<Mover>();

        var destination = new Vector3(GetTrackStartXPosition() + 0.05f, 0, 0);

        // TODO: Move up to track y position then move over to the X
        // TODO: even better, spring past the x position and come back for a little sample of juice
        StartCoroutine(mover.RotateOverSeconds(Quaternion.Euler(0, 90, 90), DominoRotateToTrack));
        StartCoroutine(mover.MoveOverSeconds(destination, DominoSlideToTrack, afterComplete));   // TODO: new animation definition for adding domino to track
    }

    //public Vector3 GetNextTrackPosition(GameObject domino)
    //{
    //    var yStartPosition = PositionHelper.GetScreenLeftCenter(mainCamera).y;
    //    var xStartPosition = GetEnginePosition(domino).x;
    //}

    public float GetTrackStartXPosition()
    {
        var dominoSize = PositionHelper.GetObjectDimensions(DominoPrefab);
        Vector3 leftCenterPos = PositionHelper.GetScreenLeftCenterPositionForObject(dominoSize, mainCamera, 0);
        var xOffset = 0f;

        float centerObjectSize = dominoSize.x + dominoSize.x / 2;
        float marginFromLeftCenter = centerObjectSize + xOffset;

        return leftCenterPos.x + marginFromLeftCenter;
    }

    public float GetTrackYPosition(int trackIndex, int trackCount)
    {
        var objLength = PositionHelper.GetObjectDimensions(DominoPrefab).x;
        var leftSide = PositionHelper.GetScreenLeftCenter(mainCamera);

        float totalLength = ((trackCount - 1) * TrackMargin) + ((trackCount - 1) * objLength);
        float centerOffset = totalLength / 2;

        // the is the current center
        var centerPos = leftSide.y + CenterLeftYOffset;
        var trackIndexLength = trackIndex * objLength + (TrackMargin * trackIndex);

        return centerPos + trackIndexLength - centerOffset;
    }

    internal IEnumerator AddNewDominoAndUpdateTrackPositions(GameObject currentObj, Vector3 trackLeftPosition, int trackCount, float trackSlideDuration, Action afterComplete = null)
    {
        StartCoroutine(UpdateTrackPositions(currentObj, trackSlideDuration, trackCount));
        yield return StartCoroutine(AddDominoToTrack(currentObj.transform, trackLeftPosition, afterComplete));
    }

    IEnumerator UpdateTrackPositions(GameObject currentObj, float duration, int trackCount)
    {
        for (int i = 0; i < trackCount; i++)
        {
            float currentYPosition = GetTrackYPosition(i, trackCount);
            StartCoroutine(SlideToPosition(currentObj.transform, new Vector3(GetTrackStartXPosition(), currentYPosition, 0), duration, 0));
        }

        yield return new WaitForSeconds(duration);
    }

    private IEnumerator AddDominoToTrack(Transform boxTransform, Vector3 destinationPosition, Action afterComplete = null)
    {
        float rotationDuration = 0.2f;
        float rotationDelay = 0.25f;

        // TODO: need to know if the domino needs to be flipped
        StartCoroutine(AnimationHelper.RotateOverSeconds(boxTransform, Quaternion.Euler(0, -90, -90), rotationDuration, rotationDelay, SelectionEase));
        yield return StartCoroutine(AnimationHelper.MoveOverSeconds(boxTransform, destinationPosition, SelectionEase));

        if (afterComplete != null)
        {
            afterComplete();
        }
    }

    public IEnumerator SlideToPosition(Transform objectTransform, Vector3 endPos, float duration, float delay)
    {
        if (objectTransform.position == endPos)
        {
            // skip objects that are already in place
            yield break;
        }

        yield return StartCoroutine(AnimationHelper.MoveOverSeconds(objectTransform, endPos, SelectionEase));
    }
}
