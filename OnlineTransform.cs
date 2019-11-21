using UnityEngine;
using System.Collections;

public class OnlineTransform : OnlineBehavior
{

    [Sync]
    public Transform m_transform;
    public OnlineTransform()
    {
    }
    // Use this for initialization
    void Start()
    {
        m_transform = transform;
    }

    // Update is called once per frame
    void Update()
    {

    }
}
