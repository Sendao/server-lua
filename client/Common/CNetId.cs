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
			Debug.Log("Registering " + this.name + " with type " + type);

			// - parent		

			// Register with the main controller - this will set the 'id' field
			NetSocket.Instance.RegisterId(this, this.name, type);
		}

		private List<ICNetUpdate> children = new List<ICNetUpdate>();

		public void RegisterChild( ICNetUpdate child )
		{
			if( registered && !local ) {
				child.Register();
			}
			lock( _registerlock  ) {
				children.Add( child );
				Debug.Log("RegisterChild " + child.GetType().ToString() + " " + children.Count);
			}
		}

		public void Register()
		{
			Debug.Log("Registering " + children.Count + " children");
			if( local ) {
				Debug.LogError("Registering local object to receive updates makes no sense");
			}
			lock( _registerlock ) {
				foreach( var child in children ) {
					child.Register();
				}
				registered=true;
			}
		}
	}
}
