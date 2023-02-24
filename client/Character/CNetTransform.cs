using Opsive.Shared.Events;
using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Networking;
using Opsive.UltimateCharacterController.Utility;
using CNet;
using UnityEngine;

[RequireComponent(typeof(CNetId))]
[AddComponentMenu("Character Transform Network Connection")]
public class CNetTransform: MonoBehaviour, ICNetUpdate, ICNetReg
{
	private CNetCharacter character;
	private CNetId id;
	private UltimateCharacterLocomotion characterLocomotion;
	private CharacterFootEffects characterFootEffects;

	private Vector3 netPosition;
	private Quaternion netRotation;
	private Vector3 netEulers;
	private Vector3 netScale;
	private ulong lastUpdate = 0;

	// setup: startval, maxaccel, maxspeed, mindist
	private static Vector3 east = new Vector3(0, 0, -1);
	private LagData<Vector3> lagPos = new LagData<Vector3>(Vector3.zero, 14f, 2.6f, 0.02f);
	private LagData<Vector3> lagRot = new LagData<Vector3>(east, 6f, 3f, 0.25f);
	private LagData<Vector3> lagScale = new LagData<Vector3>(Vector3.one, 0.2f, 0.1f, 0.05f);

	private bool netOnPlatform;
	private uint netPlatformId;
	private Quaternion netPlatformRotationOffset;
	private Quaternion netPlatformPrevRotationOffset;
	private Vector3 netPlatformRelativePosition;
	private Vector3 netPlatformPrevRelativePosition;

	private float distance;
	private float angle;
	private bool hasData = false;

	private void Awake()
	{
		character = gameObject.GetCachedComponent<CNetCharacter>();
		characterLocomotion = gameObject.GetCachedComponent<UltimateCharacterLocomotion>();
		characterFootEffects = gameObject.GetCachedComponent<CharacterFootEffects>();
		id = gameObject.GetComponent<CNetId>();

		EventHandler.RegisterEvent(gameObject, "OnRespawn", OnRespawn);
		EventHandler.RegisterEvent<bool>(gameObject, "OnCharacterImmediateTransformChange", OnImmediateTransformChange);
	}

	public void Register()
	{
		NetSocket.Instance.RegisterPacket( CNetFlag.Transform, id.id, DoUpdate ); // dynamic packet
	}

	public void Start()
	{
		if( id.local ) {
			NetSocket.Instance.RegisterNetObject( this );
		} else {
			lagPos.goal = lagPos.value = netPosition = transform.position;
			lagRot.goal = lagRot.value = netEulers = transform.rotation * Vector3.forward;
			lagScale.goal = lagScale.value = netScale = transform.localScale;

			id.RegisterChild( this );
		}
	}

	private void Update()
	{
		if (id.local) {
			return;
		}

		if( !hasData ) return;

		System.TimeSpan ts = System.DateTime.Now - System.DateTime.UnixEpoch;
		ulong now = (ulong)ts.TotalMilliseconds;
		if( (now - lastUpdate) > 150 ) {
			lastUpdate = now;
		} else if( lagPos.updt > lastUpdate ) {
			lastUpdate = lagPos.updt;
		}

		if (characterLocomotion.Platform != null) {
			if (characterFootEffects != null && (netPlatformPrevRelativePosition - netPlatformRelativePosition).sqrMagnitude > 0.01f) {
				characterFootEffects.CanPlaceFootstep = true;
			}
			
			//netPlatformPrevRelativePosition = Vector3.MoveTowards(netPlatformPrevRelativePosition, netPlatformRelativePosition, distance * serializationRate);
			//netPlatformPrevRotationOffset = Quaternion.RotateTowards(netPlatformPrevRotationOffset, netPlatformRotationOffset, angle * serializationRate);
			//characterLocomotion.SetPositionAndRotation(characterLocomotion.Platform.TransformPoint(netPlatformPrevRelativePosition), MathUtility.TransformQuaternion(characterLocomotion.Platform.rotation, netPlatformPrevRotationOffset), false);

		} else {
			if ((transform.position - netPosition).sqrMagnitude > 0.01f) {
				if( characterFootEffects != null ) {
					characterFootEffects.CanPlaceFootstep = true;
				}
			}
				//Debug.Log("Move distance " + distance*serializationRate + ": " + serializationRate);
				/* old method:
				transform.position = Vector3.MoveTowards(transform.position, netPosition, distance * serializationRate);
				transform.rotation = Quaternion.RotateTowards(transform.rotation, netRotation, angle * serializationRate);
				*/
				// new method:
				//Debug.Log("lagPos: " + lagPos.value + " -> " + lagPos.goal);
			Lagger.Update( now, ref lagPos );
			Lagger.Update( now, ref lagRot );
			//Debug.Log("LDecid: " + lagPos.value);
			transform.position = lagPos.value;
			Quaternion q = new Quaternion();
			q.SetLookRotation( lagRot.value.normalized );
			transform.rotation = q;//Quaternion.SetLookRotation( lagRot.value.normalized );
		}
		Lagger.Update( now, ref lagScale );
		transform.localScale = lagScale.value;
	}

	public void NetUpdate()
	{
		if( !id.local ) {
			Debug.Log("illegal CNetTransform.NetUpdate() on " + id);
			return;
		}
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
			var rotation = MathUtility.InverseTransformQuaternion(characterLocomotion.Platform.rotation, transform.rotation).eulerAngles;

			if (position != netPosition) {
				dirtyFlag |= (byte)TransformDirtyFlags.Position;
				netPosition = position;
			}

			/*if (rotation != netRotation) {
				dirtyFlag |= (byte)TransformDirtyFlags.Rotation;
				netRotation = rotation;
			}*/
			if (rotation != netEulers) {
				dirtyFlag |= (byte)TransformDirtyFlags.Rotation;
				netEulers = rotation;
			}

			if( dirtyFlag == 0 ) {
				return;
			}

			sb.AddByte(dirtyFlag);
			sb.AddUint(platformIdent.id);
			if ((dirtyFlag & (byte)TransformDirtyFlags.Position) != 0) {
				sb.AddShortVector3(position);
			}
			if ((dirtyFlag & (byte)TransformDirtyFlags.Rotation) != 0) {
				sb.AddShortVector3(rotation);
			}
		} else {
			// Determine the changed objects before sending them.
			if (transform.position != netPosition) {
				dirtyFlag |= (byte)TransformDirtyFlags.Position;
			}
			if (transform.rotation.eulerAngles != netEulers) {
				dirtyFlag |= (byte)TransformDirtyFlags.Rotation;
			}

			if( dirtyFlag == 0 ) {
				return;
			}

			sb.AddByte(dirtyFlag);
			if ((dirtyFlag & (byte)TransformDirtyFlags.Position) != 0) {
				sb.AddShortVector3(transform.position);
				sb.AddShortVector3(transform.position - netPosition);
				netPosition = transform.position;
			}
			if ((dirtyFlag & (byte)TransformDirtyFlags.Rotation) != 0) {
				netEulers = transform.rotation * Vector3.forward;
				sb.AddShortVector3(netEulers);
			}
		}
		if ((dirtyFlag & (byte)TransformDirtyFlags.Scale) != 0) {
			sb.AddShortVector3(transform.localScale);
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
				netPlatformRelativePosition = stream.ReadShortVector3();
			}
			if ((dirtyFlag & (byte)TransformDirtyFlags.Rotation) != 0) {
				netPlatformRotationOffset = Quaternion.Euler(stream.ReadShortVector3());
			}

			// Do not do any sort of interpolation when the platform has changed.
			if (platformId != netPlatformId) {
				var platform = NetSocket.Instance.GetRigidbody(platformId).transform;
				characterLocomotion.SetPlatform(platform, true);
				netPlatformRelativePosition = netPlatformPrevRelativePosition = platform.InverseTransformPoint(transform.position);
				netPlatformRotationOffset = netPlatformPrevRotationOffset = MathUtility.InverseTransformQuaternion(platform.rotation, transform.rotation);
			}

			distance = Vector3.Distance(netPlatformPrevRelativePosition, netPlatformRelativePosition);
			angle = Quaternion.Angle(netPlatformPrevRotationOffset, netPlatformRotationOffset);
			netPlatformId = platformId;
			netOnPlatform = true;
		} else {
			if (netOnPlatform) {
				characterLocomotion.SetPlatform(null, true);
				netPlatformId = 0;
				netOnPlatform = false;
			}
			if ((dirtyFlag & (byte)TransformDirtyFlags.Position) != 0) {
				netPosition = stream.ReadShortVector3();
				var velocity = stream.ReadShortVector3();
			}
			if ((dirtyFlag & (byte)TransformDirtyFlags.Rotation) != 0) {
				netEulers = stream.ReadShortVector3();
			}
		}

		if ((dirtyFlag & (byte)TransformDirtyFlags.Scale) != 0) {
			netScale = stream.ReadShortVector3();
		}

		lagPos.goal = netPosition;
		lagRot.goal = netEulers;
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
		netRotation = transform.rotation;
		netScale = transform.localScale;
		netEulers = transform.rotation.eulerAngles;
	}

	private void OnImmediateTransformChange(bool snapAnimator)
	{
		netPosition = transform.position;
		netRotation = transform.rotation;
		netScale = transform.localScale;
		netEulers = transform.rotation.eulerAngles;
	}

	private void OnDestroy()
	{
		EventHandler.UnregisterEvent(gameObject, "OnRespawn", OnRespawn);
		EventHandler.UnregisterEvent<bool>(gameObject, "OnCharacterImmediateTransformChange", OnImmediateTransformChange);
	}
}
