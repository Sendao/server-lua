using System.Collections.Generic;
using UnityEngine;
using CNet;

public class CNetMecanim : MonoBehaviour, ICNetUpdate
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

    private bool TriggerUsageWarningDone;
    
    private Animator animator;
    private CNetId cni;

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
        this.cni = GetComponent<CNetId>();
    }

    private void Start()
    {
        if( !cni.local ) {
            cni.RegisterChild( this );
        }
    }

    private void Update()
    {
        if (this.animator.applyRootMotion && !this.cni.local && NetSocket.Instance.connected )
            this.animator.applyRootMotion = false;

        if( !NetSocket.Instance.connected ) {
            return;
        }

        if (this.cni.local)
        {
            this.ContinuousUpdate();
            this.CacheDiscreteTriggers();
        }
    }

    public void Register()
    {
		NetSocket.Instance.RegisterPacket( CNetFlag.MecContinuousUpdate, cni.id, DoContinuousUpdate );
		NetSocket.Instance.RegisterPacket( CNetFlag.MecDiscreteUpdate, cni.id, DoDiscreteUpdate );
		NetSocket.Instance.RegisterPacket( CNetFlag.MecSetup, cni.id, DoSetup );
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
            return SynchronizeType.Disabled;
        return this.m_SynchronizeLayers[index].SynchronizeType;
    }

    public SynchronizeType GetParameterSynchronizeType(string name)
    {
        int index = this.m_SynchronizeParameters.FindIndex(item => item.Name == name);

        if (index == -1)
            return SynchronizeType.Disabled;
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

    
    private void ContinuousUpdate()
    {
        NetStringBuilder sb = new NetStringBuilder();

        if (this.animator == null)
        {
            return;
        }

        if (this.m_WasSynchronizeTypeChanged == true) {
            return;
        }

        for (int i = 0; i < this.m_SynchronizeLayers.Count; ++i)
        {
            if (this.m_SynchronizeLayers[i].SynchronizeType == SynchronizeType.Continuous)
            {
                sb.AddFloat(this.animator.GetLayerWeight(this.m_SynchronizeLayers[i].LayerIndex));
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
                        sb.AddBool(this.animator.GetBool(parameter.Name));
                        break;
                    case ParameterType.Float:
                        sb.AddFloat(this.animator.GetFloat(parameter.Name));
                        break;
                    case ParameterType.Int:
                        sb.AddInt(this.animator.GetInteger(parameter.Name));
                        break;
                    case ParameterType.Trigger:
                        sb.AddBool(this.animator.GetBool(parameter.Name));
                        break;
                }
            }
        }
		NetSocket.Instance.SendPacket( CNetFlag.MecContinuousUpdate, cni.id, sb, true );
    }


    private void DoContinuousUpdate(ulong ts, NetStringReader stream)
    {
        for (int i = 0; i < this.m_SynchronizeLayers.Count; ++i)
        {
            if (this.m_SynchronizeLayers[i].SynchronizeType == SynchronizeType.Continuous)
            {
                this.animator.SetLayerWeight(this.m_SynchronizeLayers[i].LayerIndex, stream.ReadFloat());
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
                        this.animator.SetBool(parameter.Name, stream.ReadBool());
                        break;
                    case ParameterType.Float:
                        this.animator.SetFloat(parameter.Name, stream.ReadFloat());
                        break;
                    case ParameterType.Int:
                        this.animator.SetInteger(parameter.Name, stream.ReadInt());
                        break;
                    case ParameterType.Trigger:
                        this.animator.SetBool(parameter.Name, stream.ReadBool());
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
                sb.AddFloat(this.animator.GetLayerWeight(this.m_SynchronizeLayers[i].LayerIndex));
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
                        sb.AddBool(this.animator.GetBool(parameter.Name));
                        break;
                    case ParameterType.Float:
                        sb.AddFloat(this.animator.GetFloat(parameter.Name));
                        break;
                    case ParameterType.Int:
                        sb.AddInt(this.animator.GetInteger(parameter.Name));
                        break;
                    case ParameterType.Trigger:
                        if (!TriggerUsageWarningDone)
                        {
                            TriggerUsageWarningDone = true;
                            Debug.Log("Network: When using triggers, make sure this component is last in the stack.\n" +
                                        "If you still experience issues, implement triggers as a regular RPC \n" +
                                        "or in custom observable component instead",this);
                        
                        }
                        // here we can't rely on the current real state of the trigger, we might have missed its raise
                        sb.AddBool(this.m_raisedDiscreteTriggersCache.Contains(parameter.Name));
                        break;
                }
            }
        }

        // reset the cache, we've synchronized.
        this.m_raisedDiscreteTriggersCache.Clear();
		NetSocket.Instance.SendPacket( CNetFlag.MecDiscreteUpdate, cni.id, sb );
    }

    private void DoDiscreteUpdate(ulong ts, NetStringReader stream)
    {
        for (int i = 0; i < this.m_SynchronizeLayers.Count; ++i)
        {
            if (this.m_SynchronizeLayers[i].SynchronizeType == SynchronizeType.Discrete)
            {
                this.animator.SetLayerWeight(this.m_SynchronizeLayers[i].LayerIndex, stream.ReadFloat());
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
                        this.animator.SetBool(parameter.Name, stream.ReadBool());
                        break;
                    case ParameterType.Float:
                        this.animator.SetFloat(parameter.Name, stream.ReadFloat());
                        break;
                    case ParameterType.Int:
                        this.animator.SetInteger(parameter.Name, stream.ReadInt());
                        break;
                    case ParameterType.Trigger:
                        if ((bool) stream.ReadBool())
                        {
                            this.animator.SetTrigger(parameter.Name);
                        }
                        break;
                }
            }
        }
    }

    public void DoSetup( ulong ts, NetStringReader stream )
    {
        byte[] state = (byte[]) stream.ReadShortBytes();

        for (int i = 0; i < this.m_SynchronizeLayers.Count; ++i)
        {
            this.m_SynchronizeLayers[i].SynchronizeType = (SynchronizeType) state[i];
        }

        for (int i = 0; i < this.m_SynchronizeParameters.Count; ++i)
        {
            this.m_SynchronizeParameters[i].SynchronizeType = (SynchronizeType) state[this.m_SynchronizeLayers.Count + i];
        }
    }

    private void ReadSetup()
    {
        NetStringBuilder sb = new NetStringBuilder();
        byte[] states = new byte[this.m_SynchronizeLayers.Count + this.m_SynchronizeParameters.Count];

        for (int i = 0; i < this.m_SynchronizeLayers.Count; ++i)
        {
            states[i] = (byte) this.m_SynchronizeLayers[i].SynchronizeType;
        }

        for (int i = 0; i < this.m_SynchronizeParameters.Count; ++i)
        {
            states[this.m_SynchronizeLayers.Count + i] = (byte) this.m_SynchronizeParameters[i].SynchronizeType;
        }

        sb.AddShortBytes(states);
        NetSocket.Instance.SendPacket( CNetFlag.MecSetup, cni.id, sb, true );
    }


    public void NetUpdate()
    {
        if (this.animator == null)
        {
            return;
        }

        if (this.m_WasSynchronizeTypeChanged == true)
        {
            this.ReadSetup();

            this.m_WasSynchronizeTypeChanged = false;
        }

        this.DiscreteUpdate();
    }

}
