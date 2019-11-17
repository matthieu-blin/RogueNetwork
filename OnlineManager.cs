using Assets;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using UnityEngine;

public class OnlineManager : MonoBehaviour
{
    private static OnlineManager instance = null;
    public static OnlineManager Instance
    {
        get
        {
            return instance;
        }
    }


    public string HostIP = "127.0.0.1";
    public int HostPort = 25000;

    bool m_Connected = false;
    bool m_host = false;

    public delegate void GameMessageCallback(byte[] _msg);

    Dictionary<byte,GameMessageCallback> m_MessageCallbacksHandler;

  
    public void Awake()
    {
        instance = this;
        DontDestroyOnLoad(this);
    }
    public void Start()
    {
        m_tcp = new TCPApi();
        m_tcp.Log = Log;
        m_tcp.OnMessageReceived = OnGameMessage;
        m_MessageCallbacksHandler = new Dictionary<byte, GameMessageCallback>();
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
    public void Log(string txt)
    {
        Debug.Log(txt);
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
            using(BinaryReader r = new BinaryReader(m))
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
        return 0;
    }
}
