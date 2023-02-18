using System.Collections.Generic;
using UnityEngine;

/*
public class CNetMecanim : MonoBehaviour, CNetUpdate
{
    public enum ParameterType
    {
        Float = 1,
        Int = 3,
        Bool = 4,
        Trigger = 9,
    }
    public enum SynchronizeType
    {
        Disabled = 0,
        Discrete = 1,
        Continuous = 2,
    }

    public class SynchronizedParameter
    {
        public ParameterType Type;
        public SynchronizeType SynchronizeType;
        public string Name;
    }
    public class SynchronizedLayer
    {
        public SynchronizeType SynchronizeType;
        public int LayerIndex;
    }

    #if PHOTON_DEVELOP
    public PhotonAnimatorView ReceivingSender;
    #endif

    private bool TriggerUsageWarningDone;
    
    private Animator animator;

    private PhotonStreamQueue m_StreamQueue = new PhotonStreamQueue(120);

    [HideInInspector]
    private List<SynchronizedParameter> m_SynchronizeParameters = new List<SynchronizedParameter>();

    [HideInInspector]
    private List<SynchronizedLayer> m_SynchronizeLayers = new List<SynchronizedLayer>();

    private Vector3 m_ReceiverPosition;
    private float m_LastDeserializeTime;
    private bool m_WasSynchronizeTypeChanged = true;

    List<string> m_raisedDiscreteTriggersCache = new List<string>();

    private void Awake()
    {
        this.animator = GetComponent<Animator>();
    }

    private void Update()
    {
        if (this.animator.applyRootMotion && this.photonView.IsMine == false && PhotonNetwork.IsConnected == true)
        {
            this.animator.applyRootMotion = false;
        }

        if( !NetSocket.Instance.connected ) {
            this.m_StreamQueue.Reset();
            return;
        }

        if (this.photonView.IsMine == true)
        {
            this.SerializeDataContinuously();

            this.CacheDiscreteTriggers();
        }
        else
        {
            this.DeserializeDataContinuously();
        }
    }

    public void CacheDiscreteTriggers()
    {
        for (int i = 0; i < this.m_SynchronizeParameters.Count; ++i)
        {
            SynchronizedParameter parameter = this.m_SynchronizeParameters[i];

            if (parameter.SynchronizeType == SynchronizeType.Discrete && parameter.Type == ParameterType.Trigger && this.animator.GetBool(parameter.Name))
            {
                if (parameter.Type == ParameterType.Trigger)
                {
                    this.m_raisedDiscreteTriggersCache.Add(parameter.Name);
                    break;
                }
            }
        }
    }

    public bool DoesLayerSynchronizeTypeExist(int layerIndex)
    {
        return this.m_SynchronizeLayers.FindIndex(item => item.LayerIndex == layerIndex) != -1;
    }
    public bool DoesParameterSynchronizeTypeExist(string name)
    {
        return this.m_SynchronizeParameters.FindIndex(item => item.Name == name) != -1;
    }

    public List<SynchronizedLayer> GetSynchronizedLayers()
    {
        return this.m_SynchronizeLayers;
    }
    public List<SynchronizedParameter> GetSynchronizedParameters()
    {
        return this.m_SynchronizeParameters;
    }

    public SynchronizeType GetLayerSynchronizeType(int layerIndex)
    {
        int index = this.m_SynchronizeLayers.FindIndex(item => item.LayerIndex == layerIndex);

        if (index == -1)
        {
            return SynchronizeType.Disabled;
        }

        return this.m_SynchronizeLayers[index].SynchronizeType;
    }

    public SynchronizeType GetParameterSynchronizeType(string name)
    {
        int index = this.m_SynchronizeParameters.FindIndex(item => item.Name == name);

        if (index == -1)
        {
            return SynchronizeType.Disabled;
        }

        return this.m_SynchronizeParameters[index].SynchronizeType;
    }

    public void SetLayerSynchronized(int layerIndex, SynchronizeType synchronizeType)
    {
        if (Application.isPlaying == true)
        {
            this.m_WasSynchronizeTypeChanged = true;
        }

        int index = this.m_SynchronizeLayers.FindIndex(item => item.LayerIndex == layerIndex);

        if (index == -1)
        {
            this.m_SynchronizeLayers.Add(new SynchronizedLayer {LayerIndex = layerIndex, SynchronizeType = synchronizeType});
        }
        else
        {
            this.m_SynchronizeLayers[index].SynchronizeType = synchronizeType;
        }
    }

    public void SetParameterSynchronized(string name, ParameterType type, SynchronizeType synchronizeType)
    {
        if (Application.isPlaying == true)
        {
            this.m_WasSynchronizeTypeChanged = true;
        }

        int index = this.m_SynchronizeParameters.FindIndex(item => item.Name == name);

        if (index == -1)
        {
            this.m_SynchronizeParameters.Add(new SynchronizedParameter {Name = name, Type = type, SynchronizeType = synchronizeType});
        }
        else
        {
            this.m_SynchronizeParameters[index].SynchronizeType = synchronizeType;
        }
    }

    private void NetUpdate()
    {
        DiscreteUpdate();
        ContinuousUpdate();
    }

    private void ContinuousUpdate()
    {
        NetStringBuilder sb;

        if (this.animator == null)
        {
            return;
        }

        for (int i = 0; i < this.m_SynchronizeLayers.Count; ++i)
        {
            if (this.m_SynchronizeLayers[i].SynchronizeType == SynchronizeType.Continuous)
            {
                this.m_StreamQueue.SendNext(this.animator.GetLayerWeight(this.m_SynchronizeLayers[i].LayerIndex));
            }
        }

        for (int i = 0; i < this.m_SynchronizeParameters.Count; ++i)
        {
            SynchronizedParameter parameter = this.m_SynchronizeParameters[i];

            if (parameter.SynchronizeType == SynchronizeType.Continuous)
            {
                switch (parameter.Type)
                {
                    case ParameterType.Bool:
                        this.m_StreamQueue.SendNext(this.animator.GetBool(parameter.Name));
                        break;
                    case ParameterType.Float:
                        this.m_StreamQueue.SendNext(this.animator.GetFloat(parameter.Name));
                        break;
                    case ParameterType.Int:
                        this.m_StreamQueue.SendNext(this.animator.GetInteger(parameter.Name));
                        break;
                    case ParameterType.Trigger:
                        this.m_StreamQueue.SendNext(this.animator.GetBool(parameter.Name));
                        break;
                }
            }
        }
    }


    private void DoUpdateContinuous(long ts, NetStringReader stream)
    {
        if (this.m_StreamQueue.HasQueuedObjects() == false)
        {
            return;
        }

        for (int i = 0; i < this.m_SynchronizeLayers.Count; ++i)
        {
            if (this.m_SynchronizeLayers[i].SynchronizeType == SynchronizeType.Continuous)
            {
                this.animator.SetLayerWeight(this.m_SynchronizeLayers[i].LayerIndex, (float) this.m_StreamQueue.ReceiveNext());
            }
        }

        for (int i = 0; i < this.m_SynchronizeParameters.Count; ++i)
        {
            SynchronizedParameter parameter = this.m_SynchronizeParameters[i];

            if (parameter.SynchronizeType == SynchronizeType.Continuous)
            {
                switch (parameter.Type)
                {
                    case ParameterType.Bool:
                        this.animator.SetBool(parameter.Name, (bool) this.m_StreamQueue.ReceiveNext());
                        break;
                    case ParameterType.Float:
                        this.animator.SetFloat(parameter.Name, (float) this.m_StreamQueue.ReceiveNext());
                        break;
                    case ParameterType.Int:
                        this.animator.SetInteger(parameter.Name, (int) this.m_StreamQueue.ReceiveNext());
                        break;
                    case ParameterType.Trigger:
                        this.animator.SetBool(parameter.Name, (bool) this.m_StreamQueue.ReceiveNext());
                        break;
                }
            }
        }
    }

    private void DiscreteUpdate()
    {
        NetStringBuilder sb = new NetStringBuilder();

        for (int i = 0; i < this.m_SynchronizeLayers.Count; ++i)
        {
            if (this.m_SynchronizeLayers[i].SynchronizeType == SynchronizeType.Discrete)
            {
                stream.SendNext(this.animator.GetLayerWeight(this.m_SynchronizeLayers[i].LayerIndex));
            }
        }

        for (int i = 0; i < this.m_SynchronizeParameters.Count; ++i)
        {
            
            SynchronizedParameter parameter = this.m_SynchronizeParameters[i];
    
            if (parameter.SynchronizeType == SynchronizeType.Discrete)
            {
                switch (parameter.Type)
                {
                    case ParameterType.Bool:
                        stream.SendNext(this.animator.GetBool(parameter.Name));
                        break;
                    case ParameterType.Float:
                        stream.SendNext(this.animator.GetFloat(parameter.Name));
                        break;
                    case ParameterType.Int:
                        stream.SendNext(this.animator.GetInteger(parameter.Name));
                        break;
                    case ParameterType.Trigger:
                        if (!TriggerUsageWarningDone)
                        {
                            TriggerUsageWarningDone = true;
                            Debug.Log("PhotonAnimatorView: When using triggers, make sure this component is last in the stack.\n" +
                                        "If you still experience issues, implement triggers as a regular RPC \n" +
                                        "or in custom IPunObservable component instead",this);
                        
                        }
                        // here we can't rely on the current real state of the trigger, we might have missed its raise
                        stream.SendNext(this.m_raisedDiscreteTriggersCache.Contains(parameter.Name));
                        break;
                }
            }
        }

        // reset the cache, we've synchronized.
        this.m_raisedDiscreteTriggersCache.Clear();
    }

    private void DoUpdateDiscrete(long ts, NetStringReader stream)
    {
        for (int i = 0; i < this.m_SynchronizeLayers.Count; ++i)
        {
            if (this.m_SynchronizeLayers[i].SynchronizeType == SynchronizeType.Discrete)
            {
                this.animator.SetLayerWeight(this.m_SynchronizeLayers[i].LayerIndex, (float) stream.ReceiveNext());
            }
        }

        for (int i = 0; i < this.m_SynchronizeParameters.Count; ++i)
        {
            SynchronizedParameter parameter = this.m_SynchronizeParameters[i];

            if (parameter.SynchronizeType == SynchronizeType.Discrete)
            {
                switch (parameter.Type)
                {
                    case ParameterType.Bool:
                        if (stream.PeekNext() is bool == false)
                        {
                            return;
                        }
                        this.animator.SetBool(parameter.Name, (bool) stream.ReceiveNext());
                        break;
                    case ParameterType.Float:
                        if (stream.PeekNext() is float == false)
                        {
                            return;
                        }

                        this.animator.SetFloat(parameter.Name, (float) stream.ReceiveNext());
                        break;
                    case ParameterType.Int:
                        if (stream.PeekNext() is int == false)
                        {
                            return;
                        }

                        this.animator.SetInteger(parameter.Name, (int) stream.ReceiveNext());
                        break;
                    case ParameterType.Trigger:
                        if (stream.PeekNext() is bool == false)
                        {
                            return;
                        }

                        if ((bool) stream.ReceiveNext())
                        {
                            this.animator.SetTrigger(parameter.Name);
                        }
                        break;
                }
            }
        }
    }

    private void SerializeSynchronizationTypeState(PhotonStream stream)
    {
        byte[] states = new byte[this.m_SynchronizeLayers.Count + this.m_SynchronizeParameters.Count];

        for (int i = 0; i < this.m_SynchronizeLayers.Count; ++i)
        {
            states[i] = (byte) this.m_SynchronizeLayers[i].SynchronizeType;
        }

        for (int i = 0; i < this.m_SynchronizeParameters.Count; ++i)
        {
            states[this.m_SynchronizeLayers.Count + i] = (byte) this.m_SynchronizeParameters[i].SynchronizeType;
        }

        stream.SendNext(states);
    }

    private void DeserializeSynchronizationTypeState(PhotonStream stream)
    {
        byte[] state = (byte[]) stream.ReceiveNext();

        for (int i = 0; i < this.m_SynchronizeLayers.Count; ++i)
        {
            this.m_SynchronizeLayers[i].SynchronizeType = (SynchronizeType) state[i];
        }

        for (int i = 0; i < this.m_SynchronizeParameters.Count; ++i)
        {
            this.m_SynchronizeParameters[i].SynchronizeType = (SynchronizeType) state[this.m_SynchronizeLayers.Count + i];
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (this.animator == null)
        {
            return;
        }

        if (stream.IsWriting == true)
        {
            if (this.m_WasSynchronizeTypeChanged == true)
            {
                this.m_StreamQueue.Reset();
                this.SerializeSynchronizationTypeState(stream);

                this.m_WasSynchronizeTypeChanged = false;
            }

            this.m_StreamQueue.Serialize(stream);
            this.SerializeDataDiscretly(stream);
        }
        else
        {
            #if PHOTON_DEVELOP
            if( ReceivingSender != null )
            {
                ReceivingSender.OnPhotonSerializeView( stream, info );
            }
            else
            #endif
            {
                if (stream.PeekNext() is byte[])
                {
                    this.DeserializeSynchronizationTypeState(stream);
                }

                this.m_StreamQueue.Deserialize(stream);
                this.DeserializeDataDiscretly(stream);
            }
        }
    }

    #endregion
}
*/
