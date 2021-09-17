using UnityEngine;
using System.Collections;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
//[CustomEditor(typeof(OnlineTransform))]
public class OnlineTransformEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
    }
}
#endif

public class OnlineTransform : OnlineBehavior
{
    [SerializeField]  int m_SyncFrequencyInMs = 100;


    [Sync]
    Vector3 m_position = new Vector3();
    [Sync]
    Quaternion m_rotation = new Quaternion();
    [Sync]
    float m_deltaTimeBetweenSync = 0;

    enum Smoothing { NoInterpolation,  Lerp, };
    [SerializeField] Smoothing m_smoothing = Smoothing.Lerp;


   
     public OnlineTransform()
    {
    }

    // Use this for initialization
    void Start()
    {
        m_position = transform.position;
        m_rotation = transform.rotation;
        Init();
    }

    private float m_deltaTimeSinceLastSync = 0;
    void Update()
    {
        m_deltaTimeSinceLastSync += Time.deltaTime;
        if(!HasAuthority())
        {
            switch (m_smoothing)
            {
                case Smoothing.NoInterpolation:
                    {
                        transform.position = m_position;
                        transform.rotation = m_rotation;
                        break;
                    }
                case Smoothing.Lerp:
                    {
                        if (m_SyncFrequencyInMs > m_LerpDelayInMs)
                        {
                            m_LerpDelayInMs = m_SyncFrequencyInMs;
                            OnlineManager.Log("Warning : Lerp delay < sync frequency, will result in glitches");
                         }
                        LinearInterpolation();
                        break;
                    }
            }
        }
    }

    public override bool NeedSync()
    {
        if (m_deltaTimeSinceLastSync  > m_SyncFrequencyInMs / 1000f)
        {
            m_position = transform.position;
            m_rotation = transform.rotation;
            m_deltaTimeBetweenSync = m_deltaTimeSinceLastSync;
            m_deltaTimeSinceLastSync = 0;
            return true;
        }
        return false;
    }

    ///  Linear interpolatoin

    [SerializeField]  int m_LerpDelayInMs = 200;
    float m_currentLerpDeltaTime = 0;
    struct LerpTransform
    {
        public Vector3 position;
        public Quaternion rotation;
        public float deltaTimeSinceLastTransform;
    } 
    LerpTransform  m_currentLerpSrc = new LerpTransform();
    private List<LerpTransform> m_transforms = new List<LerpTransform>();

    private void LinearInterpolation()
    {
        if(HasSynced())
        {
            LerpTransform recvTransform = new LerpTransform();
            recvTransform.position = m_position;
            recvTransform.rotation = m_rotation;
            recvTransform.deltaTimeSinceLastTransform = m_deltaTimeBetweenSync;
            m_transforms.Add(recvTransform);
        }
        if (m_deltaTimeSinceLastSync < m_LerpDelayInMs / 1000f)
        {
            m_currentLerpSrc.position = transform.position;
            m_currentLerpSrc.rotation = transform.rotation;
            return;
        }
        while (m_transforms.Count > 0 && m_transforms[0].deltaTimeSinceLastTransform <= m_currentLerpDeltaTime)
        {
            m_currentLerpDeltaTime -= m_transforms[0].deltaTimeSinceLastTransform;
            m_transforms.RemoveAt(0);
            m_currentLerpSrc.position = transform.position;
            m_currentLerpSrc.rotation = transform.rotation;
        }
        if (m_transforms.Count > 0)
        {
            m_currentLerpDeltaTime += Time.deltaTime;
            transform.position = Vector3.Lerp(m_currentLerpSrc.position, m_transforms[0].position, m_currentLerpDeltaTime / m_transforms[0].deltaTimeSinceLastTransform);
            transform.rotation = Quaternion.Lerp(m_currentLerpSrc.rotation, m_transforms[0].rotation, m_currentLerpDeltaTime / m_transforms[0].deltaTimeSinceLastTransform);
        }
    }
}
