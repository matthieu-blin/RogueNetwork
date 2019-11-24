using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using System.Linq;
using System.IO;

[AttributeUsage( AttributeTargets.Field , AllowMultiple = true)]
public class Sync : Attribute
{

}

public abstract class OnlineBehavior : MonoBehaviour
{

    public OnlineIdentity m_onlineIdentity;
    private FieldInfo[] m_syncedFields;
    public delegate void FieldReader(FieldInfo _f,BinaryReader _r);
    private Dictionary<Type, FieldReader> m_FieldReaders = new Dictionary<Type, FieldReader>();
    public delegate void FieldWriter(FieldInfo _f,BinaryWriter _w);
    private Dictionary<Type, FieldWriter> m_FieldWriters = new Dictionary<Type, FieldWriter>();
    public void Init()
    {

        m_syncedFields = GetType().GetFields(BindingFlags.NonPublic
             | BindingFlags.Public
             | BindingFlags.FlattenHierarchy
             | BindingFlags.Instance
             | BindingFlags.Static)
             .Where(prop => Attribute.IsDefined(prop, typeof(Sync))).ToArray();

        OnlineObjectManager.Instance.RegisterOnlineBehavior(this);

        //find OnlineIdentity Component
        m_FieldReaders.Add(typeof(Vector3), ReadVector3);
        m_FieldWriters.Add(typeof(Vector3), WriteVector3);
        m_FieldReaders.Add(typeof(Quaternion), ReadQuaternion);
        m_FieldWriters.Add(typeof(Quaternion), WriteQuaternion);
        m_FieldReaders.Add(typeof(int), ReadInt);
        m_FieldWriters.Add(typeof(int), WriteInt);

    }

    public void Update()
    {
        if (HasAuthority())
            LocalUpdate();
        else
            RemoteUpdate();
    }
    public abstract void LocalUpdate();
    public abstract void RemoteUpdate();

    public bool HasAuthority()
    {
        if (m_onlineIdentity == null)
            return false;
        return m_onlineIdentity.HasAuthority();
    }
    public bool NeedUpdate()
    {
        if (m_onlineIdentity == null)
            m_onlineIdentity = GetComponent<OnlineIdentity>();

        if (m_onlineIdentity == null)
            return false;
        return m_onlineIdentity.HasAuthority();
    }

    public void Write(BinaryWriter w)
    {
        foreach(var field in m_syncedFields)
        {
            Type type = field.FieldType;
            FieldWriter fw;
            if( m_FieldWriters.TryGetValue(type, out fw))
            {
                fw(field, w);
            }
        }
    }

    public void Read(BinaryReader r)
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
