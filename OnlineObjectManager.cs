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

    // Start is called before the first frame update
    void Start()
    {
        OnlineManager.Instance.RegisterHandler((byte)OnlineProtocol.Handler.ONLINE_OBJECT, RecvOnlineObject);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public GameObject Instanciate(string _name)
    {
        if (!OnlineManager.Instance.IsHost())
            return null;
        GameObject obj = Array.Find(m_DynamicObject, go => go.name == _name);
        GameObject newObj = Instantiate(obj);
        newObj.GetComponent<OnlineIdentity>().m_srcName = _name;
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
}
