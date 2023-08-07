using Assets.Scripts.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
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
    public AnimationDefinition SelectionEase;

    private Camera mainCamera;
    private float playerYPosition = 0;


    private void Awake()
    {
        mainCamera = Camera.main;
    }

    public void PlaceInitialPlayerDominoes(List<GameObject> playerDominoes)
    {
        PositionHelper.LayoutAcrossAndUnderScreen(playerDominoes, mainCamera, BottomSideMargin);  //place them outside of the camera's view to allow them to slide in

        var objectSize = PositionHelper.GetObjectDimensions(DominoPrefab);

        // TODO: this throws a NAN error for x position when one domino is passed
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
        for (int i = 0; i < trackCount; i++)
        {
            float currentYPosition = GetTrackYPosition(i, trackCount);
            StartCoroutine(SlideToPosition(currentObj.transform, new Vector3(GetTrackStartXPosition(), currentYPosition, 0), duration, 0));
        }

        yield return new WaitForSeconds(duration);
    }

    private IEnumerator AddDominoToTrack(Transform dominoTransform, int trackObjectIndex, int trackIndex, int trackCount, Action afterComplete = null)
    {
        float rotationDuration = 0.2f;
        float rotationDelay = 0.25f;

        // TODO: need to know if the domino needs to be flipped
        StartCoroutine(AnimationHelper.RotateOverSeconds(dominoTransform, Quaternion.Euler(0, -90, -90), rotationDuration, rotationDelay, SelectionEase));
        yield return StartCoroutine(AnimationHelper.MoveOverSeconds(dominoTransform, GetTrackPosition(trackObjectIndex, trackIndex, trackCount), SelectionEase));

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

    public IEnumerator AddNewDominoAndUpdateTrackPositions(Transform gameObjectToAdd, int addedDominoId, List<List<int>> tracksWithDomininoIds, MeshManager meshManager, float duration, Action afterComplete = null)
    {
        StartCoroutine(UpdateTrackPositions(tracksWithDomininoIds, meshManager, duration, addedDominoId));
        yield return StartCoroutine(AddDominoToTrack(gameObjectToAdd, 0, tracksWithDomininoIds.Count - 1, tracksWithDomininoIds.Count, afterComplete));
    }

    public IEnumerator AddDominoAndUpdateTrackPositions(Transform gameObjectToAdd, List<List<int>> tracksWithDomininoIds, MeshManager meshManager, int trackIndex, float duration, Action afterComplete = null)
    {
        //StartCoroutine(UpdateTrackPositions(tracksWithDomininoIds, meshManager, duration, addedDominoId));
        yield return StartCoroutine(AddDominoToTrack(gameObjectToAdd, tracksWithDomininoIds[trackIndex].Count - 1, trackIndex, tracksWithDomininoIds.Count, afterComplete));
    }

    private IEnumerator UpdateTrackPositions(List<List<int>> tracksWithDomininoIds, MeshManager meshManager, float duration, int? addedDominoId = null)
    {
        // TODO: need to move all dominoes along with the track

        for (int i = 0; i < tracksWithDomininoIds.Count; i++)
        {
            float currentYPosition = GetTrackYPosition(i, tracksWithDomininoIds.Count);

            for (int j = 0; j < tracksWithDomininoIds[i].Count; j++)
            {
                // TODO: stagger or ease the sliding to look cool?

                float currentXPosition = GetTrackXPosition(j, i, tracksWithDomininoIds.Count);
                int currentDominoId = tracksWithDomininoIds[i][j];
                if(currentDominoId == addedDominoId)
                {
                    // ignoring the domino being added to the track (so it can have its own separate animation)
                    continue;
                }

                Transform transform = meshManager.GetDominoMeshById(currentDominoId).transform;
                StartCoroutine(SlideToPosition(transform, new Vector3(currentXPosition, currentYPosition, 0), duration, 0));
            }


        }

        yield return new WaitForSeconds(duration);
    }

    public void AddNewDominoForPlayer(Dictionary<int, Transform> playerDominoes, int incomingDominoId)
    {
        var objLength = PositionHelper.GetObjectDimensions(DominoPrefab);
        var positionOffScreen = PositionHelper.GetScreenRightCenter(mainCamera) + new Vector3(objLength.x, 0, 0);

        //newObj.transform.rotation = Quaternion.Euler(new Vector3(0, 180, 0));   // start flipped over?

        // fly in and rotate
        StartCoroutine(SlideAndRotateToCenterThenAddToBottom(playerDominoes, incomingDominoId, () => Debug.Log("Intro animation complete")));
    }
    
    public void ReturnDominoToPlayer(Dictionary<int, Transform> playerDominoes, int incomingDominoId)
    {
        // TODO: lift domino from track (closer to camera)
        // TODO: move horizontally to its final x position
        // TODO: slide vertically back into the hand
        // TODO: make space in the hand for the domino to return to
        
        // rotate then fly in
        StartCoroutine(SlideAndRotateToCenterThenAddToBottom(playerDominoes, incomingDominoId, () => Debug.Log("Return domino animation complete")));
    }

    private IEnumerator SlideAndRotateToCenterThenAddToBottom(Dictionary<int, Transform> playerDominoes, int incomingDominoId, Action afterComplete = null)
    {
        float showDuration = 2f; // seconds that the box is displayed in the center before moving into its bottom position
        float beginAnimationDelay = 0.4f;   // delay before beginning this animation
        float rotationDelay = 0.1f;        // additional delay before rotation animation begins
        float slideAnimationDuration = 1f;
        float rotationAnimationDuration = 0.5f;
        float depth = -0.015f; // z position

        var newDominoTransform = playerDominoes[incomingDominoId];
        newDominoTransform.rotation = Quaternion.Euler(new Vector3(-90, 180, 180));   // start flipped over

        StartCoroutine(SlideToPosition(newDominoTransform, new Vector3(0, 0, depth), slideAnimationDuration, beginAnimationDelay));
        yield return StartCoroutine(
            AnimationHelper.RotateOverSeconds(
                newDominoTransform.transform,
                Quaternion.Euler(-90, 0, 180),
                rotationAnimationDuration,
                beginAnimationDelay + rotationDelay,
                SelectionEase
                )
            );

        yield return new WaitForSeconds(showDuration);

        yield return StartCoroutine(UpdateHorizontalPositionsWithDepth(playerDominoes, incomingDominoId, depth * 0.9f));

        if (afterComplete != null)
        {
            afterComplete();
        }
    }


    private IEnumerator UpdateHorizontalPositionsWithDepth(Dictionary<int, Transform> playerDominoes, int incomingDominoId, float depthFromFinalPosition)
    {
        float animationDuration = 0.3f;
        float delayBeforeAnimation = 0.1f;
        float totalAnimationTime = delayBeforeAnimation + animationDuration;
        float horizontalSideMargin = 0f;

        var objectDimensions = PositionHelper.GetObjectDimensions(DominoPrefab);
        Vector2 screenSize = PositionHelper.GetScreenSize(mainCamera);
        var positionY = PositionHelper.GetPlayerDominoYPosition(objectDimensions, mainCamera);

        int i = 0;
        foreach(int dominoId in playerDominoes.Keys)
        {
            float endXPos = PositionHelper.GetCenterPositionByIndex(screenSize.x, objectDimensions.x, i++, playerDominoes.Count, horizontalSideMargin);

            if (dominoId == incomingDominoId)
            {
                // moves the new domino to its final place then moves it in the Z position to its final spot
                StartCoroutine(AnimationHelper.MoveOverSecondsWithDepthReturn(playerDominoes[dominoId].transform, new Vector3(endXPos, positionY, 0), animationDuration, delayBeforeAnimation, depthFromFinalPosition, SelectionEase));
            }
            else
            {
                StartCoroutine(SlideToPosition(playerDominoes[dominoId].transform, new Vector3(endXPos, positionY, 0), animationDuration, delayBeforeAnimation));
            }
        }

        yield return new WaitForSeconds(totalAnimationTime);
    }

    public void UpdateStationPositions(List<List<int>> trackDominoIds, Dictionary<int,Transform> dominoTransforms)
    {
        // TODO: how to know when a domino is flipped? Might need full DominoEntity here instead of just an ID

        StartCoroutine(MoveDominoesToStation(trackDominoIds, dominoTransforms));
    }

    private IEnumerator MoveDominoesToStation(List<List<int>> trackDominoIds, Dictionary<int,Transform> dominoTransforms)
    {
        float staggerDeloy = 0.25f;
        var staggerWait = new WaitForSeconds(staggerDeloy);

        for (int trackIndex = 0; trackIndex < trackDominoIds.Count; trackIndex++)
        {
            for (int dominoIndex = 0; dominoIndex < trackDominoIds[trackIndex].Count; dominoIndex++)
            {
                int dominoId = trackDominoIds[trackIndex][dominoIndex];
                // TODO: slow this animation down (make it configurable)
                StartCoroutine(AddDominoToTrack(dominoTransforms[dominoId], dominoIndex, trackIndex, trackDominoIds.Count));
            }
        }

        yield return staggerWait;
    }
}
