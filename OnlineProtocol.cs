using UnityEngine;
using UnityEditor;

public class OnlineProtocol 
{
    public enum Handler
    {
        PLAYERS_UPDATE = 0,
        LOBBY_GO ,
        ONLINE_OBJECT,
        ONLINE_OBJECT_UPDATE,
        GAME_PROTOCOL_START, //user can use id from this
    }
}