using UnityEngine;
using System;
using System.Runtime.InteropServices;

public class CNetId : MonoBehaviour
{
	public long id = -1;

	public void Start()
	{
		// Collect any identifying information

		// Register with the main controller
		NetSocket.instance.RegisterId(this, this.name);
	}
}
