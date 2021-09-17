using Assets;
using System.Collections.Generic;
using System.IO;
using System.Net;
using UnityEngine;

/// <summary>
/// Singleton Component, don't destroy on load
/// Main component needed to enable online in your game
/// Basic architecture used is Host/Client 
/// Set your host adress using editor then call StartHost or StartClient to establish connection
/// 
/// The main purpose of this class is to pair a message type with message data to write your own game protocol easily
/// Register a message handler with a specific (unique) type
/// when someone send a message with this type your handler will be called.
/// 
/// Note : your receive handlers will be called when this component is Updated to avoid thread issue
/// </summary>
public class OnlineManager : MonoBehaviour
{

    //todo rajouter un log du nombre de messages traités en une frame


    public string HostIP = "127.0.0.1";
    public int HostPort = 25000;

    bool m_Connected = false;
    bool m_host = false;

    public delegate void GameMessageCallback(byte[] _msg);

    Dictionary<byte,GameMessageCallback> m_MessageCallbacksHandler;

    //Singleton
    public static OnlineManager Instance { get; private set; } = null;

    public void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(this);
    }
    public void Start()
    {
        m_tcp = new TCPApi();
        m_tcp.Log = Log;
        m_tcp.OnMessageReceived = OnGameMessage;
        m_MessageCallbacksHandler = new Dictionary<byte, GameMessageCallback>();
    }
    public void OnDisable()
    {
        m_tcp.End();
    }

    private TCPApi m_tcp;

    public void StartHost()
    {
        m_host = true;
        m_tcp.Listen(HostIP, HostPort);
        m_Connected = true;
    }

    public bool IsHost()
    {
        return m_host;
    }

    public EndPoint GetLocalEndpoint()
    {
        return m_tcp.GetLocalEndPoint();
    }
    public void FetchClients(ref List<EndPoint> _clients)
    {
        _clients.Clear();
        m_tcp.FetchClients(_clients);
    }

    public void StartClient()
    {
        m_Connected = m_tcp.Connect(HostIP, HostPort);
    }

    public bool IsConnected()
    {
        return m_Connected;
    }

    public void RegisterHandler(byte _handlerType, GameMessageCallback _cb)
    {
        GameMessageCallback cb;
        if (m_MessageCallbacksHandler.TryGetValue(_handlerType, out cb))
        {
            m_MessageCallbacksHandler[_handlerType] = cb + _cb;
        }
        else
        {
            m_MessageCallbacksHandler.Add(_handlerType, _cb);
        }

        
    }

    // Update is called once per frame
    void Update()
    {
        if (!IsConnected())
            return;

        m_tcp.Process();
    }
    public static void Log(string txt)
    {
        Debug.LogError(txt);
    }
    public void SendMessage(byte _handlerType, byte[] _msg)
    {
        using (MemoryStream m = new MemoryStream())
        {
            using (BinaryWriter w = new BinaryWriter(m))
            {
                w.Write(_handlerType);
                w.Write(_msg.Length);
                w.Write(_msg);
                m_tcp.SendMessage(m.ToArray());
            }
        }
    }

    public int OnGameMessage(TCPApi.Message msg)
    {
        using (MemoryStream m = new MemoryStream(msg.m_message))
        {
            using (BinaryReader r = new BinaryReader(m))
            {
                while (r.BaseStream.Position != r.BaseStream.Length)
                {
                    byte handlerType = r.ReadByte();
                    int size = r.ReadInt32();
                    byte[] buffer = r.ReadBytes(size);
                    GameMessageCallback cb;
                    if (m_MessageCallbacksHandler.TryGetValue(handlerType, out cb))
                    {
                        cb(buffer);
                    }
                    else
                    {
                        Log("Unhandled Message");
                    }
                }
            }
        }
        return 0;
    }
}
