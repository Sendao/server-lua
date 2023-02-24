using UnityEngine;
using System;
using System.Runtime.InteropServices;
using UMA;
using UMA.CharacterSystem;
using System.Collections.Generic;

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

			// Register with the main controller - this will set the 'id' field and call 'Register'
			if( type != 2 ) {
				NetSocket.Instance.RegisterId(this, this.name, type);
			} // note player types are handled differently by NewUser()
		}

		private List<ICNetReg> children = new List<ICNetReg>();

		public void RegisterChild( ICNetReg child )
		{
			if( registered ) {
				child.Register();
			} else {
				lock( _registerlock  ) {
					children.Add( child );
				}
			}
		}

		public void Register()
		{
			lock( _registerlock ) {
				if( type != 2 ) {
					if( NetSocket.Instance.authoritative ) {
						this.local = true;
					} else {
						this.local = false;
					}
				}
				Debug.Log("Registering " + this.name + " with type " + type);

				foreach( var child in children ) {
					child.Register();
				}
				registered=true;
			}
		}
	}
}
