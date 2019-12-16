using UnityEngine;
using UnityEditor;

public class OnlineProtocol 
{
    public enum Handler
    {
        PLAYERS_UPDATE = 0,
        LOBBY_GO ,
        ONLINE_OBJECT,
        ONLINE_OBJECT_DESTROY,
        ONLINE_OBJECT_FIELDS,
        ONLINE_OBJECT_METHODS,
        GAME_PROTOCOL_START, //user can use id from this
    }
}