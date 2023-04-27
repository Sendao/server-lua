using UnityEngine;
using CNet;
using System;

[RequireComponent(typeof(Rigidbody))]
[AddComponentMenu("Rigidbody Network Connection")]
public class CNetRigidbodyView : MonoBehaviour, ICNetReg, ICNetUpdate
{
	private Rigidbody body;
	private CNetId cni;

	private Vector3 netPosition;
	private Vector3 netEulers;
	private Vector3 netScale;
	private ulong lastUpdate = 0;

	public float maxspeed = 14f;
	public float maxaccel = 2.6f;
	public float mindist = 0.02f;
	public float maxdist = 5f;

	// setup: startval, maxaccel, maxspeed, mindist
	private static Vector3 east = new Vector3(0, 0, -1);
	private LagData<Vector3> lagPos;
	private LagData<Vector3> lagRot;
	private LagData<Vector3> lagScale;

	private bool hasData = false;

	public void Awake()
	{
		this.body = GetComponent<Rigidbody>();
		this.cni = GetComponent<CNetId>();

		lagPos = new LagData<Vector3>(Vector3.zero, maxaccel, maxspeed, mindist, maxdist);
		lagRot = new LagData<Vector3>(east, 3f, 6f, 0.25f, 1.0f);
		lagScale = new LagData<Vector3>(Vector3.one, 0.1f, 0.2f, 0.05f, 0.2f);

		Debug.Log("CNetObjRigidBody woke up on " + cni);
	}

	public void Start()
	{
		cni.RegisterChild(this);
	}
	public void Delist()
	{
		if( !cni.local ) {
			this.body.isKinematic = true;
			NetSocket.Instance.UnregisterPacket( CNetFlag.ObjTransform, cni.id );
		} else {
			this.body.isKinematic = true;
			NetSocket.Instance.UnregisterNetObject( this );
		}
	}
	public void Register()
	{
		if( !cni.local ) {
			this.body.isKinematic = true;
			lagPos.goal = lagPos.value = netPosition = body.position;
			lagRot.goal = lagRot.value = netEulers = body.rotation * Vector3.forward;
			lagScale.goal = lagScale.value = netScale = transform.localScale;
			NetSocket.Instance.RegisterPacket( CNetFlag.ObjTransform, cni.id, this.DoUpdate );
		} else {
			this.body.isKinematic = false;
			netPosition = Vector3.zero;
			netEulers = new Vector3(0,0,1);
			netScale = Vector3.one;
			NetSocket.Instance.RegisterNetObject( this );
		}
	}

	public void MoveTo( Vector3 pos )
	{
		TimeSpan ts = DateTime.Now - DateTime.UnixEpoch;
		ulong now = (ulong)ts.TotalMilliseconds;

		lastUpdate = lagPos.updt = lagPos.tick = now;
		lagPos.value = lagPos.goal = pos;
		body.position = pos;
	}
	public void RotateTo( Vector3 facing )
	{
		TimeSpan ts = DateTime.Now - DateTime.UnixEpoch;
		ulong now = (ulong)ts.TotalMilliseconds;

		lastUpdate = lagPos.updt = lagPos.tick = now;
		
		//Quaternion q = Quaternion.Euler( facing );
		
		Quaternion q = new Quaternion();
		q.SetLookRotation( facing );

		lagRot.value = lagRot.goal = q * Vector3.forward;
		body.rotation = q;
	}

	private void Update()
	{
		if (cni.local || !hasData) {
			return;
		}

		System.TimeSpan ts = System.DateTime.Now - System.DateTime.UnixEpoch;
		ulong now = (ulong)ts.TotalMilliseconds;
		if( (now - lastUpdate) > 150 ) {
			lastUpdate = now;
		} else if( lagPos.updt > lastUpdate ) {
			lastUpdate = lagPos.updt;
		}

		Lagger.Update( now, ref lagPos );
		Lagger.Update( now, ref lagRot );

		hasData = false;
		if( (lagPos.value - body.position).magnitude > 0.01f ) {
			body.position = lagPos.value;
			hasData = true;
		}

		Quaternion q = new Quaternion();
		q.SetLookRotation( lagRot.value.normalized );
		//Quaternion q = Quaternion.Euler( lagRot.value );
		if( q != body.rotation ) {
			body.rotation = q;
			hasData = true;
		}
		
		Lagger.Update( now, ref lagScale );
		if( (transform.localScale - lagScale.value).magnitude > 0.01f ) {
			transform.localScale = lagScale.value;
			hasData = true;
		}
	}


/* lol....
	public void FixedUpdate()
	{
		if (!this.cni.local)
		{
			this.body.position = Vector3.MoveTowards(this.body.position, this.netPosition, this.distance * (1.0f / NetSocket.Instance.updateRate));
			this.body.rotation = Quaternion.RotateTowards(this.body.rotation, this.netRotation, this.angle * (1.0f / NetSocket.Instance.updateRate));
		}
	}
*/

	public void NetUpdate()
	{
		NetStringBuilder sb = new NetStringBuilder();
		byte dirtyFlag = 0;

		if( transform.localScale != netScale ) {
			dirtyFlag |= (byte)TransformDirtyFlags.Scale;
		}
		if (this.body.position != netPosition) {
			dirtyFlag |= (byte)TransformDirtyFlags.Position;
		}
		if (this.body.rotation * Vector3.forward != netEulers) {
			dirtyFlag |= (byte)TransformDirtyFlags.Rotation;
		}

		if( dirtyFlag == 0 ) {
			return;
		}
		sb.AddByte(dirtyFlag);

		if ((dirtyFlag & (byte)TransformDirtyFlags.Position) != 0) {
			netPosition = body.position;
			sb.AddVector3(netPosition);
		}
		if ((dirtyFlag & (byte)TransformDirtyFlags.Rotation) != 0) {
			netEulers = body.rotation * Vector3.forward;
			sb.AddVector3(netEulers);
		}
		if ((dirtyFlag & (byte)TransformDirtyFlags.Scale) != 0) {
			netScale = transform.localScale;
			sb.AddVector3(netScale);
		}
		NetSocket.Instance.SendDynPacket( CNetFlag.ObjTransform, cni.id, sb );
	}
	public void DoUpdate(ulong ts, NetStringReader stream)
	{
		byte dirtyFlag = stream.ReadByte();

		if ((dirtyFlag & (byte)TransformDirtyFlags.Position) != 0) {
			netPosition = stream.ReadVector3();
		}
		if ((dirtyFlag & (byte)TransformDirtyFlags.Rotation) != 0) {
			netEulers = stream.ReadVector3();
		}
		if ((dirtyFlag & (byte)TransformDirtyFlags.Scale) != 0) {
			netScale = stream.ReadVector3();
		}

		lagPos.goal = netPosition;
		lagRot.goal = netEulers;
		lagScale.goal = netScale;

		Lagger.Speed( ts, ref lagPos );
		Lagger.Speed( ts, ref lagRot );
		Lagger.Speed( ts, ref lagScale );
		lastUpdate = ts;
		hasData = true;
	}
}