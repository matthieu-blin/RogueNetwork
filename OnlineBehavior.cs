using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using System.Linq;
using System.IO;

[AttributeUsage( AttributeTargets.Field , AllowMultiple = true)]
public class Sync : Attribute{}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class CMD : Attribute { }
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class RPC : Attribute { }
public abstract class OnlineBehavior : MonoBehaviour
{

    public OnlineIdentity m_onlineIdentity;
    private FieldInfo[] m_syncedFields;
    public delegate void FieldReader(FieldInfo _f, BinaryReader _r);
    private Dictionary<Type, FieldReader> m_FieldReaders = new Dictionary<Type, FieldReader>();
    public delegate void FieldWriter(FieldInfo _f, BinaryWriter _w);
    private Dictionary<Type, FieldWriter> m_FieldWriters = new Dictionary<Type, FieldWriter>();

    private MethodInfo[] m_cmds;
    private List<string> m_pendingcmds = new List<string>();
    private MethodInfo[] m_rpcs;
    private List<string> m_pendingrpcs = new List<string>();
    public void Init()
    {

        m_syncedFields = GetType().GetFields(BindingFlags.NonPublic
             | BindingFlags.Public
             | BindingFlags.FlattenHierarchy
             | BindingFlags.Instance
             | BindingFlags.Static)
             .Where(prop => Attribute.IsDefined(prop, typeof(Sync))).ToArray();

        m_rpcs = GetType().GetMethods(BindingFlags.NonPublic
            | BindingFlags.Public
            | BindingFlags.FlattenHierarchy
            | BindingFlags.Instance
            | BindingFlags.Static)
            .Where(prop => Attribute.IsDefined(prop, typeof(RPC))).ToArray();

        m_cmds = GetType().GetMethods(BindingFlags.NonPublic
            | BindingFlags.Public
            | BindingFlags.FlattenHierarchy
            | BindingFlags.Instance
            | BindingFlags.Static)
            .Where(prop => Attribute.IsDefined(prop, typeof(CMD))).ToArray();

        OnlineObjectManager.Instance.RegisterOnlineBehavior(this);

        //find OnlineIdentity Component
        m_FieldReaders.Add(typeof(Vector3), ReadVector3);
        m_FieldWriters.Add(typeof(Vector3), WriteVector3);
        m_FieldReaders.Add(typeof(Quaternion), ReadQuaternion);
        m_FieldWriters.Add(typeof(Quaternion), WriteQuaternion);
        m_FieldReaders.Add(typeof(int), ReadInt);
        m_FieldWriters.Add(typeof(int), WriteInt);

    }

    private void OnDestroy()
    {
        OnlineObjectManager.Instance.UnregisterOnlineBehavior(this);
    }
    public bool HasAuthority()
    {
        if (m_onlineIdentity == null)
            return false;
        return m_onlineIdentity.HasAuthority();
    }
    public bool NeedUpdateFields()
    {
        if (m_onlineIdentity == null)
            m_onlineIdentity = GetComponent<OnlineIdentity>();

        if (m_onlineIdentity == null)
            return false;
        return m_onlineIdentity.HasAuthority();
    }

       public void Write(BinaryWriter w)
    {
        foreach (var field in m_syncedFields)
        {
            Type type = field.FieldType;
            FieldWriter fw;
            if (m_FieldWriters.TryGetValue(type, out fw))
            {
                fw(field, w);
            }
        }
    }

    public void Read(BinaryReader r)
    {
        if (!HasAuthority())
        {
            foreach (var field in m_syncedFields)
            {
                Type type = field.FieldType;
                FieldReader fr;
                if (m_FieldReaders.TryGetValue(type, out fr))
                {
                    fr(field, r);
                }
            }
        }
        if (OnlineManager.Instance.IsHost())
        {
            ReadCMDs(r);
        }
        else
        {
            ReadRPCs(r);
        }
    }
    public void Call(string _fncName)
    {
        var rpc = Array.Find(m_rpcs, f => f.Name == _fncName);
        if (rpc != null)
        {
            if(OnlineManager.Instance.IsHost())
            {
                m_pendingrpcs.Add(_fncName);
                rpc.Invoke(this, new object[0]);
            }
        }
        var cmd = Array.Find(m_cmds, f => f.Name == _fncName);
        if (cmd != null)
        {
            if (HasAuthority())
            {
                if (OnlineManager.Instance.IsHost())
                {
                    cmd.Invoke(this, new object[0]);
                }
                else
                {
                    m_pendingcmds.Add(_fncName);
                }
            }
        }
    }

    public bool NeedUpdateMethods()
    {
        if (m_onlineIdentity == null)
            m_onlineIdentity = GetComponent<OnlineIdentity>();

        if (m_onlineIdentity == null)
            return false;
        return m_pendingcmds.Count > 0 || m_pendingrpcs.Count > 0;
    }
    public void WriteRPCs(BinaryWriter w)
    {
        w.Write(m_pendingrpcs.Count);
        foreach(var rpc in m_pendingrpcs)
        {
            w.Write(rpc);
        }
        m_pendingrpcs.Clear();
     }
    public void WriteCMDs(BinaryWriter w)
    {
        w.Write(m_pendingcmds.Count);
        foreach (var cmd in m_pendingcmds)
        {
            w.Write(cmd);
        }
        m_pendingcmds.Clear();
    }

    public void ReadRPCs(BinaryReader r)
    {
        int rpcsCount = r.ReadInt32();
        for(int i = 0; i < rpcsCount; ++i)
        {
            string name = r.ReadString();
            var rpc = Array.Find(m_rpcs, f => f.Name == name);
            if (rpc != null)
            {
                if (OnlineManager.Instance.IsHost())
                {
                    rpc.Invoke(this, new object[0]);
                }
            }
        }
    }
    public void ReadCMDs(BinaryReader r)
    {
        int cmdsCount = r.ReadInt32();
        for (int i = 0; i < cmdsCount; ++i)
        {
            string name = r.ReadString();
            var cmd = Array.Find(m_cmds, f => f.Name == name);
            if (cmd != null)
            {
                if (OnlineManager.Instance.IsHost())
                {
                    cmd.Invoke(this, new object[0]);
                }
            }
        }
    }
    private void WriteVector3(FieldInfo _f, BinaryWriter _w)
    {
        var v = (Vector3)_f.GetValue(this);
        _w.Write(v.x);
        _w.Write(v.y);
        _w.Write(v.z);
    }
    private void ReadVector3(FieldInfo _f, BinaryReader _r)
    {
        var v = (Vector3)_f.GetValue(this);
        v.x = _r.ReadSingle(); 
        v.y = _r.ReadSingle(); 
        v.z = _r.ReadSingle(); 
        _f.SetValue(this, v);
    }
    private void WriteQuaternion(FieldInfo _f, BinaryWriter _w)
    {
        var q = (Quaternion)_f.GetValue(this);
        _w.Write(q.x);
        _w.Write(q.y);
        _w.Write(q.z);
        _w.Write(q.w);
    }
    private void ReadQuaternion(FieldInfo _f, BinaryReader _r)
    {
        var q = (Quaternion)_f.GetValue(this);
        q.x = _r.ReadSingle();
        q.y = _r.ReadSingle();
        q.z = _r.ReadSingle();
        q.w = _r.ReadSingle();
        _f.SetValue(this, q);
    }
    private void WriteInt(FieldInfo _f , BinaryWriter _w)
    {
        var t = (int)_f.GetValue(this);
        _w.Write(t);
    }

 
    
    private void ReadInt(FieldInfo _f, BinaryReader _r)
    {
        _f.SetValue(this, _r.ReadInt32());
    }
}
