using Opsive.Shared.Events;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Character.Abilities;
using Opsive.UltimateCharacterController.Character.Abilities.AI;
using Opsive.UltimateCharacterController.Objects.CharacterAssist;
using Opsive.UltimateCharacterController.Utility;
using Opsive.Shared.Input.VirtualControls;
using Opsive.Shared.Input;
using UnityEngine;
	
namespace CNet
{
    public class VirtualControlFloat : VirtualControl
    {
		public float my_value=0;

        public override bool GetButton(InputBase.ButtonAction action) {
			return false;
		}

        public override float GetAxis(string buttonName) {
			return my_value;
		}
    }

    public class CNetVirtualControlsManager : VirtualControlsManager, ICNetReg, ICNetUpdate
    {
		private CNetId cni;
		protected float my_x, my_y;

		protected VirtualControlFloat m_x = null, m_y = null;

        protected override void Awake()
        {
			m_Character = gameObject;
			base.Awake();
			if( !gameObject.activeSelf ) {
				Debug.LogWarning("Failed to register virtual controls manager");
				gameObject.SetActive(true);
			}
			//m_GameObject = gameObject;
		}

		public void Start()
		{
			my_x = my_y = 0;
			if( !gameObject.activeSelf ) {
				Debug.LogWarning("Failed to register virtual controls manager");
				gameObject.SetActive(true);
			}
		}

		public void Delist()
		{
			//! todo
		}

		public void Register()
		{
			cni = GetComponent<CNetId>();
			UnityInput ui = GetComponent<UnityInput>();
            if (cni.local) {
				NetSocket.Instance.RegisterNetObject( this );
				ui.ForceInput = (UnityInput.ForceInputType)1; // standalone
            } else {
				m_x = gameObject.AddComponent<VirtualControlFloat>();
				m_y = gameObject.AddComponent<VirtualControlFloat>();
				NetSocket.Instance.RegisterPacket( CNetFlag.VirtualControl, cni.id, OnVirtualControl, 4 );
				this.RegisterVirtualControl("Horizontal", m_x);
				this.RegisterVirtualControl("Vertical", m_y);
				ui.ForceInput = (UnityInput.ForceInputType)2; // virtual
				Character = gameObject;
				if( !gameObject.activeSelf ) {
					Debug.LogError("Failed to register virtual controls manager");
					gameObject.SetActive(true);
				}
			}
			Debug.Log("Virtual controls registered (" + (cni.local?"local":"remote") + ")");
        }

		public void NetUpdate()
		{
            float x = Input.GetAxis("Horizontal");
			float y = Input.GetAxis("Vertical");

			NetStringBuilder sb = new NetStringBuilder();
			sb.AddShortFloat(x, 5.0f);
			sb.AddShortFloat(y, 5.0f);

			NetSocket.Instance.SendPacket(CNetFlag.VirtualControl, cni.id, sb, true);
		}

		public void SetControl( Vector2 xy )
		{
			my_x = xy.x;
			my_y = xy.y;
			if( m_x != null )
				m_x.my_value = xy.x;
			if( m_y != null )
				m_y.my_value = xy.y;
		}

		public void OnVirtualControl(ulong ts, NetStringReader stream)
		{
			float x = stream.ReadShortFloat(5.0f);
			float y = stream.ReadShortFloat(5.0f);

			my_x = x;
			my_y = y;
			if( m_x != null )
				m_x.my_value = x;
			if( m_y != null )
				m_y.my_value = y;
		}
/*
	    public override float GetAxis(string axisName)
		{
			if( cni.local ) {
				return base.GetAxis(axisName);
			}
			if( axisName == "Mouse X" ) {
				return 0;
			} else if( axisName == "Mouse Y" ) {
				return 0;
			} else if( axisName == "Horizontal" ) {
				Debug.Log("Returning my_x: " + my_x);
				return my_x;
			} else if( axisName == "Vertical" ) {
				Debug.Log("Returning my_y: " + my_y);
				return my_y;
			} else {
				Debug.Log("Unknown axis: " + axisName);
				return 0;
			}
		}
*/

    }
}