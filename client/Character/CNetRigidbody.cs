using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CNet;

namespace CNet
{
	public class CNetRigidbody : MonoBehaviour
	{
		public Vector3 lastpos;
		public Quaternion lastrot;
		public Rigidbody rb;

		public void Start()
		{
			rb = this.GetComponent<Rigidbody>();
			lastpos = rb.position;
			lastrot = rb.rotation;
		}
		public void Update()
		{
			if( NetSocket.Instance.authoritative )
			{
				if( rb.isKinematic && Input.GetKeyDown("space") )
					rb.isKinematic = false;

				// We are authoritative, so we need to send our position to the server
				float dist = Vector3.Distance( lastpos, rb.position );
				float angle = Quaternion.Angle( lastrot, rb.rotation );
				if( dist > 0.1f || angle > 1.0f )
				{
					NetSocket.Instance.SendObject( this );
					lastpos = rb.position;
					lastrot = rb.rotation;
				}
			}
		}
	}
}
