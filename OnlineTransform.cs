using UnityEngine;
using System.Collections;

public class OnlineTransform : OnlineBehavior
{

    [Sync]
    Vector3 pos = new Vector3();
    [Sync]
    Quaternion rot = new Quaternion();
    public OnlineTransform()
    {
    }

        // Use this for initialization
    void Start()
    {
        pos = transform.position;
        rot = transform.rotation;
        Init();
    }

    void Update()
    {
        if (HasAuthority())
        {
            pos = transform.position;
            rot = transform.rotation;
        }
        else
        {
            transform.position = pos;
            transform.rotation = rot;
        }
    }


}
