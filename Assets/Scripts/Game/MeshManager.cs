using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshManager : MonoBehaviour
{
    [SerializeField] private GameObject playerDominoPrefab = null;
    [SerializeField] private GameObject tableDominoPrefab = null;

    private Dictionary<int, GameObject> dominoObjects = new Dictionary<int, GameObject>();   // TODO: now both clients know about each other's dominoes. Feels unsure.
    private Quaternion dominoRotation = Quaternion.Euler(new Vector3(-90, 0, 180));

    private int engineDominoId;

    public GameObject GetDominoMeshById(int id)
    {
        return dominoObjects[id];
    }

    public GameObject GetEngineDomino() => dominoObjects[engineDominoId];

    public GameObject CreateEngineDomino(GameObject prefab, DominoEntity info, Vector3 position)
    {
        var engineDomino = CreateDominoFromInfo(prefab, info, position, PurposeType.Engine);
        return engineDomino;
    }

    public GameObject CreatePlayerDominoFromInfo(DominoEntity info, Vector3 position, PurposeType purpose) => 
        CreateDominoFromInfo(playerDominoPrefab, info, position, purpose);

    private GameObject CreateDominoFromInfo(GameObject prefab, DominoEntity info, Vector3 position, PurposeType purpose)
    {
        var newDomino = Instantiate(prefab, position, dominoRotation);
        newDomino.name = info.ID.ToString();

        var dom = newDomino.GetComponent<DominoEntityUI>();
        dom.SetDominoInfo(info);

        dominoObjects.Add(info.ID, newDomino);

        return newDomino;
    }
}
