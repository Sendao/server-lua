using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Networking.Traits;
using Opsive.UltimateCharacterController.Traits;
using UnityEngine;

namespace CNet
{
    [RequireComponent(typeof(CNetId))]
    public class CNetInteractableMonitor : MonoBehaviour, INetworkInteractableMonitor
    {
        private GameObject m_GameObject;
        private Interactable m_Interactable;
        private CNetId cni;

        private void Awake()
        {
            m_GameObject = gameObject;
            m_Interactable = m_GameObject.GetCachedComponent<Interactable>();
            cni = m_GameObject.GetCachedComponent<CNetId>();
        }

        public void Register()
        {
            if (!cni.local) {
                NetSocket.Instance.RegisterPacket(CNetFlag.Interact, cni.id, DoInteract, 2);
            }
        }

        public void Interact(GameObject character)
        {
            var characterCNetId = character.GetCachedComponent<CNetId>();
            if (characterCNetId == null) {
                Debug.LogError("Error: The character " + character.name + " must have a CNetId component.");
                return;
            }

            NetStringBuilder sb = new NetStringBuilder();
            sb.AddUint(cni.id);

            NetSocket.Instance.SendPacket(CNetFlag.Interact, cni.id, sb);
        }

        private void DoInteract(ulong ts, NetStringReader stream)
        {
            uint characterViewID = stream.ReadUint();

            var obj = NetSocket.Instance.GetView(characterViewID);
            if (obj == null) {
                return;
            }

            m_Interactable.Interact(obj);
        }
    }
}
