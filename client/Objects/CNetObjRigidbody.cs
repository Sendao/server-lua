using UnityEngine;
using CNet;

[RequireComponent(typeof(Rigidbody))]
[AddComponentMenu("Rigidbody Network Connection")]
public class CNetRigidbodyView : MonoBehaviour, ICNetUpdate
{
	[Tooltip("synch angular velocity?")]
	public bool syncAngularVelocity = false;

	private float distance;
	private float angle;

	private Rigidbody body;

	private Vector3 netPosition;

	private Quaternion netRotation;
	private CNetId cni;
	
	[HideInInspector]
	public bool teleportEnabled = false;
	[HideInInspector]
	public float teleportIfDistanceGreaterThan = 3.0f;

	public void Awake()
	{
		this.body = GetComponent<Rigidbody>();
		this.cni = GetComponent<CNetId>();

		this.netPosition = new Vector3();
		this.netRotation = new Quaternion();
	}

	public void Start()
	{
		cni = GetComponent<CNetId>();
	}

	public void Register()
	{
		NetSocket.Instance.RegisterPacket( CNetFlag.ObjTransformUpdate, cni.id, this.DoUpdate );
	}

	public void FixedUpdate()
	{
		if (!this.cni.local)
		{
			this.body.position = Vector3.MoveTowards(this.body.position, this.netPosition, this.distance * (1.0f / NetSocket.Instance.updateRate));
			this.body.rotation = Quaternion.RotateTowards(this.body.rotation, this.netRotation, this.angle * (1.0f / NetSocket.Instance.updateRate));
		}
	}

	public void NetUpdate()
	{
		NetStringBuilder sb = new NetStringBuilder();

		sb.AddVector3(this.body.position);
		sb.AddVector3(this.body.rotation.eulerAngles);
		sb.AddVector3(this.body.velocity);

		if (this.syncAngularVelocity) {
			sb.AddVector3(this.body.angularVelocity);
		}

		NetSocket.Instance.SendPacket( CNetFlag.ObjTransformUpdate, cni.id, sb );
	}
	public void DoUpdate(ulong ts, NetStringReader stream)
	{
		this.netPosition = (Vector3)stream.ReadVector3();
		this.netRotation = Quaternion.Euler(stream.ReadVector3());

		if (this.teleportEnabled) {
			if (Vector3.Distance(this.body.position, this.netPosition) > this.teleportIfDistanceGreaterThan) {
				this.body.position = this.netPosition;
			}
		}
		
		float lag = Mathf.Abs((float)(NetSocket.Instance.last_netupdate - ts) / 1000.0f);

		this.body.velocity = stream.ReadVector3();
		this.netPosition += this.body.velocity * lag;
		this.distance = Vector3.Distance(this.body.position, this.netPosition);

		if( this.syncAngularVelocity ) {
			this.body.angularVelocity = stream.ReadVector3();
			this.netRotation = Quaternion.Euler(this.body.angularVelocity * lag) * this.netRotation;
			this.angle = Quaternion.Angle(this.body.rotation, this.netRotation);
		}
	}
}