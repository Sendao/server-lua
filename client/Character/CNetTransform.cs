using Opsive.Shared.Events;
using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Networking;
using Opsive.UltimateCharacterController.Utility;
using Opsive.UltimateCharacterController.Character.Abilities;
using Opsive.UltimateCharacterController.Game;
using Opsive.Shared.Input.VirtualControls;
using CNet;
using Animancer;
using UnityEngine;

[RequireComponent(typeof(CNetId))]
[AddComponentMenu("Character Transform Network Connection")]
public class CNetTransform: MonoBehaviour, ICNetUpdate, ICNetReg
{
	private CNetCharacter character;
	private CNetId id;
	private UltimateCharacterLocomotion characterLocomotion;
	private CharacterFootEffects characterFootEffects;
    protected HybridAnimancerComponent m_Animancer;

	private Vector3 netPosition;
	private Vector3 netEulers;
	private Vector3 netScale;
	private ulong lastUpdate = 0;

	// setup: startval, maxaccel, maxspeed, mindist
	private static Vector3 east = new Vector3(0, 0, -1);
	private LagData<Vector3> lagPos = new LagData<Vector3>(Vector3.zero, 1.0f, 3.0f, 0.01f, 12f);
	private LagData<Vector3> lagRot = new LagData<Vector3>(east, 1.6f, 3.1f, 0.01f, 6f);
	private LagData<Vector3> lagScale = new LagData<Vector3>(Vector3.one, 0.2f, 0.8f, 0.03f, 1.5f);

	private bool netOnPlatform;
	private uint netPlatformId;
	private Quaternion netPlatformRotationOffset;
	private Quaternion netPlatformPrevRotationOffset;
	private Vector3 netPlatformRelativePosition;
	private Vector3 netPlatformPrevRelativePosition;
	private AnimatorMonitor m_AnimatorMonitor;
	private ILookSource m_LookSource;


	private bool hasData = false;

	private void Awake()
	{
		character = gameObject.GetCachedComponent<CNetCharacter>();
		characterLocomotion = gameObject.GetCachedComponent<UltimateCharacterLocomotion>();
		characterFootEffects = gameObject.GetCachedComponent<CharacterFootEffects>();
		id = gameObject.GetComponent<CNetId>();

		m_AnimatorMonitor = GetComponent<AnimatorMonitor>();
		m_Animancer = GetComponent<HybridAnimancerComponent>();
		m_LookSource = GetComponent<ILookSource>();
		lagPos.goal = lagPos.value = netPosition = transform.position;

		EventHandler.RegisterEvent(gameObject, "OnRespawn", OnRespawn);
		EventHandler.RegisterEvent<bool>(gameObject, "OnCharacterImmediateTransformChange", OnImmediateTransformChange);
	}

	public void Start()
	{
		lagPos.goal = lagPos.value = netPosition = transform.position;
		id.RegisterChild(this);
	}

	public void Delist()
	{
		if( id.local ) {
			NetSocket.Instance.UnregisterNetObject( this );
		} else {
			NetSocket.Instance.UnregisterPacket( CNetFlag.Transform, id.id );
		}
	}
	public void Register()
	{
		if( id.local ) {
			NetSocket.Instance.RegisterNetObject( this );
		} else {
			lagPos.goal = lagPos.value = netPosition = transform.position;
			lagRot.goal = lagRot.value = netEulers = transform.rotation * Vector3.forward;
			lagScale.goal = lagScale.value = netScale = transform.localScale;

			NetSocket.Instance.RegisterPacket( CNetFlag.Transform, id.id, DoUpdate ); // dynamic packet
			NetSocket.Instance.RegisterPacket( CNetFlag.VirtualControl, id.id, OnVirtualControl, 4 );
		}
	}

	protected Vector2 GetInputVector(Vector3 direct)
	{
		var inputVector = Vector2.zero;
		var direction = direct;
		if( direction.x == Mathf.Infinity || direction.x == Mathf.NegativeInfinity ) {
			Debug.Log("Overflow x");
			direction.x = direction.x > 0 ? 1 : -1;
		}
		if( direction.z == Mathf.Infinity || direction.z == Mathf.NegativeInfinity ) {
			Debug.Log("Overflow z");
			direction.z = direction.z > 0 ? 1 : -1;
		}
		inputVector.x = direction.x;
		inputVector.y = direction.z;
		return inputVector;
	}

	public void RegisterControls( VirtualControlsManager mgr )
	{
		if( id.local ) {
			Debug.Log("Shouldn't register controls on local");
			return;
		}
		Debug.Log("Got manager and controls");
	}

	public void SetControl( Vector2 xy )
	{
	}

	public float speed = 0f;

	private void Update()
	{
		if (id.local) {
			return;
		}

		// check for active abilities
		int i;
		for( i=0; i<characterLocomotion.ActiveAbilities.Length; i++ ) {
			var ability = characterLocomotion.ActiveAbilities[i];
			if( ability is MoveTowards ) {
				Debug.Log("No move during movetowards");
				return;
			}
		}

/*		if( characterLocomotion.UsingRootMotionPosition ) {
			Debug.Log("Don't move during FRMP");
			return;
		}*/

		if( !hasData ) {
			KinematicObjectManager.SetCharacterMovementInput(characterLocomotion.KinematicObjectIndex, 0, 0);
			return;
		}

		System.TimeSpan ts = System.DateTime.Now - System.DateTime.UnixEpoch;
		ulong now = (ulong)ts.TotalMilliseconds;

		hasData=false;
		if (characterLocomotion.Platform != null) {
			Lagger.Update( now, ref lagPos );
			Lagger.Update( now, ref lagRot );
			netPlatformRelativePosition = lagPos.value;
			if( (netPlatformPrevRelativePosition - netPlatformRelativePosition).sqrMagnitude > 0.001f ) {
				if( characterFootEffects != null )
					characterFootEffects.CanPlaceFootstep = true;
				hasData=true;
			}
			//SetControl( new Vector2(0,0) );
			Quaternion q = new Quaternion();
			q.SetLookRotation( lagRot.value.normalized );

			Vector3 pos = characterLocomotion.Platform.TransformPoint(lagPos.value);
			Quaternion rot = MathUtility.TransformQuaternion(characterLocomotion.Platform.rotation, q);

			if( pos != transform.position || rot != transform.rotation ) {
				characterLocomotion.SetPositionAndRotation( pos,
						rot, false, false, false );
				//Debug.Log("Set position and rotation to " + lagPos.value + ": " + pos + ", " + q);
			}
		} else {
			float hangTime = ( (now-lastUpdate) / 1000.0f );
			if( hangTime < 0.0001 ) {
				//characterLocomotion.Move( 0, 1, 0 ); // InputVector = GetInputVector(movedir);
				hasData = true;
				return;
			}

			Lagger.Update( now, ref lagPos );
			Lagger.Update( now, ref lagRot );

			Vector3 oldproject = transform.rotation * Vector3.forward;
			Quaternion q = new Quaternion();
			float angles = Vector3.SignedAngle( oldproject, lagRot.value, Vector3.up );
			if( angles != 0 ) {
				q.SetLookRotation( lagRot.value.normalized );
				KinematicObjectManager.SetCharacterDeltaYawRotation(characterLocomotion.KinematicObjectIndex, angles);
				//characterLocomotion.SetRotation(q);
				transform.rotation = q;
				hasData=true;
			} else {
				KinematicObjectManager.SetCharacterDeltaYawRotation(characterLocomotion.KinematicObjectIndex, 0);
				q = transform.rotation;
			}

			if( characterLocomotion.KinematicObjectIndex == -1 ) {
				Debug.LogWarning("KinematicObjectIndex is -1");
			}
			//characterLocomotion.SetPositionAndRotation(lagPos.value, q, false, false, false);

			Vector3 diff = lagPos.goal - transform.position;

			if (diff.sqrMagnitude > 0.001f) {
				if( characterFootEffects != null ) {
					characterFootEffects.CanPlaceFootstep = true;
				}
				Vector3 movedir = MathUtility.InverseTransformDirection(diff, transform.rotation);
				Vector2 inputVector = GetInputVector(movedir.normalized).normalized;

				KinematicObjectManager.SetCharacterMovementInput(characterLocomotion.KinematicObjectIndex, inputVector.x, inputVector.y);
				transform.position = lagPos.value;
				hasData=true;
			} else {
				KinematicObjectManager.SetCharacterMovementInput(characterLocomotion.KinematicObjectIndex, 0, 0); // todo: smooth?
				transform.position = lagPos.goal;
			}
		}
		Lagger.Update( now, ref lagScale );
		transform.localScale = lagScale.value;
		lastUpdate = now;
	}

	public void OnVirtualControl(ulong ts, NetStringReader stream)
	{
		float x = stream.ReadShortFloat(5.0f);
		float y = stream.ReadShortFloat(5.0f);
		// ignore.
			/*
		if( m_x != null )
			m_x.my_value = x;
		if( m_y != null )
			m_y.my_value = y;
			*/
	}

	public void NetUpdate()
	{
		if( !id.local ) {
			Debug.Log("illegal CNetTransform.NetUpdate() on " + id);
			return;
		}

		/*
		float x = Input.GetAxis("Horizontal");
		float y = Input.GetAxis("Vertical");
		NetStringBuilder sb = new NetStringBuilder();
		sb.AddShortFloat(x, 5.0f);
		sb.AddShortFloat(y, 5.0f);
		NetSocket.Instance.SendPacket(CNetFlag.VirtualControl, id.id, sb, true);
		*/
		
		NetStringBuilder sb = new NetStringBuilder();
		byte dirtyFlag = 0;
		if( transform.localScale != netScale ) {
			dirtyFlag |= (byte)TransformDirtyFlags.Scale;
		}

		if( characterLocomotion.Platform != null) {
			var platformIdent = characterLocomotion.Platform.gameObject.GetCachedComponent<CNetId>();
			if (platformIdent == null) {
				Debug.LogError($"Error: The platform {characterLocomotion.Platform} must have a CNetId.");
				return;
			}
			dirtyFlag |= (byte)TransformDirtyFlags.Platform;

			var position = characterLocomotion.Platform.InverseTransformPoint(transform.position);
			//var rotation = MathUtility.InverseTransformQuaternion(characterLocomotion.Platform.rotation, transform.rotation);
			var rotation = MathUtility.InverseTransformQuaternion(characterLocomotion.Platform.rotation, transform.rotation) * Vector3.forward;

			if (position != netPosition) {
				dirtyFlag |= (byte)TransformDirtyFlags.Position;
				netPosition = position;
			}

			if (rotation != netEulers) {
				dirtyFlag |= (byte)TransformDirtyFlags.Rotation;
				netEulers = rotation;
			}

			if( dirtyFlag == (byte)TransformDirtyFlags.Platform ) {
				return;
			}

			sb.AddByte(dirtyFlag);
			sb.AddUint(platformIdent.id);
			if ((dirtyFlag & (byte)TransformDirtyFlags.Position) != 0) {
				sb.AddVector3(position);
			}
			if ((dirtyFlag & (byte)TransformDirtyFlags.Rotation) != 0) {
				sb.AddVector3(rotation);
			}
		} else {
			// Determine the changed objects before sending them.
			if (transform.position != netPosition) {
				dirtyFlag |= (byte)TransformDirtyFlags.Position;
			}
			if (transform.rotation * Vector3.forward != netEulers) {
				dirtyFlag |= (byte)TransformDirtyFlags.Rotation;
			}

			if( dirtyFlag == 0 ) {
				return;
			}

			sb.AddByte(dirtyFlag);
			if ((dirtyFlag & (byte)TransformDirtyFlags.Position) != 0) {
				sb.AddVector3(transform.position);
				sb.AddVector3(transform.position - netPosition);
				netPosition = transform.position;
			}
			if ((dirtyFlag & (byte)TransformDirtyFlags.Rotation) != 0) {
				netEulers = transform.rotation * Vector3.forward;
				sb.AddVector3(netEulers);
			}
		}
		if ((dirtyFlag & (byte)TransformDirtyFlags.Scale) != 0) {
			sb.AddVector3(transform.localScale);
			netScale = transform.localScale;
		}
		NetSocket.Instance.SendDynPacket( CNetFlag.Transform, id.id, sb );
	}

	public void DoUpdate(ulong ts, NetStringReader stream)
	{
		byte dirtyFlag = stream.ReadByte();
		//Debug.Log($"NetTransform DoUpdate {dirtyFlag}");
		if ((dirtyFlag & (byte)TransformDirtyFlags.Platform) != 0) {
			uint platformId = stream.ReadUint();

			// When the character is on a platform the position and rotation is relative to that platform.
			if ((dirtyFlag & (byte)TransformDirtyFlags.Position) != 0) {
				netPlatformRelativePosition = stream.ReadVector3();
			}
			if ((dirtyFlag & (byte)TransformDirtyFlags.Rotation) != 0) {
				netPlatformRotationOffset.SetLookRotation( stream.ReadVector3().normalized );
				//netPlatformRotationOffset = Quaternion.Euler(stream.ReadVector3());
			}
			
			var platform = NetSocket.Instance.GetRigidbody(platformId).transform;
			if( platform == characterLocomotion.Platform ) {
				if( netPlatformId != platformId ) {
					netPlatformId = platformId;
					netOnPlatform = true;
					lagPos.value = lagPos.goal = netPlatformRelativePosition;
					lagRot.value = lagRot.goal = netPlatformRotationOffset * Vector3.forward;
				} else {
					lagPos.goal = netPlatformRelativePosition;
					lagRot.goal = netPlatformRotationOffset * Vector3.forward;
				}
			}


			if (platformId != netPlatformId) {
				Debug.Log("Platform changed: -> " + platformId);

				characterLocomotion.SetPlatform(platform, true);
				platformId = netPlatformId;
				
				//netPlatformRelativePosition = netPlatformPrevRelativePosition = platform.InverseTransformPoint(transform.position);
				//netPlatformRotationOffset = netPlatformPrevRotationOffset = MathUtility.InverseTransformQuaternion(platform.rotation, transform.rotation);
				lagPos.value = lagPos.goal = netPlatformRelativePosition;
				lagRot.value = lagRot.goal = netPlatformRotationOffset * Vector3.forward;
			} else {
				lagPos.goal = netPlatformRelativePosition;
				lagRot.goal = netPlatformRotationOffset * Vector3.forward;
			}

			netPlatformId = platformId;
			netOnPlatform = true;
		} else {
			if ((dirtyFlag & (byte)TransformDirtyFlags.Position) != 0) {
				netPosition = stream.ReadVector3();
				var velocity = stream.ReadVector3();
			}
			if ((dirtyFlag & (byte)TransformDirtyFlags.Rotation) != 0) {
				netEulers = stream.ReadVector3();
			}
			netOnPlatform = ( characterLocomotion.Platform != null );
			if( netOnPlatform ) {
				characterLocomotion.SetPlatform(null, true);
				netPlatformId = 0;
				netOnPlatform = false;
				lagPos.value = netPosition;
				lagRot.value = netEulers;
			} else {
				lagPos.goal = netPosition;
				lagRot.goal = netEulers;
			}
		}

		if ((dirtyFlag & (byte)TransformDirtyFlags.Scale) != 0) {
			netScale = stream.ReadVector3();
		}

		lagScale.goal = netScale;

		Lagger.Speed( ts, ref lagPos );
		Lagger.Speed( ts, ref lagRot );
		Lagger.Speed( ts, ref lagScale );
		lastUpdate = ts;
		hasData = true;
	}

	private void OnRespawn()
	{
		netPosition = transform.position;
		netScale = transform.localScale;
		netEulers = transform.rotation * Vector3.forward;
	}

	private void OnImmediateTransformChange(bool snapAnimator)
	{
		//Debug.Log("ImmediateTransformChange"); -> yes it happens, and it happens a lot.
		netPosition = transform.position;
		netScale = transform.localScale;
		netEulers = transform.rotation * Vector3.forward;
	}

	private void OnDestroy()
	{
		EventHandler.UnregisterEvent(gameObject, "OnRespawn", OnRespawn);
		EventHandler.UnregisterEvent<bool>(gameObject, "OnCharacterImmediateTransformChange", OnImmediateTransformChange);
	}
}
