using Assets.Scripts.Models;
using System.Collections.Generic;

public class StationManager
{
    public List<Station> Stations { get; private set; }

    public StationManager()
    {
        Stations = new List<Station>();
    }

    public void AddStation(Station station)
    {
        Stations.Add(station);
    }

    public void RemoveStation(Station station)
    {
        Stations.Remove(station);
    }

    public Station GetStation(int index)
    {
        return Stations[index];
    }

    public int GetStationCount()
    {
        return Stations.Count;
    }

    public void ClearStations()
    {
        Stations.Clear();
    }
}

