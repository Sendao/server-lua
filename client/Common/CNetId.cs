using UnityEngine;
using System;
using System.Runtime.InteropServices;
using UMA;
using UMA.CharacterSystem;
using System.Collections.Generic;
using Opsive.UltimateCharacterController.Objects;

namespace CNet
{
	[AddComponentMenu("CNet Identifier")]
	public class CNetId : MonoBehaviour
	{
		public bool registered = false;
		public uint id;
		public bool local = true;
		public int type;
		private object _registerlock = new object();

        private void Awake()
        {
			registered = false;

			ObjectIdentifier objid = GetComponent<ObjectIdentifier>();
            if( objid != null ) {
				this.id = objid.ID;
                NetSocket.Instance.RegisterObjectIdentifier(objid);
            }
        }

        private void OnDestroy()
        {
            if (registered) {
				ObjectIdentifier objid = GetComponent<ObjectIdentifier>();
				if( objid != null ) {
                	NetSocket.Instance.UnregisterObjectIdentifier(objid);
				}
                registered = false;
            }
        }

		public void Start()
		{
			// Collect any identifying information - type

			DynamicCharacterAvatar c = GetComponent<DynamicCharacterAvatar>();
			if( c != null ) {
				type = 2;
			} else {
				Rigidbody rb = GetComponent<Rigidbody>();
				if( rb != null ) {
					type = 0;
				} else {
					Debug.Log("Unknown type in CNetId");
					return;
				}
			}
			// - parent

			ObjectIdentifier oid = GetComponent<ObjectIdentifier>();
			if( oid != null ) {
				this.id = oid.ID;
				this.type = 1;
			}

			// Register with the main controller - this will set the 'id' field and call 'Register'
			if( type != 2 ) {
				NetSocket.Instance.RegisterId(this, this.name, type);
			} // note player types are handled differently by NewUser()
		}

		private List<ICNetReg> children = new List<ICNetReg>();

		public void RegisterChild( ICNetReg child )
		{
			lock( _registerlock  ) {
				children.Add( child );
				if( registered ) {
					child.Register();
				}
			}
		}

		public void Delist()
		{
			lock( _registerlock ) {
				foreach( ICNetReg child in children ) {
					child.Delist();
				}
				registered=false;
			}
		}

		public void Register()
		{
			lock( _registerlock ) {
				Debug.Log("Registering " + this.name + " with type " + type + ", " + (local?"local":"remote") + " and id " + id + ", " + children.Count + " children");

				foreach( ICNetReg child in children ) {
					child.Register();
				}
				registered=true;
			}
		}
	}
}
