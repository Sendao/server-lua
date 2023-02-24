using Opsive.Shared.Events;
using Opsive.UltimateCharacterController.Character;
using UnityEngine;
using CNet;

[RequireComponent(typeof(CNetId))]
class CNetLookSource : MonoBehaviour, ILookSource, ICNetReg, ICNetUpdate
{
    [Tooltip("A multiplier to apply to the networked values for remote players.")]
    [SerializeField] protected float remoteInterpolationMultiplayer = 1.2f;

    private UltimateCharacterLocomotion characterLocomotion;
    private CNetId id;
    private ILookSource lookSource = null;

    public GameObject GameObject { get { return gameObject; } }
    public Transform Transform { get { return transform; } }
    public float LookDirectionDistance { get { return netLookDirectionDistance; } }
    public float Pitch { get { return netPitch; } }

    private float netLookDirectionDistance = 1f;
    private float netTargetLookDirectionDistance = 1f;
    private LagData<float> lagDistance = new LagData<float>(1f, 0.25f, 0.125f, 0.01f);
    
    private float netPitch;
    private float netTargetPitch;
    private LagData<float> lagPitch = new LagData<float>(0f, 0.25f, 0.125f, 0.01f);
    
    private Vector3 netLookPosition;
    private Vector3 netTargetLookPosition;
    private LagData<Vector3> lagLook = new LagData<Vector3>(Vector3.zero, 1, 1, 0.01f);

    private Vector3 netLookDirection;
    private Vector3 netTargetLookDirection;
    private LagData<Vector3> lagDir = new LagData<Vector3>(Vector3.zero, 1, 1, 0.01f);

    private bool initialSync = true;

    private ulong lastUpdate;

    private void Awake()
    {
        characterLocomotion = gameObject.GetComponent<UltimateCharacterLocomotion>();
        id = gameObject.GetComponent<CNetId>();

        lagLook.goal = lagLook.value = netLookPosition = netTargetLookPosition = new Vector3(0, 0, 0);
        lagDir.goal = lagDir.value = netLookDirection = netTargetLookDirection = new Vector3(0, 0, -1);


        EventHandler.RegisterEvent<ILookSource>(gameObject, "OnCharacterAttachLookSource", OnAttachLookSource);
    }

    private void Start()
    {
        if (!id.local) {
            
            EventHandler.UnregisterEvent<ILookSource>(gameObject, "OnCharacterAttachLookSource", OnAttachLookSource);
            EventHandler.ExecuteEvent<ILookSource>(gameObject, "OnCharacterAttachLookSource", this);

            id.RegisterChild(this);

            lagDistance.goal = lagDistance.value = netLookDirectionDistance;
            lagPitch.goal = lagPitch.value = netPitch;
            lagLook.goal = lagLook.value = netLookPosition;
            lagDir.goal = lagDir.value = netLookDirection;

            lagDistance.updt = lagPitch.updt = lagLook.updt = lagDir.updt = 0;
            lagDistance.tick = lagPitch.tick = lagLook.tick = lagDir.tick = 0;

        } else { // monitor local player
            NetSocket.Instance.RegisterNetObject( this );
        }
    }

    public void Register()
    {
        NetSocket.Instance.RegisterPacket( CNetFlag.PlayerLook, id.id, OnUpdate );
    }

    private void OnAttachLookSource(ILookSource source)
    {
        lookSource = source;
    }

    public Vector3 LookPosition(bool characterLookPosition)
    {
        return netLookPosition;
    }

    public Vector3 LookDirection(bool characterLookDirection)
    {
        if (characterLookDirection) {
            return transform.forward;
        }
        return netLookDirection;
    }

    public Vector3 LookDirection(Vector3 lookPosition, bool characterLookDirection, int layerMask, bool includeRecoil, bool includeMovementSpread)
    {
        var collisionLayerEnabled = characterLocomotion.CollisionLayerEnabled;
        characterLocomotion.EnableColliderCollisionLayer(false);

        // Cast a ray from the look source point in the forward direction. The look direction is then the vector from the look position to the hit point.
        RaycastHit hit;
        Vector3 direction;
        if (Physics.Raycast(netLookPosition, characterLookDirection ? transform.forward : netLookDirection, out hit, netLookDirectionDistance, layerMask, QueryTriggerInteraction.Ignore)) {
            direction = (hit.point - lookPosition).normalized;
        } else {
            direction = netLookDirection;
        }

        characterLocomotion.EnableColliderCollisionLayer(collisionLayerEnabled);

        return direction;
    }

    private void Update()
    {
        if ( id.local ) {
            return;
        }
		System.TimeSpan ts = System.DateTime.Now - System.DateTime.UnixEpoch;
        ulong now = (ulong)ts.TotalMilliseconds;
        if( (now - lastUpdate) > 333 ) {
            lastUpdate = now;
        } else if( lagDistance.updt > lastUpdate ) {
            lastUpdate = lagDistance.updt;
        }

        //var serializationRate = (1f / NetSocket.Instance.updateRate);        
        /* This method will not overcorrect, which is ok, but we want to overcorrect most of the time, so it is not ok.
        netLookDirectionDistance = Mathf.MoveTowards(netLookDirectionDistance, netTargetLookDirectionDistance, 
                                                        Mathf.Abs(netTargetLookDirectionDistance - netLookDirectionDistance) * serializationRate);
        netPitch = Mathf.MoveTowards(netPitch, netTargetPitch, Mathf.Abs(netTargetPitch - netPitch) * serializationRate);
        netLookPosition = Vector3.MoveTowards(netLookPosition, netTargetLookPosition, (netTargetLookPosition - netLookPosition).magnitude * serializationRate);
        netLookDirection = Vector3.MoveTowards(netLookDirection, netTargetLookDirection, (netTargetLookDirection - netLookDirection).magnitude * serializationRate);
        */
        Lagger.Update( now, ref lagDistance );
        Lagger.Update( now, ref lagPitch );
        Lagger.Update( now, ref lagLook );
        Lagger.Update( now, ref lagDir );

        if( lagDir.value.magnitude < 0.01f ) {
            Debug.Log("Invalid look direction");
            return;
        }
        netLookDirectionDistance = lagDistance.value;
        netPitch = lagPitch.value;
        netLookPosition = lagLook.value;
        netLookDirection = lagDir.value;
    }

    public void NetUpdate()
    {
        if( id.local && lookSource != null) {
            NetStringBuilder sb = new NetStringBuilder();
            byte dirtyFlag = 0;
            if( Mathf.Abs( netLookDirectionDistance - lookSource.LookDirectionDistance ) > 0.05f ) {
                dirtyFlag |= (byte)LookDirtyFlags.Distance;
                netLookDirectionDistance = lookSource.LookDirectionDistance;
            }
            if( Mathf.Abs( netPitch - lookSource.Pitch ) > 0.05f ) {
                dirtyFlag |= (byte)LookDirtyFlags.Pitch;
                netPitch = lookSource.Pitch;
            }
            var lookPosition = lookSource.LookPosition(true);
            if( (netLookPosition-lookPosition).magnitude > 0.01f ) {
                dirtyFlag |= (byte)LookDirtyFlags.Position;
                netLookPosition = lookPosition;
            }
            var lookDirection = lookSource.LookDirection(false);
            if( (netLookDirection-lookDirection).magnitude > 0.01f ) {
                dirtyFlag |= (byte)LookDirtyFlags.Direction;
                netLookDirection = lookDirection;
            }

            // Send the changes.
            if( dirtyFlag == 0 ) return;
            
            sb.AddByte(dirtyFlag);
            if( (dirtyFlag&(byte)LookDirtyFlags.Distance) != 0 ) {
                sb.AddShortFloat(netLookDirectionDistance);
            }
            if( (dirtyFlag&(byte)LookDirtyFlags.Pitch) != 0 ) {
                sb.AddShortFloat(netPitch);
            }
            if( (dirtyFlag&(byte)LookDirtyFlags.Position) != 0 ) {
                sb.AddShortVector3(netLookPosition);
            }
            if( (dirtyFlag&(byte)LookDirtyFlags.Direction) != 0 ) {
                sb.AddShortVector3(netLookDirection);
            }

            NetSocket.Instance.SendDynPacket( CNetFlag.PlayerLook, id.id, sb );
        }
    }
    public void OnUpdate( ulong ts, NetStringReader stream )
    {
        var dirtyFlag = stream.ReadByte();
        //Debug.Log("CNetLookSource Dirtyflag: " + dirtyFlag + ", size: " + stream.data.Length);

        if( (dirtyFlag&(byte)LookDirtyFlags.Distance) != 0 ) {
            float newDistance = stream.ReadShortFloat();
            //netDistanceVelocity = newDistance - netTargetLookDirectionDistance;
            netTargetLookDirectionDistance = newDistance;
        }
        if( (dirtyFlag&(byte)LookDirtyFlags.Pitch) != 0 ) {
            float newPitch = stream.ReadShortFloat();
            //netPitchVelocity = newPitch - netTargetPitch;
            netTargetPitch = newPitch;
        }
        if( (dirtyFlag&(byte)LookDirtyFlags.Position) != 0 ) {
            Vector3 newPosition = stream.ReadShortVector3();
            //netLookVelocity = newPosition - netTargetLookPosition;
            netTargetLookPosition = newPosition;
        }
        if( (dirtyFlag&(byte)LookDirtyFlags.Direction) != 0 ) {
            Vector3 newDirection = stream.ReadShortVector3().normalized;
            //netDirVelocity = newDirection - netTargetLookDirection;
            netTargetLookDirection = newDirection;
        }
        //Debug.Log("CNetLookSource Dirtyflag: " + dirtyFlag + ", new direction: " + netTargetLookDirection);
        if (initialSync) {
            initialSync = false;
            lagDistance.value = netTargetLookDirectionDistance;
            lagPitch.value = netTargetPitch;
            lagLook.value = netTargetLookPosition;
            lagDir.value = netTargetLookDirection;
        }
        lagDistance.goal = netTargetLookDirectionDistance;
        lagPitch.goal = netTargetPitch;
        lagLook.goal = netTargetLookPosition;
        lagDir.goal = netTargetLookDirection;

        Lagger.Speed( ts, ref lagDistance );
        Lagger.Speed( ts, ref lagPitch );
        Lagger.Speed( ts, ref lagLook );
        Lagger.Speed( ts, ref lagDir );
        lastUpdate = ts;
    }

    private void OnDestroy()
    {
        NetSocket.Instance.UnregisterNetObject( this );
        EventHandler.UnregisterEvent<ILookSource>(gameObject, "OnCharacterAttachLookSource", OnAttachLookSource);
     }
}
