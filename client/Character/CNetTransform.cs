using Opsive.Shared.Events;
using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Networking;
using Opsive.UltimateCharacterController.Utility;
using CNet;
using UnityEngine;

[RequireComponent(typeof(CNetId))]
public class CNetTransform: MonoBehaviour, ICNetUpdate
{
	private float remoteInterpolationMultiplayer = 1.2f;
	private CNetCharacter character;
	private CNetId id;
	private UltimateCharacterLocomotion characterLocomotion;
	private CharacterFootEffects characterFootEffects;

	private Vector3 netPosition;
	private Quaternion netRotation;
	private Vector3 netScale;

	private bool netOnPlatform;
	private uint netPlatformId;
	private Quaternion netPlatformRotationOffset;
	private Quaternion netPlatformPrevRotationOffset;
	private Vector3 netPlatformRelativePosition;
	private Vector3 netPlatformPrevRelativePosition;

	private float distance;
	private float angle;
	private bool initialSync = true;

	private void Awake()
	{
		character = gameObject.GetCachedComponent<CNetCharacter>();
		characterLocomotion = gameObject.GetCachedComponent<UltimateCharacterLocomotion>();
		characterFootEffects = gameObject.GetCachedComponent<CharacterFootEffects>();
		id = gameObject.GetComponent<CNetId>();

		netPosition = transform.position;
		netRotation = transform.rotation;
		netScale = transform.localScale;

		EventHandler.RegisterEvent(gameObject, "OnRespawn", OnRespawn);
		EventHandler.RegisterEvent<bool>(gameObject, "OnCharacterImmediateTransformChange", OnImmediateTransformChange);

		id.RegisterChild( this );
		Debug.Log("CNetTransform woke up on " + id);
		if( id.local ) {
			NetSocket.Instance.RegisterNetObject( this );
		}
	}

	public void Register()
	{
		NetSocket.Instance.RegisterPacket( CNetFlag.Transform, id.id, DoUpdate ); // dynamic packet
	}

	private void Update()
	{
		if (id.local) {
			return;
		}

		var serializationRate = (1f / NetSocket.Instance.updateRate) * remoteInterpolationMultiplayer;
		if (characterLocomotion.Platform != null) {
			if (characterFootEffects != null && (netPlatformPrevRelativePosition - netPlatformRelativePosition).sqrMagnitude > 0.01f) {
				//!characterFootEffects.CanPlaceFootstep = true;
			}
			
			netPlatformPrevRelativePosition = Vector3.MoveTowards(netPlatformPrevRelativePosition, netPlatformRelativePosition, distance * serializationRate);
			netPlatformPrevRotationOffset = Quaternion.RotateTowards(netPlatformPrevRotationOffset, netPlatformRotationOffset, angle * serializationRate);
			characterLocomotion.SetPositionAndRotation(characterLocomotion.Platform.TransformPoint(netPlatformPrevRelativePosition), MathUtility.TransformQuaternion(characterLocomotion.Platform.rotation, netPlatformPrevRotationOffset), false);

		} else {
			if (characterFootEffects != null && (transform.position - netPosition).sqrMagnitude > 0.01f) {
				//!characterFootEffects.CanPlaceFootstep = true;
			}
			Debug.Log("Move distance " + distance*serializationRate + ": " + serializationRate);
			transform.position = Vector3.MoveTowards(transform.position, netPosition, distance * serializationRate);
			transform.rotation = Quaternion.RotateTowards(transform.rotation, netRotation, angle * serializationRate);
		}
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
			var rotation = MathUtility.InverseTransformQuaternion(characterLocomotion.Platform.rotation, transform.rotation);

			if (position != netPosition) {
				dirtyFlag |= (byte)TransformDirtyFlags.Position;
				netPosition = position;
			}

			if (rotation != netRotation) {
				dirtyFlag |= (byte)TransformDirtyFlags.Rotation;
				netRotation = rotation;
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
				sb.AddShortVector3(rotation.eulerAngles);
			}
		} else {
			// Determine the changed objects before sending them.
			if (transform.position != netPosition) {
				dirtyFlag |= (byte)TransformDirtyFlags.Position;
			}
			if (transform.rotation != netRotation) {
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
				sb.AddShortVector3(transform.eulerAngles);
				netRotation = transform.rotation;
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
		Debug.Log($"NetTransform DoUpdate {dirtyFlag}");
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
				if (!initialSync) {
					// Account for the lag.
					var lag = Mathf.Abs((float)(NetSocket.Instance.last_netupdate - ts));
					Debug.Log("Lastupdate: " + NetSocket.Instance.last_netupdate + ", this: " + ts + ", dist: " + lag + " velocity: " + velocity*lag);
					if( lag < 0 ) lag = 0;
					if( lag > 1 ) lag = 1;
					netPosition += velocity * lag;
				}
				initialSync = false;
			}
			if ((dirtyFlag & (byte)TransformDirtyFlags.Rotation) != 0) {
				netRotation = Quaternion.Euler(stream.ReadShortVector3());
			}

			distance = Vector3.Distance(transform.position, netPosition);
			angle = Quaternion.Angle(transform.rotation, netRotation);
		}

		if ((dirtyFlag & (byte)TransformDirtyFlags.Scale) != 0) {
			transform.localScale = stream.ReadShortVector3();
		}
	}

	private void OnRespawn()
	{
		netPosition = transform.position;
		netRotation = transform.rotation;
	}

	private void OnImmediateTransformChange(bool snapAnimator)
	{
		netPosition = transform.position;
		netRotation = transform.rotation;
	}

	private void OnDestroy()
	{
		EventHandler.UnregisterEvent(gameObject, "OnRespawn", OnRespawn);
		EventHandler.UnregisterEvent<bool>(gameObject, "OnCharacterImmediateTransformChange", OnImmediateTransformChange);
	}
}
