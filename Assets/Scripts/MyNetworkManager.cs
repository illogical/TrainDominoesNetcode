using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class MyNetworkManager : NetworkManager
{
    public List<DominoPlayer> Players = new List<DominoPlayer>();
    public DominoPlayer LocalPlayer { get; set; }
}
