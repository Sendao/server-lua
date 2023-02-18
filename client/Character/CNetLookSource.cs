using Opsive.Shared.Events;
using Opsive.UltimateCharacterController.Character;
using UnityEngine;
using CNet;

[RequireComponent(typeof(CNetId))]
class CNetLookSource : MonoBehaviour, ILookSource, ICNetUpdate
{
    [Tooltip("A multiplier to apply to the networked values for remote players.")]
    [SerializeField] protected float remoteInterpolationMultiplayer = 1.2f;

    private UltimateCharacterLocomotion characterLocomotion;
    private CNetId id;
    private ILookSource lookSource;

    public GameObject GameObject { get { return gameObject; } }
    public Transform Transform { get { return transform; } }
    public float LookDirectionDistance { get { return netLookDirectionDistance; } }
    public float Pitch { get { return netPitch; } }

    private float netLookDirectionDistance = 1f;
    private float netTargetLookDirectionDistance = 1f;
    private float netPitch;
    private float netTargetPitch;
    private Vector3 netLookPosition;
    private Vector3 netTargetLookPosition;
    private Vector3 netLookDirection;
    private Vector3 netTargetLookDirection;

    private bool initialSync = true;


    private void Awake()
    {
        characterLocomotion = gameObject.GetComponent<UltimateCharacterLocomotion>();
        id = gameObject.GetComponent<CNetId>();

        netLookPosition = netTargetLookPosition = transform.position;
        netLookDirection = netTargetLookDirection = transform.forward;

        EventHandler.RegisterEvent<ILookSource>(gameObject, "OnCharacterAttachLookSource", OnAttachLookSource);
    }

    private void Start()
    {
        if (!id.local) {
            
            EventHandler.UnregisterEvent<ILookSource>(gameObject, "OnCharacterAttachLookSource", OnAttachLookSource);
            EventHandler.ExecuteEvent<ILookSource>(gameObject, "OnCharacterAttachLookSource", this);
            id.RegisterChild(this);

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
        Vector3 direction = -netLookDirection;
        return direction;
    }

    public Vector3 LookDirection(Vector3 lookPosition, bool characterLookDirection, int layerMask, bool includeRecoil, bool includeMovementSpread)
    {
        //Debug.Log("LookDirection(" + lookPosition + ", " + characterLookDirection + ", " + layerMask + ", " + includeRecoil + ", " + includeMovementSpread + ")");
        var collisionLayerEnabled = characterLocomotion.CollisionLayerEnabled;
        characterLocomotion.EnableColliderCollisionLayer(false);

        // Cast a ray from the look source point in the forward direction. The look direction is then the vector from the look position to the hit point.
        RaycastHit hit;
        Vector3 direction;
        if (Physics.Raycast(netLookPosition, characterLookDirection ? transform.forward : netLookDirection, out hit, netLookDirectionDistance, layerMask, QueryTriggerInteraction.Ignore)) {
            direction = (hit.point - lookPosition).normalized;
        } else {
            Vector3 diff = lookPosition - netLookPosition;
            //Debug.Log("LookDiff: " + diff + ", Look: " + netLookDirection);
            direction = -netLookDirection;
            //Debug.Log("Look: " + direction);
        }

        characterLocomotion.EnableColliderCollisionLayer(collisionLayerEnabled);

        return direction;
    }

    private void Update()
    {
        if ( id.local ) {
            return;
        }

        var serializationRate = (1f / NetSocket.Instance.updateRate) * remoteInterpolationMultiplayer;
        
        netLookDirectionDistance = Mathf.MoveTowards(netLookDirectionDistance, netTargetLookDirectionDistance, 
                                                        Mathf.Abs(netTargetLookDirectionDistance - netLookDirectionDistance) * serializationRate);
        netPitch = Mathf.MoveTowards(netPitch, netTargetPitch, Mathf.Abs(netTargetPitch - netPitch) * serializationRate);
        netLookPosition = Vector3.MoveTowards(netLookPosition, netTargetLookPosition, (netTargetLookPosition - netLookPosition).magnitude * serializationRate);
        netLookDirection = Vector3.MoveTowards(netLookDirection, netTargetLookDirection, (netTargetLookDirection - netLookDirection).magnitude * serializationRate);
    }

    public void NetUpdate()
    {
        if( id.local && lookSource != null) {
            NetStringBuilder sb = new NetStringBuilder();
            byte dirtyFlag = 0;
            if( Mathf.Abs( netLookDirectionDistance - lookSource.LookDirectionDistance ) > 0.01f ) {
                dirtyFlag |= (byte)LookDirtyFlags.Distance;
                netLookDirectionDistance = lookSource.LookDirectionDistance;
            }
            if( Mathf.Abs( netPitch - lookSource.Pitch ) > 0.01f ) {
                dirtyFlag |= (byte)LookDirtyFlags.Pitch;
                netPitch = lookSource.Pitch;
            }
            var lookPosition = lookSource.LookPosition(true);
            if( (netLookPosition-lookPosition).magnitude > 0.25f ) {
                dirtyFlag |= (byte)LookDirtyFlags.Position;
                netLookPosition = lookPosition;
            }
            var lookDirection = lookSource.LookDirection(false);
            if( (netLookDirection-lookDirection).magnitude > 0.25f ) {
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
            netTargetLookDirectionDistance = stream.ReadShortFloat();
        }
        if( (dirtyFlag&(byte)LookDirtyFlags.Pitch) != 0 ) {
            netTargetPitch = stream.ReadShortFloat();
        }
        if( (dirtyFlag&(byte)LookDirtyFlags.Position) != 0 ) {
            netTargetLookPosition = stream.ReadShortVector3();
        }
        if( (dirtyFlag&(byte)LookDirtyFlags.Direction) != 0 ) {
            netTargetLookDirection = stream.ReadShortVector3().normalized;
        }
        //Debug.Log("CNetLookSource Dirtyflag: " + dirtyFlag + ", new direction: " + netTargetLookDirection);
        if (initialSync) {
            netLookDirectionDistance = netTargetLookDirectionDistance;
            netPitch = netTargetPitch;
            netLookPosition = netTargetLookPosition;
            netLookDirection = netTargetLookDirection;
            initialSync = false;
        }
    }

    private void OnDestroy()
    {
        NetSocket.Instance.RemoveNetObject( this );
        EventHandler.UnregisterEvent<ILookSource>(gameObject, "OnCharacterAttachLookSource", OnAttachLookSource);
     }
}
