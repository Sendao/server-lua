using UnityEngine;
using System;

namespace CNet
{
	public class CNetButton : MonoBehaviour, ICNetReg
	{
		private CNetId cni;

		public void Awake()
		{
			this.cni = GetComponent<CNetId>();
		}

		public void Start()
		{
			cni.RegisterChild(this);
		}

		public void Register()
		{
			NetSocket.Instance.RegisterPacket( CNetFlag.ActivateButton, cni.id, this.ActivateButton, 2 );
		}

		public void Action()
		{// override this.
			return;
		}

		public void ActivateButton( ulong ts, NetStringReader stream )
		{
			Action();
		}

		public void OnMouseDown()
		{
			NetStringBuilder sb = new NetStringBuilder();
			sb.AddUint(cni.id);
			NetSocket.Instance.SendMessage( SCommand.ActivateLua, sb, 0 );

			sb = new NetStringBuilder();
			sb.AddUint(cni.id);
			NetSocket.Instance.SendPacket( CNetFlag.ActivateButton, cni.id, sb, true );

			Action();
		}
	}
}