using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class stupidMoveNotSync : MonoBehaviour
{
    Vector3 startPos;
    OnlineIdentity net;

    // Start is called before the first frame update
    void Start()
    {

        startPos = transform.position;
        net = GetComponent<OnlineIdentity>();
    }

    // Update is called once per frame
    void Update()
    {
        if (net != null && !net.HasAuthority())
        {
            return;
        }


        transform.position = new Vector3(transform.position.x + 0.1f, transform.position.y, transform.position.z);
        if (transform.position.x > 50)
            transform.position = startPos;

    }
}
