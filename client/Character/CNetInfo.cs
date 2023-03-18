
using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Networking;
using UnityEngine;
using CNet;

public class CNetInfo : MonoBehaviour, INetworkInfo
{
	private CNetId data;

	private void Awake()
	{
		data = gameObject.GetComponent<CNetId>();
	}

	public bool IsServerAuthoritative()
	{
		return false;
	}

	public bool IsServer()
	{
		return NetSocket.Instance.authoritative;
	}

	public bool IsLocalPlayer()
	{
		return data.local;
	}
}
