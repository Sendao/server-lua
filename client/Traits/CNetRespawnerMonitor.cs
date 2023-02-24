using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Networking.Traits;
using Opsive.UltimateCharacterController.Traits;
using UnityEngine;

namespace CNet
{
    [RequireComponent(typeof(CNetId))]
    public class CNetRespawnerMonitor : MonoBehaviour, INetworkRespawnerMonitor, ICNetReg
    {
        private Respawner m_Respawner;
        private CNetId cni;

        private void Awake()
        {
            m_Respawner = null;
            cni = gameObject.GetCachedComponent<CNetId>();
        }

        public void Start()
        {
            if( !cni.local ) {
                cni.RegisterChild(this);
            }
        }

        public void Register()
        {
            NetSocket.Instance.RegisterPacket(CNetFlag.Respawn, cni.id, OnRespawn, 25);
        }
        public void Respawn(Vector3 position, Quaternion rotation, bool transformChange)
        {
            NetStringBuilder sb = new NetStringBuilder();
            sb.AddVector3(position);
            sb.AddVector3(rotation.eulerAngles);
            sb.AddBool(transformChange);

            NetSocket.Instance.SendPacket(CNetFlag.Respawn, cni.id, sb);
        }

        private void OnRespawn(ulong ts, NetStringReader stream)
        {
            if( m_Respawner == null ) {
                m_Respawner = gameObject.GetCachedComponent<Respawner>();
                if( m_Respawner == null ) {
                    Debug.LogError("Error: CNetRespawnerMonitor.OnRespawn - no Respawner component found on " + gameObject.name);
                    return;
                }
            }
            Vector3 position = stream.ReadVector3();
            Quaternion rotation = Quaternion.Euler( stream.ReadVector3() );
            bool transformChange = stream.ReadBool();
            m_Respawner.Respawn(position, rotation, transformChange);
        }
    }
}