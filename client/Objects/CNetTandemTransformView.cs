using UnityEngine;
using CNet;
using System;
using System.Collections.Generic;

[AddComponentMenu("Transform Tandem Network Connection")]
public class CNetTandemTransformView : MonoBehaviour, ICNetReg, ICNetUpdate
{
	private Rigidbody rb;
	private CNetId cni;

	private Vector3 netPosition;
	private float netVelocity=0f;
	private Vector3 netEulers;
	private float netAngularVelocity=0f;
	private Vector3 netScale;

	private RCC_CarControllerV3 carController;
	private float totalDist = 0f;

	// setup: startval, maxaccel, maxspeed, mindist
	private LagData<Vector3> lagScale;

	public class Waypoint
	{
		public Vector3 pos;
		public float speed;
		public Vector3 rot;
		public float angvel;
		public float nextdist;
		public float initdist;
		public Waypoint(Vector3 p, float s, Vector3 r, float a, float n, float i)
		{
			pos = p;
			speed = s;
			rot = r;
			angvel = a;
			nextdist = n;
			initdist = i;
		}
	};

	private List<Waypoint> waypoints = new List<Waypoint>();

	private CNetVehicle veh;

	public void Awake()
	{
		this.carController = GetComponent<RCC_CarControllerV3>();
		this.rb = GetComponent<Rigidbody>();
		this.cni = GetComponent<CNetId>();
		this.veh = GetComponent<CNetVehicle>();

		lagScale = new LagData<Vector3>(Vector3.one, 0.1f, 0.2f, 0.05f, 0.2f);

		netPosition = Vector3.zero;
		netEulers = new Vector3(0,0,1);
		netScale = Vector3.one;

		totalDist = 0f;
	}

	public void Start()
	{
		cni.RegisterChild(this);
	}
	public void Delist()
	{
		if( !cni.local ) {
			NetSocket.Instance.UnregisterPacket( CNetFlag.ObjTransform, cni.id );
		} else {
			NetSocket.Instance.UnregisterNetObject( this );
		}
	}
	public void Register()
	{
		lagScale.goal = lagScale.value = netScale = transform.localScale;
		if( !cni.local ) {
			NetSocket.Instance.RegisterPacket( CNetFlag.ObjTransform, cni.id, this.DoUpdate );
		} else {
			NetSocket.Instance.RegisterNetObject( this );
		}
	}

	private void FixedUpdate()
	{
		if (cni.local || waypoints.Count <= 0 ) {
			return;
		}
		
		UpdateWaypoints();
		Waypoint wp = waypoints[0];

		if( veh != null ) {
			float distance = (wp.pos - transform.position).magnitude;
			if( waypoints.Count > 1 ) {
				veh.FeedRCCParams( wp.pos, wp.rot, wp.pos, wp.rot, wp.speed, wp.angvel, totalDist - distance, true, waypoints[1].pos );
			} else {
				veh.FeedRCCParams( wp.pos, wp.rot, wp.pos, wp.rot, wp.speed, wp.angvel, totalDist - distance, false, Vector3.zero );
			}
		}
		
		System.TimeSpan ts = System.DateTime.Now - System.DateTime.UnixEpoch;
		ulong now = (ulong)ts.TotalMilliseconds;
		lagScale.value = transform.localScale;
		Lagger.Update( now, ref lagScale );
		if( (transform.localScale - lagScale.value).magnitude > 0.01f ) {
			transform.localScale = lagScale.value;
		}
	}

	public void NetUpdate()
	{
		NetStringBuilder sb = new NetStringBuilder();
		byte dirtyFlag = 0;

		if( transform.localScale != netScale ) {
			dirtyFlag |= (byte)TransformDirtyFlags.Scale;
		}
		if (this.transform.position != netPosition) {
			dirtyFlag |= (byte)TransformDirtyFlags.Position;
		}
		if (this.transform.rotation * Vector3.forward != netEulers) {
			dirtyFlag |= (byte)TransformDirtyFlags.Rotation;
		}

		if( dirtyFlag == 0 ) {
			return;
		}
		sb.AddByte(dirtyFlag);

		if ((dirtyFlag & (byte)TransformDirtyFlags.Position) != 0) {
			netPosition = transform.position;
			sb.AddVector3(netPosition);
			sb.AddFloat(rb.velocity.magnitude);
		}
		if ((dirtyFlag & (byte)TransformDirtyFlags.Rotation) != 0) {
			netEulers = (transform.rotation * Vector3.forward).normalized;
			sb.AddVector3(netEulers);

			sb.AddFloat(Mathf.Abs(rb.angularVelocity.x) + Mathf.Abs(rb.angularVelocity.y) + Mathf.Abs(rb.angularVelocity.z));
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
			netVelocity = stream.ReadFloat();
		}
		if ((dirtyFlag & (byte)TransformDirtyFlags.Rotation) != 0) {
			netEulers = stream.ReadVector3();
			netAngularVelocity = stream.ReadFloat() * 180.0f / Mathf.PI;
		}
		if ((dirtyFlag & (byte)TransformDirtyFlags.Scale) != 0) {
			netScale = stream.ReadVector3();
		}

		lagScale.goal = netScale;

		AddWaypoint(netPosition, netVelocity, netEulers, netAngularVelocity);
	}

	public float Vector2Dist( Vector3 a, Vector3 b )
	{
		return Mathf.Sqrt( (a.x-b.x)*(a.x-b.x) + (a.z-b.z)*(a.z-b.z) );
	}

	public void AddWaypoint( Vector3 target, float speed, Vector3 rot, float angvel )
	{
		float dist = Vector2Dist(transform.position, target);
		if( dist < 0.05f ) return;

		if( target == Vector3.zero ) {
			Debug.LogError("AddWaypoint: target is zero");
			return;
		}

		if( waypoints.Count == 0 ) {
			waypoints.Add( new Waypoint( target, speed, rot, angvel, 0.1f, dist ) );
			totalDist += dist;
		} else {
			while( waypoints.Count > 12 ) {
				totalDist -= waypoints[0].initdist;
				waypoints.RemoveAt(0);
			}
			float distance = Vector2Dist(waypoints[waypoints.Count-1].pos, target);
			float angle = Vector3.Angle( waypoints[waypoints.Count-1].rot, rot );
			if( dist < distance ) {
				waypoints.Clear();
			} else if( distance < .1f && angle < 15f ) {
				Waypoint wp = waypoints[waypoints.Count-1];
				wp.pos = target;
				wp.speed = speed;
				wp.rot = rot;
				wp.angvel = angvel;
				totalDist += distance - wp.initdist;
				wp.initdist = distance;
				return;
			}
			waypoints.Add( new Waypoint( target, speed, rot, angvel, 2f*distance, distance ) );
			totalDist += distance;
		}
		//Debug.Log("Drop waypoint at " + target + ", speed + " + speed + ", count = " + waypoints.Count + ", totalDist = " + totalDist);
	}

	public void UpdateWaypoints()
	{
		if( waypoints.Count < 5 ) {
			totalDist = 0f;
			int i;
			for( i = 0; i < waypoints.Count; i++ ) {
				totalDist += waypoints[i].initdist;
			}
		}
		if( waypoints.Count < 2 ) return;
		float dist1, dist2;

		dist1 = Vector2Dist(waypoints[0].pos, transform.position);
		dist2 = Vector2Dist(waypoints[1].pos, transform.position);

		if( dist1 < dist2 && dist1 > waypoints[0].nextdist ) return;
		do {
			totalDist -= waypoints[0].initdist;
			waypoints.RemoveAt(0);
			dist1 = dist2;
			if( waypoints.Count < 2 ) return;
			dist2 = Vector2Dist(waypoints[1].pos, transform.position);
		} while( dist2 < dist1 || dist1 < waypoints[0].nextdist );
	}
}