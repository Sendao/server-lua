using UnityEngine;
using CNet;
using System;
using System.Collections.Generic;

[AddComponentMenu("Transform Tandem Network Connection")]
public class CNetTandemTransformView : MonoBehaviour, ICNetReg, ICNetUpdate
{
	private Rigidbody rb;
	private CNetId cni;

	public float extrapolationFactor = 3f;
	public float angularExtrapolationFactor = 1.5f;

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
		public Vector3 pos, extrapos;
		public float speed, extraspeed;
		public Vector3 rot, extrarot;
		public float angvel, extraangvel;
		public float nextdist;
		public float initdist;
		public Waypoint(Vector3 p, float s, Vector3 r, float a, float n, float i, Vector3 ep, float es, Vector3 er, float ea)
		{
			pos = p;
			speed = s;
			rot = r;
			angvel = a;
			nextdist = n;
			initdist = i;
			extrapos = ep;
			extraspeed = es;
			extrarot = er;
			extraangvel = ea;
		}
	};

	private List<Waypoint> waypoints = new List<Waypoint>();
	private float max_speed=0f;

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
		max_speed = 0f;
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

	private void Update()
	{
		if( !cni.local && waypoints.Count > 0 ) {
			FixedUpdate();
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
			//float distance = (wp.pos - transform.position).magnitude;
			if( waypoints.Count > 1 ) {
				veh.FeedRCCParams( wp.extrapos, wp.extrarot, wp.extraspeed, wp.extraangvel, totalDist, true, waypoints[1].extrapos, max_speed*extrapolationFactor );
			} else {
				veh.FeedRCCParams( wp.extrapos, wp.extrarot, wp.extraspeed, wp.extraangvel, totalDist, false, Vector3.zero, max_speed*extrapolationFactor );
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

	public void RecalculateMaxSpeed()
	{
		max_speed = 0f;
		for( int i=0; i<waypoints.Count; i++ ) {
			if( waypoints[i].speed > max_speed ) {
				max_speed = waypoints[i].speed;
			}
		}
	}

	public Waypoint CalcWaypoint( Vector3 target, float speed, Vector3 rot, float angvel, float nextdist, float initdist, Vector3 start, float startspeed, Vector3 startrot, float startangvel)
	{
		Vector3 extrapos = start + (target - start) * this.extrapolationFactor;
		float extraspeed = startspeed + (speed - startspeed) * this.extrapolationFactor;
		Vector3 extrarot = (startrot + (rot - startrot) * this.angularExtrapolationFactor).normalized;
		float extraangvel = startangvel + (angvel - startangvel) * this.angularExtrapolationFactor;

		Waypoint wt = new Waypoint( target, speed, rot, angvel, nextdist, initdist, extrapos, extraspeed, extrarot, extraangvel );		
		return wt;
	}

	public void AddWaypoint( Vector3 target, float speed, Vector3 rot, float angvel )
	{
		Waypoint p;
		float dist = Vector2Dist(transform.position, target);
		if( target == Vector3.zero ) {
			Debug.LogWarning("AddWaypoint: target is zero");
			return;
		}

		if( speed > max_speed )
			max_speed = speed;

		if( waypoints.Count == 0 ) {
			p = CalcWaypoint( target, speed, rot, angvel, 0.1f, dist, transform.position, 0f, transform.rotation * Vector3.forward, 0f );

			waypoints.Add( p );
			totalDist += dist;
		} else {
			bool recalcMax=false;
			while( waypoints.Count > 12 ) {
				totalDist -= waypoints[0].initdist;
				if( waypoints[0].speed == max_speed ) {
					recalcMax=true;
				}
				waypoints.RemoveAt(0);
			}
			float distance = Vector2Dist(waypoints[waypoints.Count-1].pos, target);
			float angle = Vector3.Angle( waypoints[waypoints.Count-1].rot, rot );
			Waypoint wp = waypoints[waypoints.Count-1];
			if( dist < distance ) { // Waypoints are not linear, clear all waypoints:
				waypoints.Clear();
			} else if( distance < .1f && angle < 5f ) { // Adjust existing waypoint:

				// Recalculate extrapolation:
				if( waypoints.Count < 2 ) {
					Vector3 fwd;
					wp.extrapos = transform.position + (target - transform.position) * this.extrapolationFactor;
					wp.extraspeed = 0f + (speed - 0f) * this.extrapolationFactor;
					fwd = transform.rotation * Vector3.forward;
					wp.extrarot = (fwd + (rot - fwd) * this.angularExtrapolationFactor).normalized;
					wp.extraangvel = 0f + (angvel - 0f) * this.angularExtrapolationFactor;
				} else {
					Waypoint wp2;
					wp2 = waypoints[waypoints.Count-2];
					wp.extrapos = wp2.pos + (target - wp2.pos) * this.extrapolationFactor;
					wp.extraspeed = wp2.speed + (speed - wp2.speed) * this.extrapolationFactor;
					wp.extrarot = (wp2.rot + (rot - wp2.rot) * this.angularExtrapolationFactor).normalized;
					wp.extraangvel = wp2.angvel + (angvel - wp2.angvel) * this.angularExtrapolationFactor;
				}

				wp.pos = target;
				if( wp.speed == max_speed ) {
					recalcMax=true;
				}
				wp.speed = speed;
				wp.rot = rot;
				wp.angvel = angvel;
				if( waypoints.Count > 1 ) {
					distance = Vector2Dist(waypoints[waypoints.Count-2].pos, target);
				} else {
					distance = dist;
				}
				totalDist += distance - wp.initdist;
				wp.initdist = distance;
				if( recalcMax ) {
					RecalculateMaxSpeed();
				}
				return;
			}
			p = CalcWaypoint( target, speed, rot, angvel, 2f*distance, distance, wp.pos, wp.speed, wp.rot, wp.angvel );
			waypoints.Add( p );
			totalDist += distance;
			if( recalcMax ) {
				RecalculateMaxSpeed();
			}
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
		bool recalcMax=false;

		dist1 = Vector2Dist(waypoints[0].pos, transform.position);
		dist2 = Vector2Dist(waypoints[1].pos, transform.position);

		if( dist1 < dist2 && dist1 > waypoints[0].nextdist ) return;
		do {
			totalDist -= waypoints[0].initdist;
			if( waypoints[0].speed == max_speed ) {
				recalcMax=true;
			}
			waypoints.RemoveAt(0);
			dist1 = dist2;
			if( waypoints.Count < 2 ) break;
			dist2 = Vector2Dist(waypoints[1].pos, transform.position);
		} while( dist2 < dist1 || dist1 < waypoints[0].nextdist );

		if( recalcMax ) {
			RecalculateMaxSpeed();
		}
	}

}