using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class OnlineTransform : OnlineBehavior
{

    [Sync]
    Vector3 pos = new Vector3();
    [Sync]
    Quaternion rot = new Quaternion();

    int m_Tick = 0;
    [Sync]
    int m_RemoteTick = 0;

    class Remotetransform
    {
        public int tick;
        public Vector3 pos;
        public Quaternion rot;
    }
    List<Remotetransform> m_buffer = new List<Remotetransform>();
    public int m_tickDelay = 5;

    Remotetransform remoteA = null;
    Remotetransform remoteB = null;


    public OnlineTransform()
    {
    }

    // Use this for initialization
    void Start()
    {
        pos = transform.position;
        rot = transform.rotation;
        remoteA = new Remotetransform() { tick = 0, pos = this.pos, rot = this.rot };
        remoteB = new Remotetransform() { tick = 0, pos = this.pos, rot = this.rot };
        Init();
    }

    private float deltaTimeCumulative = 0;
    void Update()
    {
        float dt = Time.deltaTime;


        //lerp between remoteA and b
        int deltaTick = remoteB.tick - remoteA.tick;
        if(deltaTick == 0)
        {
            if (!UpdateRemotePoints())
                return;
        }
        deltaTick = remoteB.tick - remoteA.tick;
        if (deltaTick > 0)
        {
            deltaTimeCumulative += dt;
            if (deltaTimeCumulative > deltaTick * Time.fixedDeltaTime)
            {
                if(UpdateRemotePoints())
                {
                    deltaTimeCumulative -= deltaTick * Time.fixedDeltaTime; ;
                }
                
            }
        }
        deltaTick = remoteB.tick - remoteA.tick;
        if (deltaTick == 0)
            return;
        float deltaTime = deltaTick * Time.fixedDeltaTime;
        float ratio = deltaTimeCumulative / deltaTime; ;
        transform.position = Vector3.Lerp(remoteA.pos, remoteB.pos, ratio);
        transform.rotation = Quaternion.Slerp(remoteA.rot, remoteB.rot, ratio);

    }

    bool UpdateRemotePoints()
    {
        //check if we have more up to date remote point
        if (m_buffer.Count == 0)
            return false;

        int nextTick = m_Tick - m_tickDelay;
        Remotetransform newRemote = null;
        while (m_buffer.Count > 0 && m_buffer[0].tick <= nextTick)
        {
            newRemote = m_buffer[0];
            m_buffer.RemoveAt(0);
        }
        if (newRemote != null)
        {
            remoteA = remoteB;
            remoteB = newRemote;
            return true;
        }
        return false;
    }
    void FixedUpdate()
    {
        m_Tick++;

        if (HasAuthority())
        {
            m_RemoteTick = m_Tick;
            pos = transform.position;
            rot = transform.rotation;
}
        else
        {
            var remote = m_buffer.Find(r => r.tick == m_RemoteTick);
            if(remote == null)
            {
                m_buffer.Add(new Remotetransform() { tick = m_RemoteTick, pos = this.pos, rot = this.rot });
            }
        }
    }


}
