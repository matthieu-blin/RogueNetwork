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

public class OnlineBehavior : MonoBehaviour
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
        m_FieldReaders.Add(typeof(Transform), ReadTransform);
        m_FieldReaders.Add(typeof(int), ReadInt);
        m_FieldWriters.Add(typeof(Transform), WriteTransform);
        m_FieldWriters.Add(typeof(int), WriteInt);
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
    private void WriteTransform(FieldInfo _f, BinaryWriter _w)
    {
        var t = (Transform)_f.GetValue(this);
        _w.Write(t.position.x);
        _w.Write(t.position.y);
        _w.Write(t.position.z);
        _w.Write(t.rotation.x);
        _w.Write(t.rotation.y);
        _w.Write(t.rotation.z);
        _w.Write(t.rotation.w);

    }
    private void WriteInt(FieldInfo _f , BinaryWriter _w)
    {
        var t = (int)_f.GetValue(this);
        _w.Write(t);
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
    private void ReadTransform(FieldInfo _f, BinaryReader _r)
    {
        Transform t = (Transform)_f.GetValue(this);
        t.position = new Vector3(_r.ReadSingle(), _r.ReadSingle(), _r.ReadSingle());
        t.rotation = new Quaternion(_r.ReadSingle(), _r.ReadSingle(), _r.ReadSingle(), _r.ReadSingle());
    }
    private void ReadInt(FieldInfo _f, BinaryReader _r)
    {
        _f.SetValue(this, _r.ReadInt32());
    }
}
