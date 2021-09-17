using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// This component add online existency to a game object
/// As soon as you set it, your object WON'T BE SPAWNED on client anymore (or won't be active)
/// instead Host will handle the spawn
/// 
/// Each Online Object should have an Unique ID so different client can refer to the 'same' objet with it
/// This unique ID could be set :
///     -automatically if you decide to spawn your object Statically or Dynamically (check OnlineObjectManager)
///     -manually if you decide to give it a determinist one (you must use OnlineObject.ComputeDeterministID()) 
/// </summary>101
public class OnlineIdentity : MonoBehaviour
{

    public ulong m_uid = 0;
    public string m_srcName;
    public uint m_localPlayerAuthority = 0; //host by default
    public enum Type
    { 
        Static, //object in scene, sync between host and clients
        Dynamic, //object dynamically spawned by script using OnlineObject.Instanciate
        Determinist, //object dynamically spawned by script but parallel on each clients using GameObject.Instanciate, you need to give a determinist ids
        HostOnly //object existing only on Host
    };
    public Type m_type = Type.Static;

    // Start is called before the first frame update
    void Awake()
    {
        switch (m_type)
        {
            case Type.HostOnly:
                {
                    break;
                }
            case Type.Dynamic:
                {
                    break;
                }
            case Type.Static:
                {
                    OnlineObjectManager.Instance.RegisterStaticObject(gameObject);
                    break;
                }
            case Type.Determinist:
                {
                    //check if uid is correctly setted and is determinist
                    break;
                }
        }

    }
    void Start()
    {
        switch (m_type)
        {
            case Type.HostOnly:
                {
                    if (!OnlineManager.Instance.IsHost())
                    {
                        Destroy(gameObject);
                        return;
                    }
                    break;
                }
            case Type.Dynamic:
                {
                    if (m_uid == 0)
                    {
                        Destroy(gameObject);
                        return;
                    }
                    break;
                }
            case Type.Static:
                {
                    m_srcName = transform.name;
                    if (OnlineManager.Instance.IsHost())
                    {
                        OnlineObjectManager.Instance.Spawn(gameObject);
                    }
                    else
                    {
                        gameObject.SetActive(false);
                    }
                    break;
                }
            case Type.Determinist:
                {
                    //check if uid is correctly setted and is determinist
                    break;
                }
        }

    }


    public bool HasAuthority()
    {
        switch(m_type)
        {
            case Type.HostOnly: return OnlineManager.Instance.IsHost();
            case Type.Static:
            case Type.Dynamic:
            case Type.Determinist:
                {
                    return m_localPlayerAuthority == OnlinePlayerManager.Instance.m_localPlayerID;
                }
        }
        return false;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnDestroy()
    {
        switch (m_type)
        {
            case Type.HostOnly:
                {
                    break;
                }
            case Type.Dynamic:
                {
                    if (m_uid != 0) 
                    {
                        OnlineObjectManager.Instance.Despawn(gameObject);
                        return;
                    }
                    break;
                }
            case Type.Static:
                {
                    break;
                }
            case Type.Determinist:
                {
                    break;
                }
        }
    }
}
