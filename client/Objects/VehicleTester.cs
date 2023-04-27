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


public class VehicleTester : MonoBehaviour
{
	/* Config */
	private float steerInput;
	private float handbrakeInput;
	private float throttleInput;
	private float brakeInput;
	private bool engineRunning;
	private bool headlightsOn;
	private bool brightsOn;
	private bool gearChanging;
	private byte indicators;
	
	private ulong engineStartTime=0;

	private RCC_CarControllerV3 carController;

	public void Awake()
	{
		this.carController = GetComponent<RCC_CarControllerV3>();
		throttleInput = brakeInput = steerInput = 0f;
		handbrakeInput = 0f;
	}

	private int testNo;
	private ulong testStart;
	private Vector3 startPoint;

	public void Start()
	{
		testNo = 0;
		testStart = 0;
		engineRunning = true;
	}

	public void FixedUpdate()
	{
		TimeSpan ts = DateTime.Now - DateTime.UnixEpoch;
		ulong now = (ulong)ts.TotalMilliseconds;

		switch( testNo ) {
			case 0:
				// wait for engine to start
				if( carController.engineRunning ) {
					startPoint = transform.position;
					testNo++;
					testStart = now;
				}
				break;
			case 1:
				// full throttle test
				throttleInput = 1f;
				brakeInput = 0f;
				if( carController.rigid.velocity.magnitude > 12f ) {
					testNo++;
					testStart = now;
				}
				break;
			case 2:
				// stop
				throttleInput = 0f;
				brakeInput = 1f;
				if( carController.rigid.velocity.magnitude < 0.01f ) {
					testNo++;
					testStart = now;
					transform.position = startPoint;
				}
				break;
			case 3:
				// half throttle test
				throttleInput = 0.5f;
				brakeInput = 0f;
				if( carController.rigid.velocity.magnitude > 12f ) {
					testNo++;
					testStart = now;
				}
				break;
			case 4:
				// stop
				throttleInput = 0f;
				brakeInput = 1f;
				if( carController.rigid.velocity.magnitude < 0.01f ) {
					testNo++;
					testStart = now;
					transform.position = startPoint;
				}
				break;
		}
		if( testNo != 0 ) {
			Debug.Log(testNo + " speed: " + carController.rigid.velocity.magnitude + " (" + (now - testStart) + "ms) Gear: " + carController.currentGear);
		}
		FeedRCC();
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

		float useThrottle = throttleInput, useBrake = brakeInput;

        if (!carController.changingGear && !carController.cutGas)
            carController.throttleInput = (carController.direction == 1 ? Mathf.Clamp01(useThrottle) : Mathf.Clamp01(useBrake));
        else
            carController.throttleInput = 0f;

        if (!carController.changingGear && !carController.cutGas) {
            carController.brakeInput = (carController.direction == 1 ? Mathf.Clamp01(useBrake) : Mathf.Clamp01(useThrottle));
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
/* Results:
1 speed: 1.000493 (316ms) Gear: 0 (316ms)
1 speed: 2.047305 (543ms) Gear: 0 (233ms)
1 speed: 3.012459 (750ms) Gear: 0 (207ms)
1 speed: 4.101812 (937ms) Gear: 0 (187ms)
1 speed: 5.09902 (1085ms) Gear: 0 (148ms)
1 speed: 6.018395 (1222ms) Gear: 0 (137ms)
1 speed: 7.091066 (1411ms) Gear: 0 (189ms)
1 speed: 8.046869 (1515ms) Gear: 0 (104ms)
1 speed: 9.001399 (1667ms) Gear: 0 (152ms)
1 speed: 10.10139 (1825ms) Gear: 0 (158ms)
1 speed: 11.0477 (1973ms) Gear: 0 (148ms)
1 speed: 11.98197 (2101ms) Gear: 0 (128ms)
total time: 2101ms
average time: 175ms

2 speed: 11.10477 (155ms) Gear: 0
2 speed: 10.09842 (245ms) Gear: 0
2 speed: 9.101331 (331ms) Gear: 0
2 speed: 8.157456 (397ms) Gear: 0
2 speed: 6.971425 (495ms) Gear: 0
2 speed: 6.001835 (589ms) Gear: 0
2 speed: 5.079964 (658ms) Gear: 0
2 speed: 3.944609 (760ms) Gear: 0
2 speed: 3.046393 (843ms) Gear: 0
2 speed: 2.005633 (952ms) Gear: 0
2 speed: 0.9505282 (1055ms) Gear: 0
2 speed: 0.03893984 (1208ms) Gear: 0

3 speed: 1.021226 (452ms) Gear: 0 (452ms)
3 speed: 1.994714 (795ms) Gear: 0 (343ms)
3 speed: 3.015489 (1150ms) Gear: 0 (355ms)
3 speed: 4.062481 (1463ms) Gear: 0 (313ms)
3 speed: 5.003315 (1708ms) Gear: 0 (245ms)
total time: 1708s
average time: 341ms


fullspeed ~ average 170ms for 1 m/s
halfspeed ~ average 340ms for 1 m/s

to reach 5 m/s would take 850ms and travel 850*2.5 = 2125m/ns = 2.125m/s * 850ms = 1.8125m

to reach 1 m/s would take 170ms and travel 170*0.5 = 85m/ns = 0.085m/s * 170ms = 0.0145m
to reach 1 m/s at half would take 340ms and travel 340*0.5 = 170m/ns = 0.17m/s * 340ms = 0.0578m

that's .05m per 1m/s or .01m at full throttle.

target_speed
target_distance

if target_distance <= .01*target_speed then
	usethrottle = 1
else {
	target = target_speed / target_distance;
	thrust = 0.5 + Mathf.Clamp01((target-20)*(target-20)/6400) * 0.5;
}





*/



// distance = 