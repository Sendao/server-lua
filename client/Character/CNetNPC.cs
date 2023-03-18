using Opsive.Shared.Events;
using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Character.Abilities;
using Opsive.UltimateCharacterController.Character.Abilities.AI;
using Opsive.UltimateCharacterController.Objects.CharacterAssist;
using Opsive.UltimateCharacterController.Utility;
using Opsive.Shared.Input.VirtualControls;
using Opsive.UltimateCharacterController.Game;
using UnityEngine;

namespace CNet
{

	public class CNetNPC : MonoBehaviour
	{
		public CNetId cni;

		private UltimateCharacterLocomotion m_CharacterLocomotion;
		private CharacterFootEffects characterFootEffects;
		//private CNetVirtualControlsManager ctlr = null;

		public Vector3 orient;
		public float myspeed;
		protected VirtualControlFloat m_x = null, m_y = null;
		private VirtualControlsManager mymgr;

		public void Awake()
		{
			cni = GetComponent<CNetId>();
			orient = new Vector3(0, 0, -1);
			myspeed = 0.0f;
			Debug.Log("NPC wakeup");
		}

		public void Start()
		{
			m_CharacterLocomotion = GetComponent<UltimateCharacterLocomotion>();
			characterFootEffects = gameObject.GetCachedComponent<CharacterFootEffects>();
			if( m_CharacterLocomotion == null ) {
				Debug.LogError("No CharacterLocomotion found on " + gameObject.name);
			}
				//Rigidbody rb = GetComponent<Rigidbody>();
				//rb.isKinematic = true;
			if( cni.local ) {
				if( m_x == null && mymgr != null ) {
					Debug.Log("Adding virtual controls early");
					m_x = gameObject.AddComponent<VirtualControlFloat>();
					m_y = gameObject.AddComponent<VirtualControlFloat>();
					mymgr.RegisterVirtualControl("Horizontal", m_x);
					mymgr.RegisterVirtualControl("Vertical", m_y);
				}
				//m_CharacterLocomotion.SetPositionAndRotation(transform.position, transform.rotation, false, false, false);
			}

	        EventHandler.RegisterEvent<ILookSource>(gameObject, "OnCharacterAttachLookSource", OnAttachLookSource);
		}

		public void OnAttachLookSource(ILookSource lookSource)
		{
			Debug.Log("NPC OnAttachLookSource");
		}

		protected Vector2 GetInputVector(Vector3 direct)
		{
			var inputVector = Vector2.zero;
			var direction = direct;
			if( direction.x == Mathf.Infinity || direction.x == Mathf.NegativeInfinity )
				direction.x = direction.x > 0 ? 1 : -1;
			if( direction.z == Mathf.Infinity || direction.z == Mathf.NegativeInfinity )
				direction.z = direction.z > 0 ? 1 : -1;
			inputVector.x = direction.x;
			inputVector.y = direction.z;
			return inputVector;
		}

		public void RegisterControls( VirtualControlsManager mgr )
		{
			if( !cni.local ) {
				Debug.LogWarning("Registered remote npc controls on cnpc");
				return;
			}
			Debug.Log("Registered local npc controls on cnpc");
			mymgr = mgr;
			if( m_x == null ) {
				m_x = gameObject.AddComponent<VirtualControlFloat>();
				m_y = gameObject.AddComponent<VirtualControlFloat>();
			}
			mgr.RegisterVirtualControl("Horizontal", m_x);
			mgr.RegisterVirtualControl("Vertical", m_y);
		}

		public void SetControl( Vector2 xy )
		{
			if( m_x != null )
				m_x.my_value = xy.x;
			if( m_y != null )
				m_y.my_value = xy.y;
			//m_CharacterLocomotion.InputVector = Inputs;
		}


		public void Update()
		{
			if( !cni.local ) return;

			float decider = Random.Range(0.0f, 1.0f);

			/*if( ctlr == null ) {
				ctlr = GetComponent<CNetVirtualControlsManager>();
				if( ctlr == null ) {
					Debug.Log("No ctlr");
					return;
				}
				Debug.Log("Found controller");
				myspeed = Random.Range(0.0f, 2.6f);
			}*/

			if( decider > 0.99f ) {
				// change direction
				orient = new Vector3(Random.Range(-1.0f, 1.0f), 0, Random.Range(-1.0f, 1.0f)).normalized;
				myspeed = Random.Range(0.5f, 2.8f);
				Debug.Log("New direction");
			}

			decider = Random.Range(0.0f, 1.0f);
			if( decider > 0.99f ) {
				// change speed
				myspeed = Random.Range(0.5f, 2.8f);
				//Debug.Log("New Speed");
			} else if( decider > 0.98f ) {
				myspeed = 0;
			}

			if( myspeed == 0 ) {
				//m_CharacterLocomotion.InputVector = Vector3.zero;
				//m_CharacterLocomotion.Moving = false;
				//m_CharacterLocomotion.AbilityMotor = Vector3.zero;
				//m_CharacterLocomotion.InputVector = m_CharacterLocomotion.RawInputVector;
				return;
			}

			Vector3 from = transform.rotation * Vector3.forward;
			Quaternion q = transform.rotation;
			float angles2 = Vector3.SignedAngle(from, orient, Vector3.up);
			if( angles2 != 0 ) {
				float speed = 180/1;
				float fract = speed / angles2;

				Vector3 to = Vector3.Slerp(from, orient, Time.deltaTime * fract);
				q.SetLookRotation(to, Vector3.up);
				KinematicObjectManager.SetCharacterDeltaYawRotation(m_CharacterLocomotion.KinematicObjectIndex, angles2);
				transform.rotation = q;
			} else {
				KinematicObjectManager.SetCharacterDeltaYawRotation(m_CharacterLocomotion.KinematicObjectIndex, 0);
			}
			
			//m_CharacterLocomotion.MoveTowardsAbility.MoveTowardsLocation(transform.position + transform.rotation * Vector3.forward * myspeed);
			//Vector3 move = transform.rotation * Vector3.forward;
			//move = move.normalized;
			//m_CharacterLocomotion.Move( move.x, move.y, move.z );

			//SetControl( GetInputVector(orient) );
			//Debug.Log("Inputs: " + Inputs);
			//m_CharacterLocomotion.Moving = true;
			Vector3 move = q * Vector3.forward * myspeed * Time.deltaTime;
			Vector3 newPos = transform.position + move;

			//m_CharacterLocomotion.ManualMove = true;				
			//ls.SetRotation(q);
			//m_CharacterLocomotion.SetRotation(q, false);
			
			//var deltaPosition = newPos - transform.position;
            //var movement = deltaPosition.normalized * myspeed;
            //m_CharacterLocomotion.AbilityMotor = movement / (m_CharacterLocomotion.TimeScaleSquared * Time.timeScale * m_CharacterLocomotion.FramerateDeltaTime);
			//m_CharacterLocomotion.AbilityMotor = q * Vector3.forward * myspeed * Time.deltaTime;

			//m_CharacterLocomotion.MoveTowardsAbility.MoveTowardsLocation(newPos);
			//m_CharacterLocomotion.SetPositionAndRotation(newPos, q, false, false, false);

			if (move.sqrMagnitude/Time.deltaTime > 0.01f) {
				if( characterFootEffects != null ) {
					characterFootEffects.CanPlaceFootstep = true;
				}
				Vector3 movedir = MathUtility.InverseTransformDirection(move/Time.deltaTime, transform.rotation);
				Vector2 inputVector = GetInputVector(movedir.normalized).normalized;

				//m_CharacterLocomotion.MoveDirection = move;
				//m_CharacterLocomotion.InputVector = m_CharacterLocomotion.RawInputVector = inputVector;			
            	KinematicObjectManager.SetCharacterMovementInput(m_CharacterLocomotion.KinematicObjectIndex, inputVector.x, inputVector.y);
				//m_CharacterLocomotion.Move( inputVector.x, inputVector.y, 0 ); // InputVector = GetInputVector(movedir);
				transform.position = newPos;
			}

//m_CharacterLocomotion.Move( inputVector.x, inputVector.y, 0 ); // InputVector = GetInputVector(movedir);

			//m_CharacterLocomotion.AbilityMotor = move;

			//m_CharacterLocomotion.SetPosition(newPos, false);
			//transform.position = newPos;
			
			/*
            for (int i = 0; i < m_CharacterLocomotion.ActiveAbilityCount; ++i) {
                m_CharacterLocomotion.ActiveAbilities[i].UpdateAnimator();
            }
            for (int i = 0; i < m_CharacterLocomotion.ActiveItemAbilityCount; ++i) {
                m_CharacterLocomotion.ActiveItemAbilities[i].UpdateAnimator();
            }
			var m_AnimatorMonitor = GetComponent<AnimatorMonitor>();
			var m_LookSource = GetComponent<ILookSource>();

            m_AnimatorMonitor.SetHorizontalMovementParameter(inputVector.x, 1.0f);
            m_AnimatorMonitor.SetForwardMovementParameter(inputVector.y, 1.0f);
            if (m_LookSource != null) {
                m_AnimatorMonitor.SetPitchParameter(m_LookSource.Pitch, 1.0f, 0);
            }
			*/
            //m_AnimatorMonitor.SetYawParameter(m_YawAngle * m_YawMultiplier, 1.0);
            /*if (!m_SpeedParameterOverride) {
                m_AnimatorMonitor.SetSpeedParameter(SpeedParameterValue, 1.0);
            }*/
			//m_AnimatorMonitor.SetSpeedParameter(move.magnitude/Time.deltaTime, 1.0f);
            //m_AnimatorMonitor.SetMovingParameter(true);

            // The ability parameters should only be updated once each move call.
            //m_CharacterLocomotion.UpdateDirtyAbilityAnimatorParameters();
            //m_AnimatorMonitor.UpdateItemIDParameters();
		}

	}
}
