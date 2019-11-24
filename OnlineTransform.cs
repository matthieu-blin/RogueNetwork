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

    public override void LocalUpdate()
    {
        pos = transform.position;
        rot = transform.rotation;
    }
    public override void RemoteUpdate()
    {
        transform.position = pos;
        transform.rotation = rot;
    }


}
