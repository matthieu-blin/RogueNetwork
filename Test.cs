using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Test : OnlineBehavior
{

    public void TestSpawn()
    {
        GameObject obj = OnlineObjectManager.Instance.Instanciate("Cylinder");
        OnlineObjectManager.Instance.Spawn(obj);
    }

    // Start is called before the first frame update
    void Start()
    {
        Init();
    }

    float duration = 5;
    // Update is called once per frame
    void Update()
    {
        duration -= Time.deltaTime;
        if (duration < 0)
        {
            CallRPC();
            CallCMD();
            duration = 5;
        }
    }

    void CallRPC()
    {
        Call("TestRPC");
    }

    void CallCMD()
    {
        Call("TestCMD");
    }

    [CMD]
    void TestCMD()
    {
        Debug.Log("CMD called on object " + m_onlineIdentity.m_uid);
    }
    [RPC]
    void TestRPC()
    {
        Debug.Log("RPC called on object " + m_onlineIdentity.m_uid);
    }
}
