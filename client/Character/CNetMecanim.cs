using System.Collections.Generic;
using UnityEngine;
using CNet;

public class CNetMecanim : MonoBehaviour, ICNetReg, ICNetUpdate
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

    //These fields are only used in the CustomEditor for this script and would trigger a
    //"this variable is never used" warning, which I am suppressing here
    #pragma warning disable 0414

    [HideInInspector]
    [SerializeField]
    private bool ShowLayerWeightsInspector = true;

    [HideInInspector]
    [SerializeField]
    private bool ShowParameterInspector = true;

    #pragma warning restore 0414


    private bool TriggerUsageWarningDone;
    
    private Animator animator;
    private CNetId cni;
    private RuntimeAnimatorController controller;
    private string current_controller;

    class ControlDetails
    {
        public string name;
        public List<SynchronizedParameter> parameters = new List<SynchronizedParameter>();
        public List<SynchronizedLayer> layers = new List<SynchronizedLayer>();
    };

    [HideInInspector]
    private List<SynchronizedParameter> m_SynchronizeParameters = new List<SynchronizedParameter>();

    [HideInInspector]
    private List<SynchronizedLayer> m_SynchronizeLayers = new List<SynchronizedLayer>();

    [HideInInspector]
    [SerializeField]
    private Dictionary<string, ControlDetails> controllers = new Dictionary<string, ControlDetails>();

    private Vector3 m_ReceiverPosition;
    private float m_LastDeserializeTime;

    List<string> m_raisedDiscreteTriggersCache = new List<string>();

    private void Awake()
    {
        this.animator = GetComponent<Animator>();
        this.cni = GetComponent<CNetId>();
    }

    private void Start()
    {
        if (this.animator != null)
        {
            RuntimeAnimatorController rc = this.GetEffectiveController(this.animator);
            if( rc == null ) {
                controller = null;
                current_controller = "None";
                Debug.Log("No controller");
            } else {
                this.SetController(rc.name);
            }
        } else {
            Debug.LogError("Mecanim requires animator");
        }
        if( !cni.local ) {
            cni.RegisterChild( this );
        } else {
            NetSocket.Instance.RegisterNetObject( this );
        }
    }

    private RuntimeAnimatorController GetEffectiveController(Animator animator)
    {
        RuntimeAnimatorController controller = animator.runtimeAnimatorController;

        AnimatorOverrideController overrideController = controller as AnimatorOverrideController;
        while (overrideController != null)
        {
            controller = overrideController.runtimeAnimatorController;
            overrideController = controller as AnimatorOverrideController;
        }

        return controller;
    }

    public void SetController( string name )
    {
        //this.animator.runtimeAnimatorController = controller;
        //this.controller = this.GetEffectiveController(this.animator) as UnityEditor.Animations.AnimatorController;
        current_controller = name;
        if( !controllers.ContainsKey( current_controller ) ) {
            Debug.Log("New controller " + current_controller);
            ControlDetails ctrl = new ControlDetails();
            ctrl.name = current_controller;
            ctrl.parameters = new List<SynchronizedParameter>();
            ctrl.layers = new List<SynchronizedLayer>();
            controllers[current_controller] = ctrl;
        }
        m_SynchronizeParameters = controllers[current_controller].parameters;
        m_SynchronizeLayers = controllers[current_controller].layers;
        Debug.Log("Set controller to " + name);
    }

    private void Update()
    {
        if (this.animator.applyRootMotion && !this.cni.local && NetSocket.Instance.connected )
            this.animator.applyRootMotion = false;

        if( !NetSocket.Instance.registered ) {
            return;
        }

        if (this.cni.local)
        {
            if( controller != animator.runtimeAnimatorController ) {
                RuntimeAnimatorController rc = this.GetEffectiveController(this.animator) as RuntimeAnimatorController;
                if( controller != rc ) {
                    controller = rc;
                    this.SetController(rc.name);
                    ReadSetup();
                }
            } else if( controller == null ) {
                return;
            }
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

/*
    private void PopulateLayers()
    {
        if (this.animator == null)
        {
            return;
        }

        this.m_SynchronizeParameters.Clear();
        for (int i = 0; i < controller.parameters.Length; ++i) {
            var parameter = controller.parameters[i];
            if (parameter.type == AnimatorControllerParameterType.Bool)
            {
                this.m_SynchronizeParameters.Add(new SynchronizedParameter() { Name = parameter.name, Type = ParameterType.Bool, SynchronizeType = SynchronizeType.Discrete });
            }
            else if (parameter.type == AnimatorControllerParameterType.Float)
            {
                this.m_SynchronizeParameters.Add(new SynchronizedParameter() { Name = parameter.name, Type = ParameterType.Float, SynchronizeType = SynchronizeType.Discrete });
            }
            else if (parameter.type == AnimatorControllerParameterType.Int)
            {
                this.m_SynchronizeParameters.Add(new SynchronizedParameter() { Name = parameter.name, Type = ParameterType.Int, SynchronizeType = SynchronizeType.Discrete });
            }
            else if (parameter.type == AnimatorControllerParameterType.Trigger)
            {
                this.m_SynchronizeParameters.Add(new SynchronizedParameter() { Name = parameter.name, Type = ParameterType.Trigger, SynchronizeType = SynchronizeType.Discrete });
            }
        }
        this.m_SynchronizeLayers.Clear();
        for (int i = 0; i < this.controller.layers.Length; ++i)
        {
            this.m_SynchronizeLayers.Add(new SynchronizedLayer() { LayerIndex = i, SynchronizeType = SynchronizeType.Discrete });
        }

        Debug.Log("Setup Layers for Mecanim system");
    }
*/

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

        if (this.animator == null || this.controller == null)
        {
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
        if( sb.used == 0 ) // don't bother sending empty packets.
            return;
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
        if( sb.used == 0 ) // don't bother sending empty packets.
            return;
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

    private void ReadSetup()
    {
        NetStringBuilder sb = new NetStringBuilder();

        sb.AddInt( this.m_SynchronizeLayers.Count );
        for (int i = 0; i < this.m_SynchronizeLayers.Count; ++i)
        {
            sb.AddByte( (byte) this.m_SynchronizeLayers[i].SynchronizeType );
        }

        sb.AddInt( this.m_SynchronizeParameters.Count );
        for (int i = 0; i < this.m_SynchronizeParameters.Count; ++i)
        {
            sb.AddByte( (byte) this.m_SynchronizeParameters[i].SynchronizeType );
            sb.AddByte( (byte) this.m_SynchronizeParameters[i].Type );
            sb.AddString( this.m_SynchronizeParameters[i].Name );
        }
        Debug.Log("Send Mec Setup");
        NetSocket.Instance.SendPacket( CNetFlag.MecSetup, cni.id, sb, true );
    }

    public void DoSetup( ulong ts, NetStringReader stream )
    {
        int layerCount = stream.ReadInt();

        Debug.Log("Recv Mec Setup");
        for (int i = 0; i < layerCount; ++i)
        {
            if( i >= this.m_SynchronizeLayers.Count ) {
                this.m_SynchronizeLayers.Add(new SynchronizedLayer {LayerIndex = i, SynchronizeType = (SynchronizeType)stream.ReadByte()});
            } else {
                this.m_SynchronizeLayers[i].SynchronizeType = (SynchronizeType) stream.ReadByte();
            }
        }

        int paramCount = stream.ReadInt();
        byte synctype, type;
        string name;
        for (int i = 0; i < paramCount; ++i)
        {
            synctype = stream.ReadByte();
            type = stream.ReadByte();
            name = stream.ReadString();

            if( i >= this.m_SynchronizeParameters.Count ) {
                this.m_SynchronizeParameters.Add(new SynchronizedParameter {Name = name, SynchronizeType = (SynchronizeType)synctype, Type = (ParameterType)type});
            } else {
                this.m_SynchronizeParameters[i].Name = name;
                this.m_SynchronizeParameters[i].SynchronizeType = (SynchronizeType)synctype;
                this.m_SynchronizeParameters[i].Type = (ParameterType)type;
            }
        }
        Debug.Log("MecSetup complete");
    }


    public void NetUpdate()
    {
        if (this.animator == null)
        {
            Debug.Log("No animator on netmecanim");
            return;
        }

        if( controller != animator.runtimeAnimatorController ) {
            RuntimeAnimatorController rc = this.GetEffectiveController(this.animator) as RuntimeAnimatorController;
            controller = rc;
            this.SetController(rc.name);
            ReadSetup();
        }
        this.DiscreteUpdate();
    }

}
