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
