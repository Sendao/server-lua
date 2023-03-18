using Opsive.Shared.Events;
using Opsive.Shared.Game;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Networking;
using Opsive.UltimateCharacterController.Utility;
using CNet;
using UnityEngine;
using UnityEngine.AI;
using System;
/*
- add a minimum speed when enough more waypoints are available
- precalculate appropriate speed_diff for each gears

*/

public class CNetVehicle : MonoBehaviour, ICNetReg, ICNetUpdate
{
	public float steerInput, steerNet;
	public float handbrakeInput;
	public float throttleInput;
	public float brakeInput;
	public int currentGear;
	public bool engineRunning;
	private bool headlightsOn;
	private bool brightsOn;
	public bool gearChanging;
	private byte indicators;
	
	private ulong engineStartTime=0;

	private CNetId cni;
	private RCC_CarControllerV3 carController;
	private Rigidbody [] rbs;
	private bool hasData = false;

	public bool AllKinematic = true;

	private LagData<Vector3> lagPos;
	private LagData<Vector3> lagRot;

	public void Awake()
	{
		this.carController = GetComponent<RCC_CarControllerV3>();
		this.cni = GetComponent<CNetId>();
		rbs = carController.GetComponentsInChildren<Rigidbody>();

		r_interp = r_target = transform.position;
		r_forward = r_forward_target = transform.forward;
		r_speed = 0.0f;
		r_angular = 0.0f;
		throttleInput = brakeInput = steerInput = 0f;
		handbrakeInput = 1f;
		currentGear = 0;

		lagRot = new LagData<Vector3>(transform.rotation * Vector3.forward, 120f, 120f, 4f, 181.0f, false, false, false, 0.1f, true);
		lagPos = new LagData<Vector3>(Vector3.zero, 2.5f, 100f, 0.5f, 200f, true, true, false, 0.5f);
		lagPos.rot = lagPos.newrot = lagRot.value.normalized;
	}

	public void Start()
	{
		cni.RegisterChild(this);
		Debug.Log("Registered car ctl to register");
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
		r_interp = r_target = transform.position;
		r_forward = r_forward_target = transform.forward;
		r_speed = 0.0f;
		r_angular = 0.0f;

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
		if( !cni.local ) {
			if( !carController.externalController )
				carController.SetExternalControl(true);
			hasData = false;
			if( stickyKinematic || AllKinematic ) {

				float basespeed, baseangular;

				lagPos.goal = r_target;
				lagRot.goal = r_forward_target;
				lagPos.value = transform.position;
				lagRot.value = transform.rotation * Vector3.forward;

				System.TimeSpan ts = System.DateTime.Now - System.DateTime.UnixEpoch;
				ulong now = (ulong)ts.TotalMilliseconds;
				ulong halftime = (now+lagPos.updt)/2;

				//lagRot.value = (transform.rotation * Vector3.forward).normalized;
				baseangular = Mathf.Max(r_angular, 1f);
				Lagger.Update( halftime, ref lagRot, r_angular, true ); // update rotation to halfpoint

				//lagRot.newrot = (transform.rotation * Vector3.forward).normalized;
				basespeed = Mathf.Max(r_speed, 1f);
				Lagger.Speed( halftime, ref lagPos, true, r_speed, true ); // update speed based on rotation change, with rotation to current (halfway point)
				
				//lagPos.value = transform.position;
				lagPos.newrot = lagRot.value.normalized;
				//Lagger.Speed( now, ref lagPos ); // update speed based on rotation change, again, this time with new (target) rotation set (will happen automatically)
				Lagger.Update( now, ref lagPos, r_speed, true ); // update position

				Lagger.Update( now, ref lagRot, r_angular, true ); // update rotation (halfway point to now)


				hasData = false;
				if( (transform.position - lagPos.value).magnitude > 0.001f ) {
					transform.position = lagPos.value;
					hasData = true;
				}

				Quaternion q = new Quaternion();
				q.SetLookRotation( lagRot.value.normalized );
				if( Vector3.Angle(transform.forward, lagRot.value.normalized) > 0.1f ) {
					transform.rotation = q;
					hasData = true;
				}

				if( !hasData ) {
					stickyKinematic=false;
					steerInput = 0f;
					handbrakeInput = 0f;
					FeedRCCTo();
				} else {
					FeedRCC(); // we still want to update the throttle and brake, steering etc
				}
			} else {
				FeedRCCTo();
			}
		}
	}
	public void FixedUpdate()
	{
		FeedRCC();
	}

	public Vector3 r_target, r_interp;
	private Vector3 r_forward, r_forward_target, r_more;
	public float r_speed, r_angular;
	public float r_total;
	public bool r_hasmore;

	private bool gearLock=false;

	private bool checkRCC()
	{
		if( Vector2Dist(r_interp, carController.transform.position) < 0.01 ) {
			//! check for rotation
			return false;
		}
		return true;
	}

	public void FeedRCCParams( Vector3 target, Vector3 fwd, Vector3 target_goal, Vector3 fwd_goal, float speed, float rotSpeed, float totalDist, bool hasMore, Vector3 more )
	{
		r_target = target_goal;
		r_interp = target;
		r_forward = fwd;
		r_forward_target = fwd_goal;
		r_speed = speed;
		r_angular = rotSpeed;
		r_total = totalDist;
		r_hasmore = hasMore;
		r_more = more;
		hasData = true;
	}

	public float helper, helpz;
	public float speed_diff=100f;
	public bool shouldFinish=false;
	public bool shouldSteer=false;
	public float dist, dot;
	public bool inReverse=false;
	public bool stickyKinematic=false;
	public string mode="none";
	public float actualspeed, base_speed, calcspeed, angle;
	private Vector3 dir;
	public float distance_magnifier=10.0f;
	public float max_power=1.0f;
	public bool hitBrake=false;
	public float steer_speed = 3.0f;

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
		float max_throttle;

		if( targetspeed < 3f ) {
			max_throttle = 0.35f;
		} else if( targetspeed < 6f ) {
			max_throttle = 0.5f;
		} else if( targetspeed < 8f ) {
			max_throttle = 0.66f;
		} else if( targetspeed < 16f ) {
			max_throttle = 0.8f;
		} else {
			max_throttle = 1f;
		}
		if( inReverse ) {
			max_throttle = Mathf.Max( max_throttle, 1f - actualspeed*0.5f );
		}

		float sd = speed_diff;
		int i;
		for( i=0; i<carController.gears.Length; i++ ){
			if( (int)actualspeed > carController.gears[i].maxSpeed ) continue;
			sd *= 1/carController.gears[i].maxRatio;
			break;
		}

		if( actualspeed + sd*Time.deltaTime*physical_predict < targetspeed ) {
			if( !shouldReverse ) {
				if( brakeInput < 0.05f ) {
					throttleInput = Mathf.Clamp( throttleInput + physical_wheelpower * sd * Time.deltaTime, 0.0f, max_throttle );
				} else {
					brakeInput = 0f;//Mathf.Clamp( brakeInput - Time.deltaTime * physical_brakepower * sd, 0.0f, 0.8f );
				}
				if( inReverse ) {
					brakeInput = 0f;
					throttleInput = max_throttle;
					mode = "Boost throttle -reverse";
				} else {
					mode = "Boost throttle";
				}
			} else {
				if( throttleInput < 0.05f ) {
					brakeInput = Mathf.Clamp( brakeInput + physical_wheelpower * sd * Time.deltaTime, 0f, max_throttle );
				} else {
					throttleInput = 0f;//Mathf.Clamp( throttleInput - sd * physical_brakepower * Time.deltaTime, 0.0f, 0.8f );
				}
				if( !inReverse ) {
					throttleInput = 0f;
					brakeInput = 1f;
					mode = "Boost reverse +reverse";
				} else {
					mode = "Boost reverse";
				}
			}
		} else if( Mathf.Abs(actualdiff) < actualspeed*0.05f ) { // slow down or maintain speed
			brakeInput = throttleInput = 0f;
			mode = "Freefall";
		} else {
			if( !shouldReverse ) {
				if( inReverse ) {
					brakeInput = 0f;
					throttleInput = 0f;
					mode = "Slow throttle -reverse";
				} else {
					if( throttleInput <= 0.05f ) {
						brakeInput = Mathf.Clamp( brakeInput + physical_brakepower * sd * Time.deltaTime, 0.0f, 0.4f );
					} else {
						throttleInput = Mathf.Clamp( throttleInput - Time.deltaTime * physical_brakepower * sd, 0f, max_throttle );
					}
					mode = "Slow throttle";
				}
			} else {
				throttleInput = 0f;
				if( !inReverse ) {
					brakeInput = 1f;
					mode = "Slow reverse +reverse";
				} else {
					if( brakeInput < 0.05f ) {
						throttleInput = Mathf.Clamp( throttleInput + physical_brakepower * sd * Time.deltaTime, 0f, 0.4f );
					} else {
						brakeInput = Mathf.Clamp( brakeInput - Time.deltaTime * physical_brakepower * sd, 0f, 1.0f );
					}
					mode = "Slow reverse";
				}
			}
		}
	}

	public void FeedRCCTo()
	{
		Vector3 fwd;

		Vector3 moredir;
		float moredist;

		dir = (r_interp - carController.transform.position);
		dir.y = 0;
		dist = Vector1Dist(dir);
		dir = dir.normalized;

		fwd = carController.transform.rotation * Vector3.forward;

		if( r_hasmore ) {
			moredir = (r_more - carController.transform.position);
			moredir.y = 0;
			moredist = Vector1Dist(moredir);
			moredir = moredir.normalized;
		} else {
			moredir = dir;
			moredist = dist;
		}

		Vector3 rot_dir = r_forward_target;

		Vector3 blend_dir = dir;//rot_dir;//(dir + rot_dir).normalized;
		dot = Vector3.Dot( fwd, blend_dir );
		if( Mathf.Abs(dot) < 0.9f ) { // we overshot the target, possibly.
			blend_dir = (moredir + rot_dir).normalized;
			float moredot = Vector3.Dot( fwd, blend_dir );
			dir = moredir;
			dot = moredot;
		}

		angle = Vector3.Angle( fwd, (dir /*+ rot_dir*/).normalized );
		if( angle >= 90 ) {
			angle = 180-angle;
		}

		if( r_total*distance_magnifier > r_speed*remote_speed ) {
			base_speed = r_total*distance_magnifier;
			calcspeed = Mathf.Clamp(calcspeed+physical_speedmag*(r_total*distance_magnifier+r_speed*remote_speed)*0.5f*Time.deltaTime, 0f, base_speed);
		} else if( r_speed > 0.01f ) {
			base_speed = r_speed*remote_speed;
			calcspeed = Mathf.Clamp(calcspeed+physical_speedmag*(r_speed*remote_speed)*Time.deltaTime, 0f, base_speed);
		} else {
			base_speed = 100f;
			calcspeed = Mathf.Clamp(calcspeed*Mathf.Pow(0.5f,Time.deltaTime), 0f, base_speed);
		}

		Vector3 components = Quaternion.Inverse(carController.transform.rotation) * blend_dir;
		actualspeed = Vector1Dist(carController.rigid.velocity);

		helper = components.x;
		helpz = components.z;

		speed_diff = Mathf.Clamp( physical_power*Mathf.Abs(calcspeed - actualspeed), physical_minpower, max_power );		

		hitBrake = false;

		float maxAngle = 40.0f;
		float steer = Mathf.Clamp01( angle / maxAngle );
		if( components.x < 0 )
			steer = -steer;

		/*if( stickyKinematic || AllKinematic ) {
		} else */if( dist > 0.01f ) {
			inReverse = carController.direction == -1;
			if( Mathf.Abs(dot) >= 0.6f ) {
				bool shouldReverse = (dot < 0);
				if( angle <= 40 ) {
					MatchSpeed( calcspeed, shouldReverse, speed_diff );
					shouldFinish=false;

					if( actualspeed < 0.001f ) {
						steerInput = Mathf.Lerp( steerInput, steerNet, Time.deltaTime * steer_speed );
					} else if( steer*steer_magnifier > steerInput ) {
						steerInput = Mathf.Clamp( steerInput + Time.deltaTime*steer_speed, -1f, steer*steer_magnifier );
					} else if( steer*steer_magnifier < steerInput ) {
						steerInput = Mathf.Clamp( steerInput - Time.deltaTime*steer_speed, steer*steer_magnifier, 1f );
					}

				} else {
					shouldFinish=true;
					if( !inReverse ) {
						throttleInput = Mathf.Clamp01( throttleInput * Mathf.Pow( 0.5f, Time.deltaTime ) );
						brakeInput = Mathf.Clamp( brakeInput - Time.deltaTime * speed_diff, 0f, 0.8f );
					} else {
						brakeInput = Mathf.Clamp01( brakeInput *Mathf.Pow( 0.5f, Time.deltaTime ) );
						throttleInput = Mathf.Clamp( throttleInput - Time.deltaTime * speed_diff, 0f, 0.8f );
					}
					mode = "High angle";

					steerInput = Mathf.Lerp( steerInput, 0.0f, Time.deltaTime/2 );
				}
			} else if( dist > 5f ) {
				shouldFinish = true;
				mode = "Too far";
			} else if( dist <= actualspeed ) {
				bool shouldReverse = (dot < 0);
				MatchSpeed( 0.0f, shouldReverse, speed_diff );
				mode = "Brakes";
			} else if( dist > 0.05f && Mathf.Abs(components.x) > Mathf.Abs(components.z) ) {
				mode = "High X";
				shouldFinish = true;
			} else {
				shouldFinish = false;
				bool shouldReverse = (dot < 0);
				MatchSpeed( calcspeed, shouldReverse, speed_diff );
				mode = "Low dot"; // can't steer!
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
			//throttleInput = Mathf.Clamp( throttleInput - Time.deltaTime, 0f, 0.5f );
			//brakeInput = Mathf.Clamp( brakeInput + Time.deltaTime, 0f, 0.5f );
			hitBrake = true;
			hasData = true;
			shouldFinish = false;
			mode = "Low dist";
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

		FeedRCC();
	}

	public float remote_speed = 1.1f;

	public float physical_power = 3f;
	public float physical_minpower = 0.1f;
	public float physical_wheelpower = 3f;
	public float physical_brakepower = 6f;
	public float physical_predict = 10f;
	public float physical_predict_continuous = 5f;
	public float physical_speedmag = 4f;

	public float physadjust_mindist = 0.5f;
	public float physadjust_power = 10.0f;
	public float physadjust_max = 60.0f;
	public float physadjust_minangle = 30.0f;

	public float kinematic_mindist = 0f; // 0.5f
	public float kinematic_minangle = 3f; // 5f

	public float kineturn_minangle = 0.01f; // 0.25f
	public float kineturn_minforceangle = 3f;
	public float kineturn_speed = 3f;

	public float kineturn_power = 1f;
	public float steer_auxpower = 20f;
	public float steer_magnifier = 1.5f;
	public float floordist;

	public void LateUpdate() {
		if( !cni.local && ( hasData || checkRCC() ) ) {
			Vector3 diff = (r_target - carController.transform.position);
			Vector3 global = Quaternion.Inverse(carController.transform.rotation) * diff;

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
				if( ( shouldFinish && ( floordist > 0.05f || angledistance > this.kinematic_minangle ) ) ||
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
				} else if( !carController.rigid.isKinematic && angledistance > physadjust_minangle && ( floordist > physadjust_mindist + (physadjust_mindist*actualspeed)) ) {
				
					float power = Mathf.Clamp( diff.magnitude, 0f, physadjust_max );
					//carController.rigid.AddForce( diff.normalized * power * Time.fixedDeltaTime, ForceMode.VelocityChange );
					carController.transform.position += diff.normalized * power * Time.fixedDeltaTime;
					Debug.Log("Use phys adjust + " + power);
					stickyKinematic = false;
				} else {
					stickyKinematic = false;
				}

				if( (shouldSteer && angledistance > this.kineturn_minangle) || angledistance > this.kineturn_minforceangle ) {
					if( carController.rigid.isKinematic ) {
						Quaternion q = new Quaternion();
						q.SetLookRotation(r_forward, Vector3.up);
						carController.transform.rotation = Quaternion.RotateTowards(carController.transform.rotation, q, Time.deltaTime * this.kineturn_speed );
						Debug.Log("Kinematic turn " + kineturn_speed*Time.deltaTime);
					} else if( actualspeed > 0.05f && floordist > 0.1f ) {
						var yturn = this.kineturn_power*carController.direction*steerInput*steer_auxpower*Time.deltaTime;
						carController.rigid.AddRelativeTorque( new Vector3(0f,yturn,0f), ForceMode.VelocityChange );
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
		}
	}

	public bool firstSkip=false;
	
    private void FeedRCC() {

		TimeSpan ts = DateTime.Now - DateTime.UnixEpoch;
		ulong now = (ulong)ts.TotalMilliseconds;

		if( engineRunning != carController.engineRunning && now > this.engineStartTime + 1100 ) {
			Debug.Log("Change engine: " + engineRunning);
			carController.SetEngine(engineRunning);
			this.engineStartTime = now;
		}

		if( headlightsOn != carController.lowBeamHeadLightsOn ) {
			carController.lowBeamHeadLightsOn = headlightsOn;
		}

		if( brightsOn != carController.highBeamHeadLightsOn ) {
			carController.highBeamHeadLightsOn = brightsOn;
		}

/* let this be automatic instead:
		if( currentGear != carController.currentGear && !carController.changingGear ) {
			Debug.Log("Changing gears to " + currentGear + " from " + carController.currentGear);
			throttleInput = 0.0f;
			brakeInput = 0.0f;
			StartCoroutine( carController.ChangeGear( currentGear ) );
		} */

		float useThrottle = throttleInput, useBrake = brakeInput + (hitBrake ? 0.5f : 0f);

        if (!carController.changingGear && !carController.cutGas)
            carController.throttleInput = (carController.direction == 1 ? Mathf.Clamp01(useThrottle) : Mathf.Clamp01(useBrake));
        else
            carController.throttleInput = 0f;

        if (!carController.changingGear && !carController.cutGas) {
            carController.brakeInput = (carController.direction == 1 ? Mathf.Clamp01(useBrake) : Mathf.Clamp01(useThrottle));
        } else {
            carController.brakeInput = 0f;
		}

        // Feeding steerInput of the RCC.
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
