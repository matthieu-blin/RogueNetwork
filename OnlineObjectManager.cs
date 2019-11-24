using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class OnlineObjectManager : MonoBehaviour
{
    private static OnlineObjectManager instance = null;
    public GameObject[] m_DynamicObject = new GameObject[0];
    private List<GameObject> m_staticObject = new List<GameObject>();
    private uint m_IDGenerator = 0;
    private List<OnlineBehavior> m_onlineBehaviors = new List<OnlineBehavior>();
    public static OnlineObjectManager Instance
    {
        get
        {
            return instance;
        }
    }
    public void Awake()
    {
        instance = this;
        DontDestroyOnLoad(this);
    }

    public ulong ComputeDeterministID(uint _id)
    {
        return (1 << 32) + _id;
    }
    // Start is called before the first frame update
    void Start()
    {
        OnlineManager.Instance.RegisterHandler((byte)OnlineProtocol.Handler.ONLINE_OBJECT, RecvOnlineObject);
        OnlineManager.Instance.RegisterHandler((byte)OnlineProtocol.Handler.ONLINE_OBJECT_UPDATE, RecvOnlineBehaviorUpdate);
    }

    internal void RegisterOnlineBehavior(OnlineBehavior onlineBehavior)
    {
        m_onlineBehaviors.Add(onlineBehavior);
    }

    // Update is called once per frame
    void Update()
    {
       foreach(var ob in m_onlineBehaviors)
        {
            if(ob.NeedUpdate())
            {
                SendOnlineBehaviorUpdate(ob); 
            }
        }
    }
    public GameObject Instanciate(string _name)
    {
        if (!OnlineManager.Instance.IsHost())
            return null;
        GameObject obj = Array.Find(m_DynamicObject, go => go.name == _name);
        GameObject newObj = Instantiate(obj);
        newObj.GetComponent<OnlineIdentity>().m_srcName = _name;
        newObj.GetComponent<OnlineIdentity>().m_type = OnlineIdentity.Type.Dynamic;
        return newObj;
    }
    public GameObject Instanciate(GameObject _prefab, Vector3 _pos, Quaternion _rot, uint _playerID = 0)
    {
        if (!OnlineManager.Instance.IsHost())
            return null;
        GameObject obj = Array.Find(m_DynamicObject, go => go.name == _prefab.name);
        GameObject newObj = Instantiate(obj, _pos, _rot);
        newObj.GetComponent<OnlineIdentity>().m_srcName = _prefab.name;
        newObj.GetComponent<OnlineIdentity>().m_type = OnlineIdentity.Type.Dynamic;
        newObj.GetComponent<OnlineIdentity>().m_localPlayerAuthority = _playerID;
        return newObj;
    }


    //gameobject should have been instanciate using OnlineObjectManager.Instanciate
    public void Spawn(GameObject _obj)
    {
        if (!OnlineManager.Instance.IsHost())
            return;
        m_IDGenerator++;
        _obj.GetComponent<OnlineIdentity>().m_uid = m_IDGenerator;
        SendOnlineObject(_obj);


    }
    public void RegisterStaticObject(GameObject _obj)
    {
        m_staticObject.Add(_obj);
    }
 
    private void RecvOnlineObject(byte[] _msg)
    {
        using (MemoryStream m = new MemoryStream(_msg))
        {
            using (BinaryReader r = new BinaryReader(m))
            {
                byte type = r.ReadByte();
                string name = r.ReadString();
                ulong uid = r.ReadUInt64();

                switch((OnlineIdentity.Type)type)
                {
                    case OnlineIdentity.Type.Static:
                        {
                            //search for current parent path
                            GameObject obj = m_staticObject.Find(go => go.name == name);
                            obj.GetComponent<OnlineIdentity>().m_uid = uid;
                            obj.SetActive(true);
                            break;
                        }
                    case OnlineIdentity.Type.Dynamic:
                        {
                            GameObject obj = Array.Find(m_DynamicObject, go => go.name == name);
                            GameObject newObj = Instantiate(obj);
                            newObj.GetComponent<OnlineIdentity>().m_srcName = name;
                            newObj.GetComponent<OnlineIdentity>().m_uid = uid;
                            break;
                        }

                }
            }
        }
    }
    private void SendOnlineObject(GameObject _obj)
    {
        using (MemoryStream m = new MemoryStream())
        {
            using (BinaryWriter w = new BinaryWriter(m))
            {
                w.Write((byte)_obj.GetComponent<OnlineIdentity>().m_type);
                w.Write(_obj.GetComponent<OnlineIdentity>().m_srcName);
                w.Write(_obj.GetComponent<OnlineIdentity>().m_uid);
                OnlineManager.Instance.SendMessage((byte)OnlineProtocol.Handler.ONLINE_OBJECT, m.GetBuffer());
            }
        }
    }
    private void RecvOnlineBehaviorUpdate(byte[] _msg)
    {
        using (MemoryStream m = new MemoryStream(_msg))
        {
            using (BinaryReader r = new BinaryReader(m))
            {
                ulong uid = r.ReadUInt64();
                var obj = m_onlineBehaviors.Find(ob => ob.m_onlineIdentity != null && ob.m_onlineIdentity.m_uid == uid);
                //note : in case of parallel creation, we could receive msg before instanciation
                //this should be buffered instead
                if(obj != null)
                    obj.Read(r);
            }
        }
    }
    private void SendOnlineBehaviorUpdate(OnlineBehavior _obj)
    {
        using (MemoryStream m = new MemoryStream())
        {
            using (BinaryWriter w = new BinaryWriter(m))
            {
                w.Write(_obj.m_onlineIdentity.m_uid);
                _obj.Write(w);
                OnlineManager.Instance.SendMessage((byte)OnlineProtocol.Handler.ONLINE_OBJECT_UPDATE, m.GetBuffer());
            }
        }
    }
}
