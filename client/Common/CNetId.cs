using UnityEngine;
using System;
using System.Runtime.InteropServices;
using UMA;
using UMA.CharacterSystem;
using System.Collections.Generic;

namespace CNet
{
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
					Collider col = GetComponent<Collider>();
					if( col != null ) {
						type = 1;
					} else {
						Debug.Log("Unknown type in CNetId");
						return;
					}
				}
			}
			// - parent		

			// Register with the main controller - this will set the 'id' field
			if( type != 2 ) {
				NetSocket.Instance.RegisterId(this, this.name, type);
			}
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
				Debug.Log("Registering " + this.name + " with type " + type);

				foreach( var child in children ) {
					child.Register();
				}
				registered=true;
			}
		}
	}
}
