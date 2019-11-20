﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//This component add online existency to a game object
//as soon as you set it, your object won't be spawned on client anymore
//but will be spawned on server then depending on configuration
//will be send to the clients
public class OnlineIdentity : MonoBehaviour
{

    public ulong m_uid = 0;
    public string m_srcName;
    public enum Type
    { 
        Static, //object in scene, sync between host and clients
        Dynamic, //object dynamically spawned using OnlineManager.Spawn
        HostOnly //object existing only on Host
    };
    public Type m_type = Type.Static;

    // Start is called before the first frame update
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
                    OnlineObjectManager.Instance.RegisterStaticObject(gameObject);
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
        }

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
