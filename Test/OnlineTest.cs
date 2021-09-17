using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class OnlineTest : OnlineBehavior
{
    public GameObject prefab;
    public GameObject prefab2;
    public Text text;
    public void TestSpawn()
    {
        GameObject obj = OnlineObject.Instanciate(prefab, new Vector3(0,0,175), Quaternion.identity);
        OnlineObject.Spawn(obj);
    }
   public void TestSpawn2()
    {
        GameObject obj = OnlineObject.Instanciate(prefab2, new Vector3(0,-1,175), Quaternion.identity);
        OnlineObject.Spawn(obj);
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
        text.text = ("CMD called on object " + m_onlineIdentity.m_uid);
    }
    [RPC]
    void TestRPC()
    {

        text.text = ("RPC called on object " + m_onlineIdentity.m_uid);
    }
}
