using Assets.Scripts.Game;
using Assets.Scripts.Helpers;
using Assets.Scripts.Models;
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

    public Vector3 GetTrackLeftPosition(int trackIndex, int trackCount)
    {
        return new Vector3(GetTrackStartXPosition(), GetTrackYPosition(trackIndex, trackCount), 0);
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

    private float GetTrackXPosition(int trackObjectIndex, Vector3 trackStartPosition)
    {
        var objLength = PositionHelper.GetObjectDimensions(DominoPrefab);

        return trackStartPosition.x + (objLength.y * trackObjectIndex);
    }

    private float GetTrackYPosition(int trackIndex, int trackCount)
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

    public IEnumerator UpdateTrackPosition(GameObject currentObj, float duration, int trackCount)
    {
        for (int i = 0; i < trackCount; i++)
        {
            float currentYPosition = GetTrackYPosition(i, trackCount);
            StartCoroutine(SlideToPosition(currentObj.transform, new Vector3(GetTrackStartXPosition(), currentYPosition, 0), duration, 0));
        }

        yield return new WaitForSeconds(duration);
    }

    public IEnumerator AddDominoToTrack(Transform boxTransform, Vector3 destinationPosition, Action afterComplete = null)
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

    private IEnumerator SlideToPosition(Transform objectTransform, Vector3 endPos, float duration, float delay)
    {
        if (objectTransform.position == endPos)
        {
            // skip objects that are already in place
            yield break;
        }

        yield return StartCoroutine(AnimationHelper.MoveOverSeconds(objectTransform, endPos, SelectionEase));
    }

    public IEnumerator AddNewDominoAndUpdateTrackPositions(Transform gameObjectToAdd, DominoTracker dominoTracker, MeshManager meshManager, float duration, Action afterComplete = null)
    {
        var trackLeftPosition = GetTrackLeftPosition(dominoTracker.Station.Tracks.Count - 1, dominoTracker.Station.Tracks.Count);
        StartCoroutine(UpdateTrackPositions(dominoTracker, meshManager, duration));
        yield return StartCoroutine(AddDominoToTrack(gameObjectToAdd, trackLeftPosition, afterComplete));
    }

    private IEnumerator UpdateTrackPositions(DominoTracker dominoTracker, MeshManager meshManager, float duration)
    {
        // TODO: need to move all dominoes along with the track

        for (int i = 0; i < dominoTracker.Station.Tracks.Count; i++)
        {
            float currentYPosition = GetTrackYPosition(i, dominoTracker.Station.Tracks.Count);

            for (int j = 0; j < dominoTracker.Station.Tracks[i].DominoIds.Count; j++)
            {
                // TODO: stagger or ease the sliding to look cool?

                float currentXPosition = GetTrackXPosition(j, GetTrackLeftPosition(i, dominoTracker.Station.Tracks.Count));
                int currentDominoId = dominoTracker.Station.Tracks[i].DominoIds[j];
                Transform transform = meshManager.GetDominoMeshById(currentDominoId).transform;
                StartCoroutine(SlideToPosition(transform, new Vector3(currentXPosition, currentYPosition, 0), duration, 0));
            }


        }

        yield return new WaitForSeconds(duration);
    }
}
