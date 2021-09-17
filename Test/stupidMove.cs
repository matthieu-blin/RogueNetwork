using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class stupidMove : OnlineBehavior
{
    Vector3 startPos;

    [Sync]
    public float x = 0;

    // Start is called before the first frame update
    void Start()
    {
        startPos = transform.position;
        Init();
    }

    // Update is called once per frame
    void Update()
    {
        if (!HasAuthority())
        {

            transform.position = new Vector3(x, transform.position.y, transform.position.z); ;
            return;
        }


        transform.position = new Vector3(transform.position.x + 0.1f, transform.position.y, transform.position.z);
        if (transform.position.x > 50)
            transform.position = startPos;

        x = transform.position.x;
    }
}
