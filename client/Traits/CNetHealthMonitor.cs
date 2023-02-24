using Opsive.Shared.Game;
using Opsive.Shared.Utility;
using Opsive.UltimateCharacterController.Inventory;
using Opsive.UltimateCharacterController.Items.Actions;
using Opsive.UltimateCharacterController.Networking.Traits;
using Opsive.UltimateCharacterController.Traits;
using Opsive.UltimateCharacterController.Traits.Damage;
using UnityEngine;

namespace CNet
{
    public class CNetHealthMonitor : MonoBehaviour, INetworkHealthMonitor, ICNetReg
    {
        private GameObject m_GameObject;
        private Health m_Health;
        private InventoryBase m_Inventory;
        private CNetId cni;

        /// <summary>
        /// Initializes the default values.
        /// </summary>
        private void Awake()
        {
            m_GameObject = gameObject;
            m_Health = m_GameObject.GetCachedComponent<Health>();
            m_Inventory = m_GameObject.GetCachedComponent<InventoryBase>();
            cni = m_GameObject.GetComponent<CNetId>();
        }

        public void Start()
        {
            if( !cni.local ) {
                cni.RegisterChild( this );
            }
        }

        public void Register()
        {
            if( !cni.local ) {
                NetSocket.Instance.RegisterPacket( CNetFlag.Damage, cni.id, OnDamage, 48 );
                NetSocket.Instance.RegisterPacket( CNetFlag.Death, cni.id, OnDie, 26 );
                NetSocket.Instance.RegisterPacket( CNetFlag.Heal, cni.id, OnHeal, 4 );
            }
        }

        /// <summary>
        /// The object has taken been damaged.
        /// </summary>
        /// <param name="amount">The amount of damage taken.</param>
        /// <param name="position">The position of the damage.</param>
        /// <param name="direction">The direction that the object took damage from.</param>
        /// <param name="forceMagnitude">The magnitude of the force that is applied to the object.</param>
        /// <param name="frames">The number of frames to add the force to.</param>
        /// <param name="radius">The radius of the explosive damage. If 0 then a non-explosive force will be used.</param>
        /// <param name="originator">The originator that did the damage.</param>
        /// <param name="hitCollider">The Collider that was hit.</param>
        public void OnDamage(float amount, Vector3 position, Vector3 direction, float forceMagnitude, int frames, float radius, IDamageOriginator originator, Collider hitCollider)
        {
            uint originatorID = 0;
            uint originatorItemIdentifierID = 0;
            var originatorSlotID = -1;
            var originatorItemActionID = -1;
            if (originator != null) {
                // If the originator is an item then more data needs to be sent.
                if (originator is ItemAction) {
                    var itemAction = originator as ItemAction;
                    originatorItemActionID = itemAction.ID;
                    originatorSlotID = itemAction.Item.SlotID;
                    originatorItemIdentifierID = itemAction.Item.ItemIdentifier.ID;
                }

                if (originator.OriginatingGameObject != null) {
                    var originatorView = originator.OriginatingGameObject.GetComponent<CNetId>();
                    if (originatorView == null) {
                        originatorView = originator.Owner.GetComponent<CNetId>();
                        if (originatorView == null) {
                            Debug.LogError($"Error: The attacker {originator.Owner.name} must have a CNetId component.");
                            return;
                        }
                    }
                    originatorID = originatorView.id;
                }
            }

            // A hit collider is not required. If one exists it must have an ObjectIdentifier or PhotonView attached for identification purposes.
            uint hitColliderID = 0;
            var hitItemSlotID = -1;
            if (hitCollider != null) {
                hitColliderID = NetSocket.Instance.GetIdent(hitCollider.gameObject, out hitItemSlotID);
            }

            NetStringBuilder sb = new NetStringBuilder();

            sb.AddFloat(amount);
            sb.AddVector3(position);
            sb.AddVector3(direction);
            sb.AddFloat(forceMagnitude);
            sb.AddInt(frames);
            sb.AddFloat(radius);
            sb.AddUint(originatorID);
            sb.AddUint(originatorItemIdentifierID);
            sb.AddInt(originatorSlotID);
            sb.AddInt(originatorItemActionID);
            sb.AddUint(hitColliderID);
            sb.AddInt(hitItemSlotID);
            NetSocket.Instance.SendPacket( CNetFlag.Damage, cni.id, sb );
        }

        private void OnDamage( ulong ts, NetStringReader stream )
        {
            float amount = stream.ReadFloat();
            Vector3 position = stream.ReadVector3();
            Vector3 direction = stream.ReadVector3();
            float forceMagnitude = stream.ReadFloat();
            int frames = stream.ReadInt();
            float radius = stream.ReadFloat();
            uint originatorID = stream.ReadUint();
            uint originatorItemIdentifierID = stream.ReadUint();
            int originatorSlotID = stream.ReadInt();
            int originatorItemActionID = stream.ReadInt();
            uint hitColliderID = stream.ReadUint();
            int hitItemSlotID = stream.ReadInt();
            
            IDamageOriginator originator = null;
            if (originatorID != 0) {
                var originatorView = NetSocket.Instance.GetView(originatorID);
                if (originatorView != null) {
                    var otherChar = originatorView.GetComponent<CNetCharacter>();
                    originator = originatorView.GetComponent<IDamageOriginator>();

                    // If the originator is null then it may have come from an item.
                    if (originator == null) {
                        var itemType = otherChar.GetItemID(originatorItemIdentifierID);
                        m_Inventory = originatorView.GetComponent<InventoryBase>();
                        if (itemType != null && m_Inventory != null) {
                            var item = m_Inventory.GetItem(itemType, originatorSlotID);
                            if (item != null) {
                                originator = item.GetItemAction(originatorItemActionID) as IDamageOriginator;
                            }
                        }
                    }
                }
            }

            var hitCollider = NetSocket.Instance.GetIdObj(m_GameObject, hitColliderID, hitItemSlotID);

            var pooledDamageData = GenericObjectPool.Get<DamageData>();
            pooledDamageData.SetDamage(originator, amount, position, direction, forceMagnitude, frames, radius, hitCollider != null ? hitCollider.GetCachedComponent<Collider>() : null);
            m_Health.OnDamage(pooledDamageData);
            GenericObjectPool.Return(pooledDamageData);
        }

        public void Die(Vector3 position, Vector3 force, GameObject attacker)
        {
            uint attackerID = 0;
            if (attacker != null) {
                var attackerCNI = attacker.GetCachedComponent<CNetId>();
                if (attackerCNI == null) {
                    Debug.LogError($"Error: The attacker {attacker.name} must have a CNetId component.");
                    return;
                }
                attackerID = attackerCNI.id;
            }

            NetStringBuilder sb = new NetStringBuilder();
            sb.AddVector3(position);
            sb.AddVector3(force);
            sb.AddUint( attackerID );

            NetSocket.Instance.SendPacket( CNetFlag.Death, cni.id, sb );
        }

        private void OnDie(ulong ts, NetStringReader stream)
        {
            Vector3 position = stream.ReadVector3();
            Vector3 force = stream.ReadVector3();
            uint attackerID = stream.ReadUint();

            GameObject attacker = null;
            if (attackerID != 0) {
                attacker = NetSocket.Instance.GetView(attackerID);
            }
            m_Health.Die(position, force, attacker != null ? attacker : null);
        }

        public void Heal(float amount)
        {
            NetStringBuilder sb = new NetStringBuilder();
            sb.AddFloat(amount);
            NetSocket.Instance.SendPacket( CNetFlag.Heal, cni.id, sb );
        }

        private void OnHeal(ulong ts, NetStringReader stream)
        {
            float amount = stream.ReadFloat();
            m_Health.Heal(amount);
        }
    }
}