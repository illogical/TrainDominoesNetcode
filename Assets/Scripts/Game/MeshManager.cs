using Assets.Scripts.Game;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MeshManager : MonoBehaviour
{
    [SerializeField] private GameObject playerDominoPrefab = null;
    [SerializeField] private TrackEndMessage trackEndMessagePrefab = null;

    private Dictionary<int, GameObject> dominoObjects = new Dictionary<int, GameObject>();
    private Dictionary<ulong, TrackEndMessage> trackEndMessages = new Dictionary<ulong, TrackEndMessage>();
    private Quaternion dominoRotation = Quaternion.Euler(new Vector3(-90, 0, 180));

    private int engineDominoId = -1;

    public GameObject GetDominoMeshById(int id)
    {
        if(!dominoObjects.ContainsKey(id))
        {
            return null;
        }

        return dominoObjects[id];
    }

    public Dictionary<int, Transform> GetDominoTransformsByIds(int[] dominoIds)
    {
        var dominoTransforms = new Dictionary<int, Transform>();
        foreach (int domino in dominoIds)
        {
            dominoTransforms.Add(domino, GetDominoMeshById(domino).transform);
        }

        return dominoTransforms;
    }

    public Dictionary<int, Transform> GetDominoMeshesByEntities(DominoEntity[] dominoEntities)
    {
        var playerDominoes = new Dictionary<int, Transform>();
        foreach (var domino in dominoEntities)
        {
            // use the mesh if it already exists, otherwise create it
            var dominoMesh = GetDominoMeshById(domino.ID);
            if (dominoMesh == null)
            {
                dominoMesh = CreatePlayerDominoFromInfo(domino, new Vector3(0, 1, 0), PurposeType.Player);

            }
            playerDominoes.Add(domino.ID, dominoMesh.transform);
        }

        return playerDominoes;
    }

    public GameObject GetEngineDomino() => dominoObjects[engineDominoId];

    public GameObject CreateEngineDomino(DominoEntity info, Vector3 position)
    {
        var engineDomino = CreateDominoFromInfo(playerDominoPrefab, info, position, PurposeType.Engine);
        engineDominoId = info.ID;
        return engineDomino;
    }

    public GameObject CreatePlayerDominoFromInfo(DominoEntity info, Vector3 position, PurposeType purpose) => 
        CreateDominoFromInfo(playerDominoPrefab, info, position, purpose);

    public void ResetDominoMeshes()
    {
        foreach (int dominoId in dominoObjects.Keys)
        {
            // murder all of the dominoes from this last round
            Destroy(dominoObjects[dominoId].gameObject);
        }
        
        dominoObjects.Clear();
    }

    private GameObject CreateDominoFromInfo(GameObject prefab, DominoEntity info, Vector3 position, PurposeType purpose)
    {
        var newDomino = Instantiate(prefab, position, dominoRotation);
        newDomino.name = info.ID.ToString();

        var dom = newDomino.GetComponent<DominoEntityUI>();
        dom.SetDominoInfo(info);

        dominoObjects.Add(info.ID, newDomino);

        return newDomino;
    }

    public GameObject SetTrackMessageForPlayer(ulong clientId, Vector3 position, string text)
    {
        if (!trackEndMessages.ContainsKey(clientId))
        {
            trackEndMessages.Add(clientId, Instantiate(trackEndMessagePrefab, position, dominoRotation));
        }
        trackEndMessages[clientId].SetText(text);

        return trackEndMessages[clientId].gameObject;
    }
    public GameObject GetTrackMessageForPlayer(ulong clientId) => trackEndMessages[clientId].gameObject;
    
    public void UpdateDomino(DominoEntity dominoInfo)
    {
        GameObject dominoObj = GetDominoMeshById(dominoInfo.ID);
        var dom = dominoObj.GetComponent<DominoEntityUI>();
        dom.SetDominoInfo(dominoInfo);
    }
}
