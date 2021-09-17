using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
///  Singleton Component, don't destroy on load
/// Require OnlineManager and OnlinePlayerManager instanciated, and should be udpated after them
/// Add automatic replication features to your component scripting
/// @see OnlineBehavior for more information about field and RPC synchronisation
///
/// How to use Online Spawning
/// There's 2 types of Online spawning behavior :
/// -Static : your objects are spawned by the unity scene system :
///     -They should have a fixed OnlineIdentity (static or determinist) to be able to communicate between source/replica
///     -They will be active ONLY when host load them (and host MUST LOAD SCENE FIRST)
/// -Dynamic : your objects will be instanciated by script (using prefab)
///     -Your prefab should have a Dynamic OnlineIdentity
///     -Your prefab should be registered in the DynamicObject List via editor
///     -Instead of using GameObject.Instanciate, use OnlineObject.Instanciate then use OnlineObject.Spawn 
/// </summary>
public class OnlineObjectManager : MonoBehaviour
{
    private uint m_IDGenerator = 0;
    public GameObject[] m_DynamicObject = new GameObject[0];
    private List<GameObject> m_DynamicObjectInstances = new List<GameObject>();
    private List<GameObject> m_StaticObject = new List<GameObject>();
    private List<OnlineBehavior> m_OnlineBehaviors = new List<OnlineBehavior>();
    //Singleton
    public static OnlineObjectManager Instance { get; private set; } = null;
    public void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(this);
    }

    // Start is called before the first frame update
    void Start()
    {
        OnlineManager.Instance.RegisterHandler((byte)OnlineProtocol.Handler.ONLINE_OBJECT, RecvOnlineObject);
        OnlineManager.Instance.RegisterHandler((byte)OnlineProtocol.Handler.ONLINE_OBJECT_DESTROY, RecvOnlineObjectDestroy);
        OnlineManager.Instance.RegisterHandler((byte)OnlineProtocol.Handler.ONLINE_OBJECT_FIELDS, RecvOnlineBehaviorFieldsUpdate);
        OnlineManager.Instance.RegisterHandler((byte)OnlineProtocol.Handler.ONLINE_OBJECT_METHODS, RecvOnlineBehaviorMethodsUpdate);
    }

    internal void RegisterOnlineBehavior(OnlineBehavior onlineBehavior)
    {
        var sameObject = m_OnlineBehaviors.FindAll(ob => ob.gameObject == onlineBehavior.gameObject);
        if (sameObject != null)
            onlineBehavior.m_index = sameObject.Count;
        else
            onlineBehavior.m_index = 0;
        m_OnlineBehaviors.Add(onlineBehavior);
    }

    internal void UnregisterOnlineBehavior(OnlineBehavior onlineBehavior)
    {
        m_OnlineBehaviors.Remove(onlineBehavior);
    }
    // Update is called once per frame
    void Update()
    {
        foreach (var ob in m_OnlineBehaviors)
        {
            if (ob.NeedUpdateFields())
            {
                SendOnlineBehaviorFieldsUpdate(ob);
            }
            if (ob.NeedUpdateMethods())
            {
                SendOnlineBehaviorMethodsUpdate(ob);
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

   //Spawn will be automatically called on gameObject with OnlineId set to Static
    //Object should have been created using OnlineObject.Instanciate
    //do not call this except if you know exactly what you're doing
    public void Spawn(GameObject _obj)
    {
        if (!OnlineManager.Instance.IsHost())
            return;
        m_IDGenerator++;
        _obj.GetComponent<OnlineIdentity>().m_uid = m_IDGenerator;
        SendOnlineObject(_obj);
    }

    //this is handled by OnlineIdentity
    //Despawn will be automatically called on gameObject with OnlineId destruction
    //do not call this except if you know exactly what you're doing
    public void Despawn(GameObject _obj)
    {
        if (!OnlineManager.Instance.IsHost())
            return;
        SendOnlineObjectDestroy(_obj);
    }
    //this is handled by OnlineIdentity
    //do not call this except if you know exactly what you're doing
    public void RegisterStaticObject(GameObject _obj)
    {
        m_IDGenerator++;
        _obj.GetComponent<OnlineIdentity>().m_uid = m_IDGenerator;
        m_StaticObject.Add(_obj);
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
                uint playerID = r.ReadUInt32();

                switch ((OnlineIdentity.Type)type)
                {
                    case OnlineIdentity.Type.Static:
                        {
                            //search for current parent path
                            GameObject obj = m_StaticObject.Find(go => go.name == name);
                            obj.GetComponent<OnlineIdentity>().m_uid = uid;
                            obj.GetComponent<OnlineIdentity>().m_localPlayerAuthority = playerID;
                            obj.SetActive(true);
                            break;
                        }
                    case OnlineIdentity.Type.Dynamic:
                        {
                            Vector3 position = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                            Quaternion rotation = new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                            GameObject obj = Array.Find(m_DynamicObject, go => go.name == name);
                            GameObject newObj = Instantiate(obj, position, rotation);
                            newObj.GetComponent<OnlineIdentity>().m_srcName = name;
                            newObj.GetComponent<OnlineIdentity>().m_uid = uid;
                            newObj.GetComponent<OnlineIdentity>().m_localPlayerAuthority = playerID;
                            m_DynamicObjectInstances.Add(newObj);
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
                w.Write(_obj.GetComponent<OnlineIdentity>().m_localPlayerAuthority);
                if (_obj.GetComponent<OnlineIdentity>().m_type == OnlineIdentity.Type.Dynamic)
                {
                    w.Write(_obj.transform.position.x);
                    w.Write(_obj.transform.position.y);
                    w.Write(_obj.transform.position.z);
                    w.Write(_obj.transform.rotation.x);
                    w.Write(_obj.transform.rotation.y);
                    w.Write(_obj.transform.rotation.z);
                    w.Write(_obj.transform.rotation.w);
                }
                OnlineManager.Instance.SendMessage((byte)OnlineProtocol.Handler.ONLINE_OBJECT, m.GetBuffer());
            }
        }
    }

    private void RecvOnlineObjectDestroy(byte[] _msg)
    {
        using (MemoryStream m = new MemoryStream(_msg))
        {
            using (BinaryReader r = new BinaryReader(m))
            {
                byte type = r.ReadByte();
                ulong uid = r.ReadUInt64();

                switch((OnlineIdentity.Type)type)
                {
                    case OnlineIdentity.Type.Static:
                        {
                            break;
                        }
                    case OnlineIdentity.Type.Dynamic:
                        {
                            GameObject obj = m_DynamicObjectInstances.Find(go => go.GetComponent<OnlineIdentity>().m_uid == uid);
                            m_DynamicObjectInstances.Remove(obj);
                            Destroy(obj);
                            break;
                        }

                }
            }
        }
    }
    private void SendOnlineObjectDestroy(GameObject _obj)
    { 
         using (MemoryStream m = new MemoryStream())
        {
            using (BinaryWriter w = new BinaryWriter(m))
            {
                w.Write((byte)_obj.GetComponent<OnlineIdentity>().m_type);
                w.Write(_obj.GetComponent<OnlineIdentity>().m_uid);
                OnlineManager.Instance.SendMessage((byte)OnlineProtocol.Handler.ONLINE_OBJECT_DESTROY, m.GetBuffer());
            }
        }
    }


    private void RecvOnlineBehaviorFieldsUpdate(byte[] _msg)
    {
        using (MemoryStream m = new MemoryStream(_msg))
        {
            using (BinaryReader r = new BinaryReader(m))
            {
                ulong uid = r.ReadUInt64();
                int index = r.ReadInt32();
                var obj = m_OnlineBehaviors.Find(ob => ob.m_onlineIdentity != null && ob.m_onlineIdentity.m_uid == uid && ob.m_index == index);
                //note : in case of parallel creation, we could receive msg before instanciation
                //this should be buffered instead
                if(obj != null)
                    obj.Read(r);
            }
        }
    }
    private void SendOnlineBehaviorFieldsUpdate(OnlineBehavior _obj)
    {
        using (MemoryStream m = new MemoryStream())
        {
            using (BinaryWriter w = new BinaryWriter(m))
            {
                w.Write(_obj.m_onlineIdentity.m_uid);
                w.Write(_obj.m_index);
                _obj.Write(w);
                OnlineManager.Instance.SendMessage((byte)OnlineProtocol.Handler.ONLINE_OBJECT_FIELDS, m.GetBuffer());
            }
        }
    }


    private void RecvOnlineBehaviorMethodsUpdate(byte[] _msg)
    {
        using (MemoryStream m = new MemoryStream(_msg))
        {
            using (BinaryReader r = new BinaryReader(m))
            {
                ulong uid = r.ReadUInt64();
                int index = r.ReadInt32();
                var obj = m_OnlineBehaviors.Find(ob => ob.m_onlineIdentity != null && ob.m_onlineIdentity.m_uid == uid && ob.m_index == index);
                //note : in case of parallel creation, we could receive msg before instanciation
                //this should be buffered instead
                if (obj != null)
                {
                    obj.ReadCMDs(r);
                    obj.ReadRPCs(r);
                }
            }
        }
    }
    private void SendOnlineBehaviorMethodsUpdate(OnlineBehavior _obj)
    {
        using (MemoryStream m = new MemoryStream())
        {
            using (BinaryWriter w = new BinaryWriter(m))
            {
                w.Write(_obj.m_onlineIdentity.m_uid);
                w.Write(_obj.m_index);
                _obj.WriteCMDs(w);
                _obj.WriteRPCs(w);
                OnlineManager.Instance.SendMessage((byte)OnlineProtocol.Handler.ONLINE_OBJECT_METHODS, m.GetBuffer());
            }
        }
    }
}

//static shortcut to OnlineObjectManager method
public class OnlineObject
{
    public static GameObject Instanciate(string _name) { return OnlineObjectManager.Instance.Instanciate(_name); }
    public static GameObject Instanciate(GameObject _prefab, Vector3 _pos, Quaternion _rot, uint _playerID = 0)
    {
        return OnlineObjectManager.Instance.Instanciate(_prefab, _pos, _rot, _playerID);
    }
    /// <summary>
    /// Call this function Only on a Dynamic online object, after a successfull OnlineObject.Instanciate
    /// </summary>
    public static void Spawn(GameObject _obj) {  OnlineObjectManager.Instance.Spawn(_obj); }
    //very stupid determinist id for now
    //TODO : should check other ID to avoid collision
    public static ulong ComputeDeterministID(uint _id)
    {
        return (1 << 32) + _id;
    }


 }
    
