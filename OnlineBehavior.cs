using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using System.Linq;
using System.IO;


#if UNITY_EDITOR //&& BETA
using UnityEditor;
[CustomEditor(typeof(OnlineBehavior), true)]
public class OnlineBehaviorEditor : Editor
{
    int selectedField = 0;
    bool openreflection = true;
    public override void OnInspectorGUI()
    {
        openreflection = EditorGUILayout.BeginFoldoutHeaderGroup(openreflection, "Sync Reflection");
        
        if (openreflection)
        {
            OnlineBehavior obj = (OnlineBehavior)target;
            var syncedFieldsByScript = target.GetType().GetFields(BindingFlags.NonPublic
                | BindingFlags.Public
                | BindingFlags.FlattenHierarchy
                | BindingFlags.Instance
                | BindingFlags.Static)
                .Where(prop => Attribute.IsDefined(prop, typeof(Sync))).ToArray();

            GUILayout.BeginHorizontal("fieldPopup");
            string[] fieldNames = obj.GetType().GetFields().Select(f => f.Name).ToArray();
            selectedField = EditorGUILayout.Popup("Fields", selectedField, fieldNames);
            if (GUILayout.Button("+", GUILayout.Width(20)))
            {
                obj.m_serializedFields.Add(fieldNames[selectedField]);
                EditorUtility.SetDirty(obj);
            }
            GUILayout.EndHorizontal();
            foreach (var f in syncedFieldsByScript)
            {
                GUILayout.BeginHorizontal("scriptfield");
                EditorGUILayout.LabelField(f.Name);
                EditorGUILayout.LabelField("(script)", GUILayout.Width(40));
                GUILayout.EndHorizontal();
            }
            foreach (var fname in obj.m_serializedFields)
            {
                GUILayout.BeginHorizontal("field");
                EditorGUILayout.LabelField(fname);
                bool b = GUILayout.Button("-", GUILayout.Width(20));
                GUILayout.EndHorizontal();
                if (b)
                {
                    obj.m_serializedFields.Remove(fname);
                    EditorUtility.SetDirty(obj);
                    break;
                }
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        base.OnInspectorGUI();
    }
}
#endif

/// <summary>
/// Synced field will be automatically replicated 
/// ONLY from player who has authority on this object to others.
/// Replication will occurs when Object must be synced (check OnlineBehavior NeedSync)
/// </summary>
[AttributeUsage( AttributeTargets.Field)]
public class Sync : Attribute{}


/// <summary>
/// CMD is a remote function request by any online node to be execute on Host ONLY 
/// This will be executed ONLY on host AND ONLY if requester has authority on object
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class CMD : Attribute { }

/// <summary>
/// RPC is a remote function called by host (and ONLY by host will do nothing on Client)
/// RPC Will be executed On Host every Clients
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class RPC : Attribute { }

/// <summary>
/// Inherit this instead of MonoBehavior to use automatic replication features to your script
/// IMPORTANT, You must call Init() function at the end of you Start function
/// 
/// Field can be automatically replicated using [Sync] attribute in script, Or setting them up in editor inspector
/// Function could be remotely called using [RPC] and [CMD] attribute (check them for more information)
///     For now, you must call your [RPC][CMD]void FunctionName(params); using Call(FunctionName, params)
///     RPC and CMD cannot return something.
/// 
/// WARNING : types of function parameters and fields that could be replicated are limited to specific objet type
///         If you want to add your own, Check Init function to see how to register a writer and reader
///TODO : use static dictionnary for this and static method
///
/// Object are only synced when Needed : By default everyframe, but you can override NeedSync method for your purpose
/// </summary>
[RequireComponent(typeof(OnlineIdentity))]
public abstract class OnlineBehavior : MonoBehaviour
{
    public int m_index = 0;
    public OnlineIdentity m_onlineIdentity;
    public List<string> m_serializedFields;
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
        m_ObjectReaders.Add(typeof(float), ReadSingle);
        m_ObjectWriter.Add(typeof(float), WriteSingle);

    }

    
    private void LateUpdate()
    {
        m_justSynced = false;

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
    //override this for your needs
    public virtual bool NeedSync() { return true; }
    bool m_justSynced = false;
    //return true only one frame after receiving a replication
    public  bool HasSynced() { return m_justSynced; }
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
            else
            {
                OnlineManager.Log("No Writer for this type " + type.Name);
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
                else
                {
                    OnlineManager.Log("No Reader for this type " + type.Name);
                }
            }
            m_justSynced = true;
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
                    OnlineManager.Log("wrong parameters size for " + _fncName);
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
                    OnlineManager.Log("wrong parameters size for " + _fncName);
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
                else
                {
                    OnlineManager.Log("No Writer for this type " + type.Name);
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
                else
                {
                    OnlineManager.Log("No Writer for this type " + type.Name);
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
                    OnlineManager.Log("wrong parameters size for " + name);
                }
                else
                {
                    int paramI = 0;
                    foreach (var param in rpc.GetParameters())
                    {
                        Type type = param.ParameterType;
                        ObjectReader or;
                        if (m_ObjectReaders.TryGetValue(type, out or))
                        {
                            parameters[paramI] = or(r);
                        }
                        else
                        {
                            OnlineManager.Log("No Reader for this type " + type.Name);
                        }
                        paramI++;
                    }
                    rpc.Invoke(this, parameters); ;
                }
            }
            else
            {
                OnlineManager.Log("unknown rpc " + name);
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
                    OnlineManager.Log("wrong parameters size for " + name);
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
                        else
                        {
                            OnlineManager.Log("No Reader for this type " + type.Name);
                        }
                        paramI++;
                    }
                    cmd.Invoke(this, parameters); ;
                }
            }
            else
            {
                OnlineManager.Log("unknown cmd " + name);
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
    private void WriteSingle(object _obj, BinaryWriter _w)
    {
        var t = (float)_obj;
        _w.Write(t);
    }

    private object ReadSingle( BinaryReader _r)
    {
        float i =  _r.ReadSingle();
        return i;
    }

}
