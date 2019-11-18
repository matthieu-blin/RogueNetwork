using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OnlineIdentity : MonoBehaviour
{

    public ulong m_uid = 0;
    public bool m_localAuthority = false;
    public enum Type
    { 
        Static,
        Dynamic,
        HostOnly
    };
    public Type m_type = Type.Static;

    // Start is called before the first frame update
    void Start()
    {
        switch(m_type)
        {
            case Type.HostOnly:
                {
                    if (!OnlineManager.Instance.IsHost())
                    {
                        Destroy(this);
                        return;
                    }
                    break;
                }
            case Type.Dynamic:
                {
                    if (!OnlineManager.Instance.IsHost())
                    {
                        Destroy(this);
                        return;
                    }
                    else
                    {
                        OnlineObjectManager.Instance.Spawn(gameObject);
                    }
                    break;
                }
            case Type.Static:
                {
                    if (OnlineManager.Instance.IsHost())
                    {
                        OnlineObjectManager.Instance.ComputeID(gameObject);
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
