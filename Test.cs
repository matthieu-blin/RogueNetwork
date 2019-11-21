using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Test : MonoBehaviour
{

    public void TestSpawn()
    {
        GameObject obj = OnlineObjectManager.Instance.Instanciate("Cylinder");
        OnlineObjectManager.Instance.Spawn(obj);
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
