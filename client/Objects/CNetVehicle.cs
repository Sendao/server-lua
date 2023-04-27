using Opsive.Shared.Events;
using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Networking;
using Opsive.UltimateCharacterController.Utility;
using CNet;
using UnityEngine;
using UnityEngine.AI;
using System;

// TODO:
// - add the other indicators (turn signals)
// - void movement when grinding gears (and switch to kinematic) -- don't do this until finely tuned


public class CNetVehicle : MonoBehaviour, ICNetReg, ICNetUpdate
{
	/* Config */
	public bool AllKinematic = false;
	public float distance_magnifier=1.5f;
	public float max_power=0.66f;
	public float steer_speed = 8f;
	public float thrust_factor = 0.005f;
	public float initial_boost = 2f;

	public float remote_speed = 1f;

	public float physical_power = 0.005f;
	public float physical_minpower = 0.02f;
	public float physical_wheelpower = 5f;
	public float physical_brakepower = 50f;
	public float physical_predict = 33f;
	public float physical_predict_continuous = 6f;
	public float physical_speedmag = 1f;

	public float physadjust_mindist = 0.5f;
	public float physadjust_power = 10.0f;
	public float physadjust_max = 100.0f;
	public float physadjust_minangle = 60.0f;

	public float kinematic_mindist = 10f;
	public float kinematic_minangle = 40f;

	public float kineturn_minangle = 40f;
	public float kineturn_minforceangle = 1f;
	public float kineturn_speed = 0.5f;

	public float kineturn_power = 1f;
	public float steer_auxpower = 10f;
	public float steer_magnifier = 1.5f;
	public float speed_multiplier=1f;

	public float floordist;
	private float steerInput, steerNet;
	private float handbrakeInput;
	public float throttleInput;
	public float brakeInput;
	private int currentGear;
	private bool engineRunning;
	private bool headlightsOn;
	private bool brightsOn;
	private bool gearChanging;
	private byte indicators;
	
	private ulong engineStartTime=0;

	private CNetId cni;
	private RCC_CarControllerV3 carController;
	private Rigidbody [] rbs;
	private bool hasData = false;

	private LagData<Vector3> lagPos;
	private LagData<Vector3> lagRot;

	private LineRenderer ltrace;

	public void Awake()
	{
		this.carController = GetComponent<RCC_CarControllerV3>();
		this.cni = GetComponent<CNetId>();
		rbs = carController.GetComponentsInChildren<Rigidbody>();

		r_target = transform.position;
		r_forward = transform.forward;
		r_speed = 0.0f;
		r_angular = 0.0f;
		throttleInput = brakeInput = steerInput = 0f;
		handbrakeInput = 0f;
		currentGear = 0;

		lagRot = new LagData<Vector3>(transform.rotation * Vector3.forward, 120f, 120f, 4f, 181.0f, false, false, false, 0.1f, true);
		lagPos = new LagData<Vector3>(Vector3.zero, 2.5f, 100f, 0.5f, 200f, true, true, false, 0.5f);
		lagPos.rot = lagPos.newrot = lagRot.value.normalized;

		ltrace = gameObject.AddComponent<LineRenderer>();
        ltrace.material = new Material(Shader.Find("Standard"));
		ltrace.material.color = Color.red;

		AnimationCurve ac = new AnimationCurve();
		ac.AddKey(0f, 1f);
		ac.AddKey(1f, 1f);
		ltrace.widthCurve = ac;
		ltrace.widthMultiplier = 0.1f;

		StartParams();

		//ltrace.positionCount = 2;
		//ltrace.SetPosition(0, transform.position);
		//ltrace.SetPosition(1, transform.position + (transform.forward * 10f));
		Debug.Log("Woke");
	}

	public void Start()
	{
		cni.RegisterChild(this);
	}
	public void Delist()
	{
		if( !cni.local ) {
			NetSocket.Instance.UnregisterPacket( CNetFlag.VehicleControls, cni.id );
		} else {
			NetSocket.Instance.UnregisterPacket( CNetFlag.VehicleControlsRequest, cni.id );
			NetSocket.Instance.UnregisterNetObject( this );
		}
	}
	public void Register()
	{
		throttleInput = brakeInput = steerInput = 0f;
		handbrakeInput = 0f;
		currentGear = 0;
		engineRunning = false;
		r_target = transform.position;
		r_forward = transform.forward;
		r_speed = 0.0f;
		r_angular = 0.0f;

		StartParams();

		if( !cni.local ) {
			NetSocket.Instance.RegisterPacket( CNetFlag.VehicleControls, cni.id, this.OnUpdate );
			NetStringBuilder sb = new NetStringBuilder();
			sb.AddUint(0);
			NetSocket.Instance.SendPacket( CNetFlag.VehicleControlsRequest, cni.id, sb );
		} else {
			NetSocket.Instance.RegisterPacket( CNetFlag.VehicleControlsRequest, cni.id, this.GetUpdate, 2 );
			NetSocket.Instance.RegisterNetObject( this );
		}

		Debug.Log("Register vehicle");
		Rigidbody mainBody = carController.GetComponent<Rigidbody>();
		mainBody.isKinematic = false;
		mainBody.constraints = RigidbodyConstraints.None;
		mainBody.detectCollisions = true;

		r_target = carController.transform.position;

		Rigidbody[] bodies = carController.GetComponentsInChildren<Rigidbody>(true);
		foreach( Rigidbody body in bodies ) {
			if( body != mainBody ) {
				body.isKinematic = false;
				body.detectCollisions = true;
				body.constraints = RigidbodyConstraints.None;
			}
		}

		startingpt = carController.transform.position;
		startspeed = 0f;
	}

	public void GetUpdate(ulong ts, NetStringReader stream)
	{
		ushort dirtyFlags = (ushort)( VehicleDirtyFlags.Handbrake|VehicleDirtyFlags.Gear|VehicleDirtyFlags.Engine|VehicleDirtyFlags.Headlights|VehicleDirtyFlags.Brights|VehicleDirtyFlags.Indicators );

		NetStringBuilder sb = new NetStringBuilder();
		sb.AddUint(dirtyFlags);
		sb.AddShortFloat(handbrakeInput, 1.0f);
		sb.AddByte((byte)currentGear);
		sb.AddBool(engineRunning);
		sb.AddBool(headlightsOn);
		sb.AddBool(brightsOn);
		sb.AddByte(indicators);
		
		NetSocket.Instance.SendDynPacket( CNetFlag.VehicleControls, cni.id, sb );
	}

	public void NetUpdate()
	{
		ushort dirtyFlags = 0;

		/*
		if( throttleInput != carController.throttleInput ) {
			throttleInput = carController.throttleInput;
			dirtyFlags |= (ushort)VehicleDirtyFlags.Throttle;
		}
		if( brakeInput != carController.brakeInput ) {
			brakeInput = carController.brakeInput;
			dirtyFlags |= (ushort)VehicleDirtyFlags.Brake;
		}
		*/

		if( steerInput != carController.steerInput ) {
			steerInput = carController.steerInput;
			dirtyFlags |= (ushort)VehicleDirtyFlags.Steering;
		}

		if( handbrakeInput != carController.handbrakeInput ) {
			handbrakeInput = carController.handbrakeInput;
			dirtyFlags |= (ushort)VehicleDirtyFlags.Handbrake;
		}

		if( currentGear != carController.currentGear ) {
			currentGear = carController.currentGear;
			dirtyFlags |= (ushort)VehicleDirtyFlags.Gear;
		}

		if( engineRunning != carController.engineRunning ) {
			engineRunning = carController.engineRunning;
			dirtyFlags |= (ushort)VehicleDirtyFlags.Engine;
		}

		if( headlightsOn != carController.lowBeamHeadLightsOn ) {
			headlightsOn = carController.lowBeamHeadLightsOn;
			dirtyFlags |= (ushort)VehicleDirtyFlags.Headlights;
		}

		if( brightsOn != carController.highBeamHeadLightsOn ) {
			brightsOn = carController.highBeamHeadLightsOn;
			dirtyFlags |= (ushort)VehicleDirtyFlags.Brights;
		}

		if( gearChanging != carController.changingGear ) {
			gearChanging = carController.changingGear;
			dirtyFlags |= (ushort)VehicleDirtyFlags.GearTransition;
		}

		if( indicators != (byte)carController.indicatorsOn ) {
			indicators = (byte) carController.indicatorsOn;
			dirtyFlags |= (ushort)VehicleDirtyFlags.Indicators;
		}

		if( dirtyFlags == 0 ) return;

		NetStringBuilder sb = new NetStringBuilder();
		sb.AddUint(dirtyFlags);
		/*
		if( (dirtyFlags & (ushort)VehicleDirtyFlags.Throttle) != 0 )
			sb.AddShortFloat(throttleInput, 1.0f);
		if( (dirtyFlags & (ushort)VehicleDirtyFlags.Brake) != 0 )
			sb.AddShortFloat(brakeInput, 1.0f);
		*/
		if( (dirtyFlags & (ushort)VehicleDirtyFlags.Steering) != 0 )
			sb.AddShortFloat(steerInput, 1.0f);
		if( (dirtyFlags & (ushort)VehicleDirtyFlags.Handbrake) != 0 )
			sb.AddShortFloat(handbrakeInput, 1.0f);
		if( (dirtyFlags & (ushort)VehicleDirtyFlags.Gear) != 0 )
			sb.AddByte((byte)currentGear);
		if( (dirtyFlags & (ushort)VehicleDirtyFlags.Engine) != 0 )
			sb.AddBool(engineRunning);
		if( (dirtyFlags & (ushort)VehicleDirtyFlags.Headlights) != 0 )
			sb.AddBool(headlightsOn);
		if( (dirtyFlags & (ushort)VehicleDirtyFlags.Brights) != 0 )
			sb.AddBool(brightsOn);
		if( (dirtyFlags & (ushort)VehicleDirtyFlags.GearTransition) != 0 )
			sb.AddBool(gearChanging);
		if( (dirtyFlags & (ushort)VehicleDirtyFlags.Indicators) != 0 )
			sb.AddByte(indicators);
		
		NetSocket.Instance.SendDynPacket( CNetFlag.VehicleControls, cni.id, sb );
	}

	public void OnUpdate( ulong ts, NetStringReader stream )
	{
		ushort dirtyFlags = (ushort)stream.ReadUint();

		/*
		if( (dirtyFlags & (ushort)VehicleDirtyFlags.Throttle) != 0 ) {
			throttleNet = stream.ReadShortFloat(1.0f);
		}
		if( (dirtyFlags & (ushort)VehicleDirtyFlags.Brake) != 0 ) {
			brakeNet = stream.ReadShortFloat(1.0f);
		}
		*/
		if( (dirtyFlags & (ushort)VehicleDirtyFlags.Steering) != 0 ) {
			steerNet = stream.ReadShortFloat(1.0f);
		}
		if( (dirtyFlags & (ushort)VehicleDirtyFlags.Handbrake) != 0 )
			handbrakeInput = stream.ReadShortFloat(1.0f);
		if( (dirtyFlags & (ushort)VehicleDirtyFlags.Gear) != 0 ) {
			int readgear = stream.ReadByte();

			if( gearLock ) {
				if( currentGear >= 0 && readgear >= 0 ) {
					currentGear = readgear;
				}
			} else {
				currentGear = readgear;
			}
		}
		if( (dirtyFlags & (ushort)VehicleDirtyFlags.Engine) != 0 )
			engineRunning = stream.ReadBool();
		if( (dirtyFlags & (ushort)VehicleDirtyFlags.Headlights) != 0 )
			headlightsOn = stream.ReadBool();
		if( (dirtyFlags & (ushort)VehicleDirtyFlags.Brights) != 0 )
			brightsOn = stream.ReadBool();
		if( (dirtyFlags & (ushort)VehicleDirtyFlags.GearTransition) != 0 )
			gearChanging = stream.ReadBool();
		if( (dirtyFlags & (ushort)VehicleDirtyFlags.Indicators) != 0 )
			indicators = stream.ReadByte();

		hasData = true;
		//FeedRCC();
	}
	public void Update()
	{
		if( cni.registered && !cni.local ) {

			SmoothParams();

			hasData = false;
			if( stickyKinematic || AllKinematic ) {

				float basespeed, baseangular;

				lagPos.goal = r_target;
				lagRot.goal = r_forward;
				lagPos.value = transform.position;
				lagRot.value = transform.rotation * Vector3.forward;

				System.TimeSpan ts = System.DateTime.Now - System.DateTime.UnixEpoch;
				ulong now = (ulong)ts.TotalMilliseconds;
				ulong halftime = (now+lagPos.updt)/2;

				//lagRot.value = (transform.rotation * Vector3.forward).normalized;
				baseangular = Mathf.Max(r_angular, 1f);
				Lagger.Update( halftime, ref lagRot, baseangular, true ); // update rotation to halfpoint

				//lagRot.newrot = (transform.rotation * Vector3.forward).normalized;
				basespeed = Mathf.Max(r_maxspeed, 1f);
				Lagger.Speed( halftime, ref lagPos, true, r_maxspeed, true ); // update speed based on rotation change, with rotation to current (halfway point)
				
				//lagPos.value = transform.position;
				lagPos.newrot = lagRot.value.normalized;
				//Lagger.Speed( now, ref lagPos ); // update speed based on rotation change, again, this time with new (target) rotation set (will happen automatically)
				Lagger.Update( now, ref lagPos, r_maxspeed, true ); // update position

				Lagger.Update( now, ref lagRot, baseangular, true ); // update rotation (halfway point to now)


				hasData = false;
				if( (transform.position - lagPos.value).magnitude > 0.01f ) {
					transform.position = lagPos.value;
					if( (transform.position-lagPos.goal).magnitude < 0.33f ) {
						hasData=false;
					} else {
						hasData = true;
					}
				}

				Quaternion q = new Quaternion();
				q.SetLookRotation( lagRot.value.normalized );
				//if( Vector3.Angle(transform.forward, lagRot.value.normalized) > 1f ) {
					transform.rotation = q;
				//}

				if( !hasData ) {
					stickyKinematic=false;
					//steerInput = 0f;
					handbrakeInput = 0f;
					FeedRCCTo();
				}
			} else {
				//! try to match speed kinetically
				if( useSpeedMatch ) {
					Vector3 startdist = (startingpt - carController.transform.position);
					Vector3 tgtvec = (r_target - carController.transform.position);
					float tgtspeed = Mathf.Lerp( startspeed, r_speed, 2f * startdist.magnitude / tgtvec.magnitude ); // take half the time to get up to speed

					startingpt = carController.transform.position;
					startspeed = carController.rigid.velocity.magnitude;

					Vector3 addvel = (r_target - carController.transform.position).normalized * tgtspeed - carController.rigid.velocity;
					carController.rigid.AddForce( addvel, ForceMode.VelocityChange );
				}
				FeedRCCTo();
			}

			FeedRCC();
		}
	}
	public void FixedUpdate()
	{
		if( cni.registered && !cni.local ) {
			FeedRCC();
		}
	}

	public bool useSpeedMatch = true;
	public Vector3 r_target;
	private Vector3 r_forward, r_more;
	public float r_speed, r_angular;
	public float r_total, r_maxspeed;
	public bool r_hasmore;
	public Vector3 startingpt;
	public float startspeed;

	private Vector3 r_target_goal, r_forward_goal, r_more_goal;
	private float r_total_goal, r_maxspeed_goal, r_speed_goal, r_angular_goal;
	public float inputSmoothingSeconds = 0.2f;

	private bool gearLock=false;

	private bool checkRCC()
	{
		if( Vector2Dist(r_target, carController.transform.position) < 0.01 ) {
			//! check for rotation
			return false;
		}
		return true;
	}

	public void StartParams()
	{
		r_target_goal = r_target = carController.transform.position;
		r_forward_goal = r_forward = carController.transform.rotation * Vector3.forward;
		r_hasmore = false;
		r_more_goal = r_more = Vector3.zero;
		r_total_goal = r_total = 0f;
		r_maxspeed_goal = r_maxspeed = 0f;
		r_speed_goal = r_speed = 0f;
		r_angular_goal = r_angular = 0f;
	}

	private float accumTime=10f;

	public void SmoothParams()
	{
		float fact = Mathf.Min(Time.deltaTime/inputSmoothingSeconds, 1.0f);
		if( accumTime >= inputSmoothingSeconds ) {
			r_target = r_target_goal;
			r_forward = r_forward_goal;
			if( r_hasmore ) {
				r_more = r_more_goal;
			}
			r_total = r_total_goal;
			r_maxspeed = r_maxspeed_goal;
			r_speed = r_speed_goal;
			r_angular = r_angular_goal;
			return;
		}
		accumTime += Time.deltaTime;

		Vector3 diff2 = (r_target_goal-carController.transform.position);
		Vector3 diffnow = (r_target-carController.transform.position);

		r_target = carController.transform.position + ( (1-fact)*diffnow + fact*diff2 );

		r_forward = r_forward_goal;

		r_total = (1f-fact)*r_total + fact*r_total_goal;
		r_maxspeed = (1f-fact)*r_maxspeed + fact*r_maxspeed_goal;
		r_speed = (1f-fact)*r_speed + fact*r_speed_goal;
		r_angular = (1f-fact)*r_angular + fact*r_angular_goal;

		//r_target = Vector3.Lerp( r_target, r_target_goal, Time.deltaTime*inputSmoothingSpeed );
		//r_forward = Vector3.Lerp( r_forward, r_forward_goal, Time.deltaTime*inputSmoothingSpeed );
		//r_total = Mathf.Lerp( r_total, r_total_goal, Time.deltaTime*inputSmoothingSpeed );
		//r_maxspeed = Mathf.Lerp( r_maxspeed, r_maxspeed_goal, Time.deltaTime*inputSmoothingSpeed );
		//r_speed = Mathf.Lerp( r_speed, r_speed_goal, Time.deltaTime*inputSmoothingSpeed );
		//r_angular = Mathf.Lerp( r_angular, r_angular_goal, Time.deltaTime*inputSmoothingSpeed );
		if( r_hasmore ) {
			r_more = r_more_goal;//Vector3.Lerp( r_more, r_more_goal, Time.deltaTime*inputSmoothingSpeed );
		}
	}

	public void FeedRCCParams( Vector3 target_goal, Vector3 fwd_goal, float speed, float rotSpeed, float totalDist, bool hasMore, Vector3 more, float maxspeed )
	{
		ltrace.positionCount = 2;
		ltrace.SetPosition(0, transform.position+new Vector3(0,1,0));
		ltrace.SetPosition(1, (target_goal)+new Vector3(0,1,0));
		if( hasMore ) {
			ltrace.positionCount = 3;
			ltrace.SetPosition(2, (more)+new Vector3(0,1,0));
		}

		r_target_goal = target_goal;
		r_forward_goal = fwd_goal;
		r_speed_goal = speed;
		r_angular_goal = rotSpeed;
		r_total_goal = totalDist;
		r_maxspeed_goal = maxspeed;
		hasData = true;
		
		r_hasmore = hasMore;
		r_more = r_more_goal = more;

		accumTime = 0f;
	}

	private float helper, helpz;
	public float speed_diff=100f;
	private bool shouldFinish=false;
	private bool shouldSteer=false;
	public float dist, dot;
	private bool inReverse=false;
	public bool stickyKinematic=false;
	public string mode="none";
	private float base_speed;
	public float actualspeed, calcspeed, angle;
	private Vector3 dir;
	private bool hitBrake=false;
	private bool initialStartup=true;


	public float Vector2Dist( Vector3 a, Vector3 b )
	{
		return Mathf.Sqrt( (a.x-b.x)*(a.x-b.x) + (a.z-b.z)*(a.z-b.z) );
	}
	public float Vector1Dist( Vector3 a )
	{
		return Mathf.Sqrt( a.x*a.x + a.z*a.z );
	}

	public void MatchSpeed( float targetspeed, bool shouldReverse, float speed_diff )
	{
		float actualdiff = actualspeed - targetspeed;
		float max_throttle, min_throttle;
		float target_distance = r_total;//Vector2Dist(transform.position, r_target);
		float target_thruster = Mathf.Abs(actualdiff/target_distance);
		// get up to 10 in 100m:
		// 10/100 = 0.1
		// get up to 10 in 10m:
		// 10/10 = 1
		// get up to 10 in 1m:
		// 10/1 = 10
		//float target = (target_thruster-20f) * (target_thruster-20f); // this is where the problem is
		float approx_thrust = 0.5f + Mathf.Clamp01(target_thruster*thrust_factor) * 0.5f; // here too.

		min_throttle = approx_thrust * 0.9f;
		max_throttle = Mathf.Clamp01( approx_thrust * 1.1f );

		if( inReverse ) {
			max_throttle = Mathf.Max( max_throttle, 1f - actualspeed );
		}

		float sd = speed_diff;
		int i;
		for( i=0; i<carController.gears.Length; i++ ){
			if( (int)actualspeed > carController.gears[i].maxSpeed ) continue;
			sd *= 1/carController.gears[i].maxRatio;
			break;
		}

		if( actualspeed + sd*Time.deltaTime*physical_predict*physical_wheelpower < targetspeed ) {
			if( !shouldReverse ) {
				brakeInput = 0f;
				if( inReverse ) {
					throttleInput = 1f;
					mode += ", Boost throttle -reverse";
				} else {
					throttleInput = Mathf.Clamp( throttleInput + physical_wheelpower * sd * Time.deltaTime, min_throttle, max_throttle );
					mode += ", Boost throttle";
				}
			} else {
				throttleInput = 0f;
				if( !inReverse ) {
					brakeInput = 1f;
					mode += ", Boost reverse +reverse";
				} else {
					brakeInput = Mathf.Clamp( brakeInput + physical_wheelpower * sd * Time.deltaTime, min_throttle, max_throttle );
					mode += ", Boost reverse";
				}
			}
		} else if( Mathf.Abs(actualdiff) < actualspeed*0.05f || actualspeed <= targetspeed ) { // slow down or maintain speed
			if( !inReverse ) {
				brakeInput = throttleInput = 0f;
			} else {
				brakeInput = 0.2f;
				throttleInput = 0f;
			}
			mode += ", Freefall";
		} else { // slow down
			if( !shouldReverse ) {
				if( inReverse ) {
					brakeInput = 0f;
					throttleInput = 1f;
					mode += ", Slow throttle -reverse";
				} else {
					throttleInput = 0f;
					brakeInput = Mathf.Clamp( brakeInput + physical_brakepower * sd * Time.deltaTime, 0f, min_throttle );
					mode += ", Slow throttle";
				}
			} else {
				if( !inReverse ) {
					throttleInput = 0f;
					brakeInput = 1f;
					mode += ", Slow reverse +reverse";
				} else {
					if( brakeInput < 0.05f ) {
						throttleInput = Mathf.Clamp( throttleInput + physical_brakepower * sd * Time.deltaTime, 0f, min_throttle );
					} else {
						brakeInput = Mathf.Clamp( brakeInput - Time.deltaTime * physical_brakepower * sd, 0.2f, Mathf.Max(min_throttle,0.2f) );
					}
					mode += ", Slow reverse";
				}
			}
		}
	}

	public void FeedRCCTo()
	{
		Vector3 fwd;

		Vector3 moredir;
		float moredist;

		inReverse = carController.direction == -1;

		dir = (r_target - carController.transform.position);
		dir.y = 0;
		dist = Vector1Dist(dir);
		dir = dir.normalized;

		fwd = carController.transform.rotation * Vector3.forward;

		Vector3 global = Quaternion.Inverse(carController.transform.rotation) * dir;
		if( inReverse ) {
			global.z = -global.z;
			dir = carController.transform.rotation * global;
		}/*
		if( inReverse ) {
			dir = (carController.transform.position - r_target);
			dir.y = 0;
			dir = dir.normalized;
		}*/

		if( r_hasmore ) {
			moredir = (r_more - carController.transform.position);
			if( inReverse ) {
				global = Quaternion.Inverse(carController.transform.rotation) * moredir;
				global.z = -global.z;
				moredir = carController.transform.rotation * global;				
			}
			moredir.y = 0;
			moredist = Vector1Dist(moredir);
			moredir = moredir.normalized;
		} else {
			moredir = dir;
			moredist = dist;
		}

		Vector3 rot_dir = r_forward;

		Vector3 blend_dir = (dir + rot_dir).normalized;
		dot = Vector3.Dot( fwd, blend_dir );
		if( Mathf.Abs(dot) < 0.6f ) { // we overshot the target, possibly.
			blend_dir = (moredir + rot_dir).normalized;
			dot = Vector3.Dot( fwd, blend_dir );
		}

		angle = Vector3.Angle( fwd, (blend_dir) );
		if( angle >= 90 ) {
			angle = 180-angle;
		}

		if( r_total > 0.025f ) {
			float xspeed;
			
			if( r_total > 0.66f ) { // start the curve with a higher acceleration
				xspeed = Mathf.Max(r_maxspeed*remote_speed, r_total*distance_magnifier)*initial_boost;
				if( !initialStartup ) {
					Debug.Log("Double speed");
					initialStartup=true;
				}
			} else { // match speed as close as possible during curves and while stopping
				if( initialStartup ) {
					Debug.Log("Switch modes to normal speed now");
					initialStartup=false;
				}
				xspeed = Mathf.Max(r_maxspeed*remote_speed, r_total*distance_magnifier);
			}
			base_speed = xspeed;
			calcspeed = Mathf.Max(xspeed, 0.05f);//Mathf.Clamp(calcspeed+physical_speedmag*(xspeed*remote_speed)*Time.deltaTime, 0f, base_speed);
		} else {
			base_speed = 0f;
			calcspeed = 0f;
		}

		Vector3 components = Quaternion.Inverse(carController.transform.rotation) * blend_dir;
		actualspeed = Vector1Dist(carController.rigid.velocity);

		helper = components.x;
		helpz = components.z;

		speed_diff = Mathf.Clamp( physical_power*Mathf.Abs(calcspeed - actualspeed), physical_minpower, max_power );		
		if( actualspeed < calcspeed/10 ) {
			speed_diff *= speed_multiplier;
			calcspeed *= speed_multiplier;
		}

		hitBrake = false;

		float maxAngle = 40.0f;
		float steer = Mathf.Clamp01( angle / maxAngle );
		if( components.x < 0 ) {
			steer = -steer;
		}
		
		/* technically this may be correct, but most of the time when z < 0, it is very small and then immediately going back to >0.
		if( components.z < 0 ) {
			steer = -steer;
			if( steer > 0.01 || steer < -0.01 ) {
				Debug.Log(components.x + "," + components.z + ": " + steer);
			}
		}*/
		// this is not correct.
		if( inReverse ) {
			steer = -steer;
		}

		if( dist > 0.025f ) {
			if( Mathf.Abs(dot) >= 0.6f ) {
				bool shouldReverse = (dot < 0);
				if( angle <= 75 ) {
					mode = "Normal";
					MatchSpeed( calcspeed, shouldReverse, speed_diff );
					shouldFinish=false;

					if( actualspeed < 0.001f || ( actualspeed < 3f && angle < 2f ) ) {
						steerInput = Mathf.Lerp( steerInput, steerNet, Time.deltaTime * steer_speed );
					} else if( steer*steer_magnifier > steerInput ) {
						steerInput = Mathf.Clamp( steerInput + Time.deltaTime*steer_speed, -1f, steer*steer_magnifier );
					} else if( steer*steer_magnifier < steerInput ) {
						steerInput = Mathf.Clamp( steerInput - Time.deltaTime*steer_speed, steer*steer_magnifier, 1f );
					}

				} else {

					if( angle > 75 || dist > 0.1f ) {
						shouldFinish=true;
					}
					throttleInput = Mathf.Clamp01( throttleInput - physical_brakepower*Time.deltaTime );
					brakeInput = Mathf.Clamp( brakeInput - physical_brakepower*Time.deltaTime, 0f, 0.8f );
					mode = "High angle";
					steerInput = Mathf.Lerp( steerInput, steerNet, Time.deltaTime/2 );
				}
				/*
			} else if( dist > this.kinematic_mindist + actualspeed*this.kinematic_mindist ) {
				shouldFinish = true;
				mode = "Too far";*/
			} else if( dist <= actualspeed ) {
				bool shouldReverse = (dot < 0);
				mode = "Brakes";
				MatchSpeed( 0.0f, shouldReverse, speed_diff );
			} else if( dist > 0.33f && Mathf.Abs(components.x) > Mathf.Abs(components.z) ) {
				mode = "High X";
				shouldFinish = true;
			} else {
				shouldFinish = false;
				bool shouldReverse = (dot < 0);
				mode = "Low dot"; // can't steer!
				MatchSpeed( calcspeed, shouldReverse, speed_diff );
				//steerInput = Mathf.Lerp( steerInput, 0.0f, Time.deltaTime/2 );
			}

			if( brakeInput == 0 && throttleInput == 0 && steerInput == 0 && !shouldFinish ) {
				hasData = false;
			} else {
				hasData = true;
			}
		} else {
			angle = 0f;
			if( actualspeed < 0.2f ) {
				steerInput = Mathf.Lerp( steerInput, steerNet, Time.deltaTime * 2f );
			} else if( steerInput > 0f ) {
				steerInput = Mathf.Clamp( steerInput - Time.deltaTime, 0f, 1f );
			} else {
				steerInput = Mathf.Clamp( steerInput + Time.deltaTime, -1f, 0f );
			}
			bool shouldReverse = (dot < 0);
			mode = "Low dist";
			MatchSpeed( 0.0f, shouldReverse, speed_diff );
			hasData = true;
			shouldFinish = false;
		}
		if( !AllKinematic && !shouldFinish && !stickyKinematic && carController.rigid.isKinematic ) {
			stickyKinematic = false;
			carController.rigid.isKinematic = false;
			carController.rigid.constraints = RigidbodyConstraints.None;
			carController.rigid.detectCollisions = true;
			foreach( Rigidbody rb in rbs ) {
				rb.isKinematic = false;
				rb.constraints = RigidbodyConstraints.None;
				rb.detectCollisions = true;
			}
		}
	}

	public void LateUpdate() {
		if( cni.registered && !cni.local && ( hasData || checkRCC() ) ) {
			Vector3 diff = (r_target - carController.transform.position);
			/*if( inReverse ) {
				diff = -diff;
			}*/
			Vector3 stdiff = diff;
			Vector3 global = Quaternion.Inverse(carController.transform.rotation) * diff;
			if( carController.direction == -1 ) {
				global.z = -global.z;
				diff = carController.transform.rotation * global;
			}

			floordist = Mathf.Abs(global.x) + Mathf.Abs(global.z);
			bool setDist = false;
			if( shouldFinish && floordist < 0.01f ) {
				hasData = false;
			}
			Vector3 myfwd = carController.transform.rotation * Vector3.forward;
			float dot2 = Vector3.Dot(myfwd, r_forward);
			float angledistance = Vector3.Angle(myfwd, r_forward);

			shouldSteer = false;

			if( AllKinematic || stickyKinematic ) { // this is handled in fixedupdate now
				setDist=true;
			} else {
				if( ( shouldFinish ) ||
					stickyKinematic ||
					( floordist > this.kinematic_mindist + actualspeed*this.kinematic_mindist ) ) {
					
					if( floordist <= 0.025f ) {
						stickyKinematic = false;
					} else {
						if( !stickyKinematic ) {
							Debug.Log("Kinematic kick-in: " + (shouldFinish?"finish":"distance") + ", floordist=" + floordist + ", angle=" + angle + ", angledistance=" + angledistance);
							stickyKinematic = true;
						}
						if( !carController.rigid.isKinematic ) {
							carController.rigid.isKinematic = true;
							//carController.rigid.detectCollisions = false;
							foreach( Rigidbody rb in rbs ) {
								rb.isKinematic = true;
								rb.constraints = RigidbodyConstraints.FreezeAll;
								//rb.detectCollisions = false;
							}
							carController.rigid.constraints = RigidbodyConstraints.FreezePosition;
						}
						setDist = true;
					}
					shouldSteer = true;
					TimeSpan ts = DateTime.Now - DateTime.UnixEpoch;
					ulong now = (ulong)ts.TotalMilliseconds;
					
					lagPos.goal = r_target;
					lagRot.goal = r_forward;

					// simulate Time.deltaTime by going back in time a bit
					//now -= (ulong)Time.deltaTime*1000;

					lagPos.value = transform.position;
					lagPos.updt = lagPos.tick = now;
					lagRot.value = (transform.rotation * Vector3.forward).normalized;
					lagRot.updt = lagRot.tick = now;

					//now += (ulong)(Time.deltaTime*500); // move halfway forward and turn
					Lagger.Speed( now, ref lagRot, true, r_angular, true );
					Lagger.Speed( now, ref lagPos, true, r_speed, true );
				} else if( !carController.rigid.isKinematic ) {
					if( dot < -0.4 && carController.direction == 1 && carController.rigid.velocity.magnitude > 0.01f && carController.rigid.velocity.magnitude < 2.5f ) {
						carController.rigid.AddForce( carController.rigid.velocity * -2, ForceMode.VelocityChange );
						//Debug.Log("Use speed reverse " + carController.rigid.velocity.magnitude);
					} else if( !carController.rigid.isKinematic && ( (angledistance > physadjust_minangle) || ( floordist > physadjust_mindist + (physadjust_mindist*actualspeed)) ) ) {
						float power = Mathf.Clamp( diff.magnitude, 0f, physadjust_max );
					//carController.rigid.AddForce( diff.normalized * power * Time.fixedDeltaTime, ForceMode.VelocityChange );
						carController.transform.position += stdiff.normalized * power * Time.fixedDeltaTime;
						//Debug.Log("Use phys adjust + " + power);
						stickyKinematic = false;
					} else {
						stickyKinematic = false;
					}
				} else {
					stickyKinematic = false;
				}

				if( (shouldSteer && angledistance > this.kineturn_minangle) || angledistance > this.kineturn_minforceangle ) {
					if( carController.rigid.isKinematic ) {
						//var yturn = this.kineturn_power* carController.direction* steerInput*steer_auxpower*Time.deltaTime;
						//carController.rigid.AddRelativeTorque( new Vector3(0f,yturn,0f), ForceMode.VelocityChange );
						
						Quaternion q = new Quaternion();
						q.SetLookRotation(diff.normalized, Vector3.up);
						carController.transform.rotation = Quaternion.RotateTowards(carController.transform.rotation, q, Time.deltaTime * this.kineturn_speed );
						
						Debug.Log("Kinematic turn " + kineturn_speed*Time.deltaTime);
					} else if( floordist > 0.02f ) {
						var yturn = this.kineturn_power* carController.direction* steerInput*steer_auxpower*Time.deltaTime;
						//carController.rigid.AddRelativeTorque( new Vector3(0f,yturn,0f), ForceMode.VelocityChange );
						Debug.Log("Add turn torque " + yturn);
					}
				}
			}
			if( setDist ) {
				//Debug.Log("Set " + setDist);
			} else if( !AllKinematic && carController.rigid.isKinematic ) {
				carController.rigid.isKinematic = false;
				carController.rigid.constraints = RigidbodyConstraints.None;
				carController.rigid.detectCollisions = true;
				foreach( Rigidbody rb in rbs ) {
					rb.isKinematic = false;
					rb.constraints = RigidbodyConstraints.None;
					rb.detectCollisions = true;
				}
				stickyKinematic = false;
			}
			FeedRCC();
		}
	}

	public bool firstSkip=false;

    private void FeedRCC() {

		if( !carController.externalController )
			carController.SetExternalControl(true);

		TimeSpan ts = DateTime.Now - DateTime.UnixEpoch;
		ulong now = (ulong)ts.TotalMilliseconds;

		if( engineRunning != carController.engineRunning && now > this.engineStartTime + 1100 ) {
			Debug.Log("Change engine: " + engineRunning);
			carController.SetEngine(engineRunning);
			this.engineStartTime = now;
		} else if( !engineRunning ) {
			return;
		}
		
		if( carController.direction == 1 && !carController.changingGear && brakeInput > 0.9f ) {
			carController.direction = -1;
		} else if( carController.direction == -1 && !carController.changingGear && brakeInput < 0.1f ) {
			carController.direction = 1;
		}

		if( headlightsOn != carController.lowBeamHeadLightsOn ) {
			carController.lowBeamHeadLightsOn = headlightsOn;
		}

		if( brightsOn != carController.highBeamHeadLightsOn ) {
			carController.highBeamHeadLightsOn = brightsOn;
		}

		float useThrottle = throttleInput, useBrake = brakeInput + (hitBrake ? 0.5f : 0f);

        if (!carController.changingGear && !carController.cutGas) {
            carController.throttleInput = Mathf.Clamp01(carController.direction == 1 ? useThrottle : useBrake);
        } else {
            carController.throttleInput = 0f;
		}

        if (!carController.changingGear && !carController.cutGas) {
            carController.brakeInput = Mathf.Clamp01(carController.direction == 1 ? useBrake : useThrottle);
        } else {
            carController.brakeInput = 0f;
		}

		if( steerInput != carController.steerInput ) {
        	carController.steerInput = steerInput;
		}

		if( handbrakeInput != carController.handbrakeInput ) {
	        carController.handbrakeInput = handbrakeInput;
		}

		if( indicators != (byte)carController.indicatorsOn ) {
			carController.indicatorsOn = (RCC_CarControllerV3.IndicatorsOn)indicators;
		}
    }
}
