using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OnlineTest : OnlineBehavior
{
    public GameObject prefab;
    public void TestSpawn()
    {
        GameObject obj = OnlineObjectManager.Instance.Instanciate(prefab, new Vector3(4,0,175), Quaternion.identity);
        OnlineObjectManager.Instance.Spawn(obj);
    }

    // Start is called before the first frame update
    void Start()
    {
        Init();
    }


    public void CallRPC()
    {
        Call("TestRPC");
    }

    public void CallCMD()
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
