﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI   ;
using UnityEngine.SceneManagement;

/// <summary>
/// Simple Lobby that can wait and display some players 
/// Host will decide when the game should start
/// Every player will automatically load the given next scene then (scene index of build settings)
/// </summary>
public class SimpleLobby : MonoBehaviour
{
    public int m_sceneOnGo;
    public GameObject[] m_players = new GameObject[4];
    public uint m_MinimumPlayers = 2;
    [SerializeField]
    Button m_Go = null;
    // Start is called before the first frame update
    void Start()
    {
        OnlineManager.Instance.RegisterHandler((byte)OnlineProtocol.Handler.LOBBY_GO, Recv);
    }

    // Update is called once per frame
    void Update()
    {
        if (OnlineManager.Instance.IsConnected())
        {
            int playerIndex = 0;
            foreach (OnlinePlayerManager.Player player in OnlinePlayerManager.Instance.m_players)
            {
                m_players[playerIndex].GetComponent<Text>().text = "player " + player.m_ID.ToString() + " " + player.m_endpoint.ToString();
                playerIndex++;
                if (playerIndex >= m_players.Length)
                    break;
            }
        }

        m_Go.interactable = !OnlineManager.Instance.IsConnected() || OnlineManager.Instance.IsHost();
        
    }
    public void GoPressed()
    {
        if (OnlineManager.Instance.IsHost())
        {
            if (OnlinePlayerManager.Instance.m_players.Count >= m_MinimumPlayers)
            {
                Send();
                SceneManager.LoadScene(m_sceneOnGo, LoadSceneMode.Single);
            }
        }
    }

    private void Recv(byte[] _msg)
    {
        SceneManager.LoadScene(m_sceneOnGo, LoadSceneMode.Single);
    }
    private void Send()
    {
        OnlineManager.Instance.SendMessage((byte)OnlineProtocol.Handler.LOBBY_GO,new  byte[0]);

    }
}
