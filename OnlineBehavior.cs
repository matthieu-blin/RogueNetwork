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
    public int m_index = 0;
    public OnlineIdentity m_onlineIdentity;
    private FieldInfo[] m_syncedFields;
    public delegate object ObjectReader(BinaryReader _r);
    public delegate void ObjectWriter(object _o, BinaryWriter _r);
    private Dictionary<Type, ObjectReader> m_ObjectReaders = new Dictionary<Type, ObjectReader>();
    private Dictionary<Type, ObjectWriter> m_ObjectWriter = new Dictionary<Type, ObjectWriter>();

    class PendingMethod
    {
        public string name;
        public object[] parameters;
    }
    private MethodInfo[] m_cmds;
    private List<PendingMethod> m_pendingcmds = new List<PendingMethod>();
    private MethodInfo[] m_rpcs;
    private List<PendingMethod> m_pendingrpcs = new List<PendingMethod>();
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

         m_onlineIdentity = GetComponent<OnlineIdentity>();
        OnlineObjectManager.Instance.RegisterOnlineBehavior(this);

        //find OnlineIdentity Component
        m_ObjectReaders.Add(typeof(Vector3), ReadVector3);
        m_ObjectWriter.Add(typeof(Vector3), WriteVector3);
        m_ObjectReaders.Add(typeof(Quaternion), ReadQuaternion);
        m_ObjectWriter.Add(typeof(Quaternion), WriteQuaternion);
        m_ObjectReaders.Add(typeof(int), ReadInt);
        m_ObjectWriter.Add(typeof(int), WriteInt);

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

        if(m_onlineIdentity.HasAuthority())
        {
            if (m_syncedFields.Count() > 0)
            {
                return NeedSync();
            }
        }
        return false;
    }
    public virtual bool NeedSync() { return true; }
    public void Write(BinaryWriter w)
    {
        foreach (var field in m_syncedFields)
        {
            Type type = field.FieldType;
            ObjectWriter ow;
            if (m_ObjectWriter.TryGetValue(type, out ow))
            {
                ow(field.GetValue(this), w);
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
                ObjectReader or;
                if (m_ObjectReaders.TryGetValue(type, out or))
                {
                    field.SetValue(this, or(r));
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
        Call(_fncName, new object[0]);
    }
    public void Call(string _fncName, object[] parameters )
    {
        var rpc = Array.Find(m_rpcs, f => f.Name == _fncName);
        if (rpc != null)
        {
            if(OnlineManager.Instance.IsHost())
            {
                if (rpc.GetParameters().Count() != parameters.Count())
                {
                    Console.Error.WriteLine("wrong parameters size for " + _fncName);
                }
                else
                {
                    m_pendingrpcs.Add(new PendingMethod() { name = _fncName, parameters = parameters });
                    rpc.Invoke(this, parameters);
                }
            }
        }
        var cmd = Array.Find(m_cmds, f => f.Name == _fncName);
        if (cmd != null)
        {
            if (HasAuthority())
            {
                if (cmd.GetParameters().Count() != parameters.Count())
                {
                    Console.Error.WriteLine("wrong parameters size for " + _fncName);
                }
                else
                {
                    if (OnlineManager.Instance.IsHost())
                    {
                        cmd.Invoke(this, parameters);
                    }
                    else
                    {
                        m_pendingcmds.Add(new PendingMethod() { name = _fncName, parameters = parameters });
                    }
                }
            }
        }
    }

    public bool NeedUpdateMethods()
    {

        if (m_onlineIdentity == null)
            return false;
        return m_pendingcmds.Count > 0 || m_pendingrpcs.Count > 0;
    }
    public void WriteRPCs(BinaryWriter w)
    {
        w.Write(m_pendingrpcs.Count);
        foreach(var rpc in m_pendingrpcs)
        {
            w.Write(rpc.name);
            w.Write(rpc.parameters.Count());
            foreach(object obj in rpc.parameters)
            {
                Type type = obj.GetType();
                ObjectWriter ow;
                if (m_ObjectWriter.TryGetValue(type, out ow))
                {
                    ow(obj, w);
                }
            }
        }
        m_pendingrpcs.Clear();
     }
    public void WriteCMDs(BinaryWriter w)
    {
        w.Write(m_pendingcmds.Count);
        foreach (var cmd in m_pendingcmds)
        {
            w.Write(cmd.name);
            w.Write(cmd.parameters.Count());
            foreach (object obj in cmd.parameters)
            {
                Type type = obj.GetType();
                ObjectWriter ow;
                if (m_ObjectWriter.TryGetValue(type, out ow))
                {
                    ow(obj, w);
                }
            }
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
            int paramCount = r.ReadInt32();
            object[] parameters = new object[paramCount];
            if (rpc != null)
            {
               
                    if (rpc.GetParameters().Count() != paramCount)
                    {
                        Console.Error.WriteLine("wrong parameters size for " + name);
                    }
                    else
                    {
                        int paramI = 0;
                        foreach(var param in rpc.GetParameters())
                        {
                            Type type = param.ParameterType;
                            ObjectReader or;
                            if (m_ObjectReaders.TryGetValue(type, out or))
                            {
                               parameters[paramI] =  or(r);
                            }
                            paramI++;
                        }
                        rpc.Invoke(this, parameters); ;
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
            int paramCount = r.ReadInt32();
            object[] parameters = new object[paramCount];
            if (cmd != null)
            {
                
                    if (cmd.GetParameters().Count() != paramCount)
                    {
                        Console.Error.WriteLine("wrong parameters size for " + name);
                    }
                    else
                    {
                        int paramI = 0;
                        foreach (var param in cmd.GetParameters())
                        {
                            Type type = param.ParameterType;
                            ObjectReader or;
                            if (m_ObjectReaders.TryGetValue(type, out or))
                            {
                                parameters[paramI] = or(r);
                            }
                            paramI++;
                    }
                        cmd.Invoke(this, parameters); ;
                    }
                
            }
        }
    }
    private void WriteVector3(object _obj, BinaryWriter _w)
    {
        var v = (Vector3)_obj;
        _w.Write(v.x);
        _w.Write(v.y);
        _w.Write(v.z);
    }
    private object ReadVector3( BinaryReader _r)
    {
        var v = new Vector3();
        v.x = _r.ReadSingle(); 
        v.y = _r.ReadSingle(); 
        v.z = _r.ReadSingle();
        return v;
    }
    private void WriteQuaternion(object _obj, BinaryWriter _w)
    {
        var q = (Quaternion)_obj;
        _w.Write(q.x);
        _w.Write(q.y);
        _w.Write(q.z);
        _w.Write(q.w);
    }
    private object ReadQuaternion( BinaryReader _r)
    {
        var q = new Quaternion();
        q.x = _r.ReadSingle();
        q.y = _r.ReadSingle();
        q.z = _r.ReadSingle();
        q.w = _r.ReadSingle();
        return q;
    }
    private void WriteInt(object _obj, BinaryWriter _w)
    {
        var t = (int)_obj;
        _w.Write(t);
    }

 
    
    private object ReadInt( BinaryReader _r)
    {
        int i =  _r.ReadInt32();
        return i;
    }
}
