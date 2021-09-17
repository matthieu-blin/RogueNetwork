using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

/// <summary>
///  Singleton Component, don't destroy on load
/// Require OnlineManager instanciated, and should be udpated after it
/// This component associate an unique ID to an Online Player 
/// You can retrieve the specific endpoint for a given player 
///
/// </summary>
public class OnlinePlayerManager : MonoBehaviour
{
    public static OnlinePlayerManager Instance { get; private set; } = null;
    public void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(this);
    }

    public struct Player
    {
        public EndPoint m_endpoint;
        public uint m_ID;

    }
    public List<Player> m_players = new List<Player>();
    private List<EndPoint> m_clients = new List<EndPoint>();
    private uint m_playerIndex = 0;

    public uint m_localPlayerID = 0;
    // Start is called before the first frame update
    void Start()
    {
        OnlineManager.Instance.RegisterHandler((byte)OnlineProtocol.Handler.PLAYERS_UPDATE, Recv);
    }

    // Update is called once per frame
    void Update()
    {
        if (!OnlineManager.Instance.IsConnected())
            return;
        if (OnlineManager.Instance.IsHost())
        {
            OnlineManager.Instance.FetchClients(ref m_clients);
            //check player removed
            bool changed = m_players.RemoveAll(x => !m_clients.Exists(c => c.Equals(x.m_endpoint))) > 0;
            //add new
            var newClients = m_clients.FindAll(c => !m_players.Exists(p => p.m_endpoint.Equals(c)));
            changed |= newClients.Count > 0;
            foreach (var newclient in newClients)
            {
                Player player = new Player();
                player.m_endpoint = newclient;
                player.m_ID = m_playerIndex;
                m_playerIndex++;
                m_players.Add(player);
            }
            if (changed)
            {
                //send players to everyone
                Send();
            }
        }
    }

    public int GetPlayerCount()
    {
        return m_players.Count;
    }

    private void Recv(byte[] _msg)
    {
        using (MemoryStream m = new MemoryStream(_msg))
        {
            using (BinaryReader w = new BinaryReader(m))
            {
                m_players.Clear();
                int count = w.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    Player player = new Player();
                    BinaryFormatter formatter = new BinaryFormatter();
                    try
                    {
                        player.m_endpoint = (EndPoint)formatter.Deserialize(m);
                    }
                    catch (SerializationException e)
                    {
                        OnlineManager.Log("Failed to deserialize. Reason: " + e.Message);
                    }
                    player.m_ID = w.ReadUInt32();
                    m_players.Add(player);
                }
            }
        }
        //Compute Local player ID
        int index = m_players.FindIndex(p => p.m_endpoint.Equals(OnlineManager.Instance.GetLocalEndpoint()));
        if (index >= 0)
        {
            m_localPlayerID = m_players[index].m_ID;
        }

    }
    private void Send()
    {
        using (MemoryStream m = new MemoryStream())
        {
            using (BinaryWriter w = new BinaryWriter(m))
            {
                w.Write(m_players.Count);
                foreach (var player in m_players)
                {
                    BinaryFormatter formatter = new BinaryFormatter();
                    try
                    {
                        formatter.Serialize(m, player.m_endpoint);
                    }
                    catch (SerializationException e)
                    {
                        OnlineManager.Log("Failed to serialize. Reason: " + e.Message);
                    }
                    w.Write(player.m_ID);
                }
                OnlineManager.Instance.SendMessage((byte)OnlineProtocol.Handler.PLAYERS_UPDATE, m.GetBuffer());
            }
        }
    }
}
