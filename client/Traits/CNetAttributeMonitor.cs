using Opsive.Shared.Events;
using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Traits;
using UnityEngine;

namespace CNet
{
    [RequireComponent(typeof(AttributeManager))]
    public class CNetAttributeMonitor : MonoBehaviour, ICNetReg
    {
        private AttributeManager m_AttributeManager;
        private CNetId cni;

        private void Awake()
        {
            m_AttributeManager = gameObject.GetCachedComponent<AttributeManager>();
            cni = gameObject.GetCachedComponent<CNetId>();
        }

        private void Start()
        {
			cni.RegisterChild( this );
        }

		public void Register()
		{
			if( cni.local ) {
				NetSocket.Instance.RegisterPacket( CNetFlag.RequestAttributeSet, cni.id, DoRequestAttributeSet, 2 );
			} else {
				NetSocket.Instance.RegisterPacket( CNetFlag.AttributeSet, cni.id, DoAttributeSet ); // dynamic packet

				NetStringBuilder sb = new NetStringBuilder();
				sb.AddUint(cni.id);
				NetSocket.Instance.SendPacketTo( cni.id, CNetFlag.RequestAttributeSet, cni.id, sb );
			}
		}

        private void DoRequestAttributeSet(ulong ts, NetStringReader stream)
        {
			uint id = stream.ReadUint();

            var attributes = m_AttributeManager.Attributes;
            if (attributes == null) {
                return;
            }

			NetStringBuilder sb = new NetStringBuilder();
            for (int i = 0; i < attributes.Length; ++i) {
				sb.AddString(attributes[i].Name);
				sb.AddFloat(attributes[i].Value);
				sb.AddFloat(attributes[i].MinValue);
				sb.AddFloat(attributes[i].MaxValue);
				sb.AddFloat(attributes[i].AutoUpdateAmount);
				sb.AddFloat(attributes[i].AutoUpdateInterval);
				sb.AddFloat(attributes[i].AutoUpdateStartDelay);
				sb.AddInt((int)attributes[i].AutoUpdateValueType);
            }
			NetSocket.Instance.SendDynPacketTo(id, CNetFlag.AttributeSet, cni.id, sb);
        }

		public void DoAttributeSet( ulong ts, NetStringReader stream )
		{
			while( stream.offset < stream.data.Length ) {
				string name = stream.ReadString();
				var attribute = m_AttributeManager.GetAttribute(name);
				if (attribute == null) {
					Debug.Log("DoAttributeSet: Cannot find attribute " + name);
					return;
				}
				attribute.Value = stream.ReadFloat();
				attribute.MinValue = stream.ReadFloat();
				attribute.MaxValue = stream.ReadFloat();
				attribute.AutoUpdateAmount = stream.ReadFloat();
				attribute.AutoUpdateInterval = stream.ReadFloat();
				attribute.AutoUpdateStartDelay = stream.ReadFloat();
				attribute.AutoUpdateValueType = (Attribute.AutoUpdateValue)stream.ReadInt();
			}
		}
    }
}