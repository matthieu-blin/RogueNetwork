using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OnlineObjectManager : MonoBehaviour
{
    private static OnlineObjectManager instance = null;
    public static OnlineObjectManager Instance
    {
        get
        {
            return instance;
        }
    }
    public void Awake()
    {
        instance = this;
        DontDestroyOnLoad(this);
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void Spawn(GameObject _obj)
    {

    }
    public void ComputeID(GameObject _obj) { }
}
