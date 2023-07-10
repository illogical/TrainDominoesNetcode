using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DominoManager
{
    public int PlayerDominoCount = 12;  // TODO: move this up to the GameManager level since multiple scripts need it

    public int EngineIndex = 0;  // index (of engines) for the current round (job for RoundManager?)    
    public int? SelectedPlayerDominoID;

    private Dictionary<int, DominoEntity> allDominoes = new Dictionary<int, DominoEntity>();
    private List<int> engineIndices = new List<int>();  // list of domino indices that have the same top and bottom numbers. Should RoundManger own this?    
    private Dictionary<ulong, List<int>> playerDominoIndices = new Dictionary<ulong, List<int>>();
    private List<int> remainingDominoIndices = new List<int>();
    private List<int> tableDominos = new List<int>(); // TODO: needs a data type for chaining together dominos? and keeping track of N trains. StationManager should own this and should be accessed via GameplayManager

    // TODO: should track all engines in order to know which is next
    // TODO: keep a list of the dominoes on the table then the StationManager can just track the IDs of the dominoes in this list.

    private const int maxDots = 12;

    // TEMP
    public int GetDominoesRemainingCount() => remainingDominoIndices.Count;

    public void SelectDomino(int dominoId)
    {
        SelectedPlayerDominoID = dominoId;
    }

    public void ClearSelectedDomino()
    {
        SelectedPlayerDominoID = null;
    }

    /// <summary>
    /// Creates 91 dominoes based upon 12 point max train domino set
    /// </summary>
    public void CreateDominoSet()
    {
        var index = 0;
        for (int i = 0; i < maxDots + 1; i++)
        {
            for (int j = i; j < maxDots + 1; j++)
            {
                allDominoes.Add(index, createDomino(i, j, index));
                remainingDominoIndices.Add(index);

                if (i == j)
                {
                    // track index for each double
                    engineIndices.Add(index);
                }

                index++;
            }
        }

        engineIndices.Reverse();
    }

    public DominoEntity PickUpDomino()
    {
        var newDomino = GetDominoFromBonePile();
        return newDomino;
    }

    public void AddDominoToHand(ulong clientId, int dominoID)
    {
        if(!playerDominoIndices.ContainsKey(clientId))
        {
            playerDominoIndices.Add(clientId, new List<int>());
        }

        playerDominoIndices[clientId].Add(dominoID);
    }

    /// <summary>
    /// Used for picking up a player's dominoes at the start of a game
    /// </summary>
    /// <param name="count"></param>
    public void PickUpDominoes(ulong clientId, int count)
    {
        for (int i = 0; i < count; i++)
        {
            AddDominoToHand(clientId, PickUpDomino().ID);
        }
    }

    public DominoEntity GetDominoFromBonePile()
    {
        int randomDominoIndex = Random.Range(0, remainingDominoIndices.Count);
        int dominoID = remainingDominoIndices[randomDominoIndex];
        var domino = allDominoes[dominoID];

        remainingDominoIndices.RemoveAt(randomDominoIndex);
        return domino;
    }

    public List<DominoEntity> GetDominoesByIDs(List<int> ids)
    {
        var dominoes = new List<DominoEntity>();

        foreach (int id in ids)
        {
            dominoes.Add(GetDominoByID(id));
        }

        return dominoes;
    }

    public DominoEntity GetDominoByID(int id)
    {
        return allDominoes[id];
    }

    public DominoEntity GetEngine(int engineIndex)
    {
        // use previous engine if attempting to use one that is no longer available
        if (engineIndex >= engineIndices.Count || !remainingDominoIndices.Contains(engineIndices[engineIndex]))
        {
            return allDominoes[engineIndices[EngineIndex]];
        }

        EngineIndex = engineIndex;

        var engineDomino = allDominoes[engineIndices[EngineIndex]];
        remainingDominoIndices.Remove(engineDomino.ID);
        return engineDomino;
    }

    public DominoEntity GetEngine()
    {
        return allDominoes[engineIndices[EngineIndex]];
    }

    public int GetEngineCount()
    {
        return engineIndices.Count;
    }

    public List<DominoEntity> GetPlayerDominoes(ulong clientId)
    {
        return GetDominoesByIDs(playerDominoIndices[clientId]);
    }

    public List<DominoEntity> GetDominoesByIds(List<int> dominoIds)
    {
        var dominoes = new List<DominoEntity>(dominoIds.Count);
        foreach (int id in dominoIds)
        {
            dominoes.Add(allDominoes[id]);
        }

        return dominoes;
    }

    public bool IsPlayerDomino(ulong clientId, int dominoId)
    {
        return playerDominoIndices[clientId].Contains(dominoId);
    }

    public void RemovePlayerDomino(ulong clientId, int dominoId)
    {
        playerDominoIndices[clientId].Remove(dominoId);
    }

    private DominoEntity createDomino(int topScore, int bottomScore, int index)
    {
        return new DominoEntity()
        {
            TopScore = topScore,
            BottomScore = bottomScore,
            ID = index
        };
    }
}
