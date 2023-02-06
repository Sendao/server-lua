using UnityEngine;
using System;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class CNetSync : Attribute
{
    public CNetSync()
    {
		// We are just a flag.
    }
}

// https://docs.unity3d.com/ScriptReference/Rigidbody.html
// https://mirror-networking.gitbook.io/docs/components/network-rigidbody

public class CNetID : MonoBehaviour
{
	public long id = -1;

	public void Start()
	{
		// Collect any identifying information

		// Register with the main controller
		NetSocket::instance.RegisterID(this, this.name);
	}
}

public class CNetRigidBody : MonoBehaviour
{
	public void Update()
	{
		if( NetSocket::instance.authoritative )
		{
			NetStringBuilder sb = new NetStringBuilder();

			SendMessage( 10, )
		}
		else
		{
			// If we are a client, we need to interpolate our position
		}
	}
}

public class CNetPositionController : MonoBehaviour
{

	public void Start()
	{
		// Register with the main controller
		NetSocket::instance.RegisterPosition(this);
	}

	public void Update()
	{
		//! If we are moving, interpolate our position
		//https://answers.unity.com/questions/1450557/best-way-to-communicate-between-scripts.html


	}
}