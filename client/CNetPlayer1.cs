using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.LEGO.Minifig;

public class CNetPlayer1 : MonoBehaviour
{
	/*
	public override void OnStartLocalPlayer()
	{
		base.OnStartLocalPlayer();
		// Do something
	}
	*/
	public bool isLocalPlayer = false;
	private MinifigController ctrl;
    public int id;
	public Vector3 lastpos;
	public Quaternion lastrot;

	public void Start()
	{
		ctrl = GetComponent<MinifigController>();
		lastpos = transform.position;
		lastrot = transform.rotation;

        if( isLocalPlayer ) {
		    NetSocket.instance.RegisterUser(this);
            ctrl.remoteControl = false;
        } else {
            ctrl.SetInputEnabled(false);
            ctrl.remoteControl = true;
            ctrl.state = MinifigController.State.Automated;
        }
	}

	public void Register()
	{
		NetSocket.instance.RegisterPacket( 0, (int)id, OnPacketReceived, 40 );
        Debug.Log("CNetPlayer1 " + id + " registered");
	}
	public void OnPacketReceived(long ts, NetStringReader stream) {
        //Debug.Log("CNetPlayer1 OnPacketReceived for id " + id);
        Vector3 target = new Vector3();
		Vector3 tgt = new Vector3();

        float x,y,z, rx,ry,rz,rw, tx, ty, tz;

        x = stream.ReadFloat();
        y = stream.ReadFloat();
        z = stream.ReadFloat();

        lastpos.Set(x,y,z);
        //t.position.Set(x,y,z);

        rx = stream.ReadFloat();
        ry = stream.ReadFloat();
        rz = stream.ReadFloat();
        rw = stream.ReadFloat();

        lastrot.Set(rx,ry,rz,rw);

        tx = stream.ReadFloat();
        ty = stream.ReadFloat();
        tz = stream.ReadFloat();

        //tgt.Set(x+tx,y+ty,z+tz);

		float speed = 1.0f;
		float rospeed = 1.0f;
		
        //ctrl.TurnTo( tgt, 0.0f, null, 0.0f, 0.0f, true, rospeed );
		//ctrl.MoveTo( target, 0.0f, null, 0.0f, 0.0f, false, speed );
        //ctrl.TurnToDirection( tgt, 0.0f, 1.0f, false );

	}
	public void Update()
	{
		if( isLocalPlayer ) {
			float dist = Vector3.Distance( lastpos, transform.position );
			float angle = Quaternion.Angle( lastrot, transform.rotation );
			if( dist > 0.1f || angle > 1.0f )
			{
				NetStringBuilder sb = new NetStringBuilder();
				sb.AddFloat( transform.position.x );
				sb.AddFloat( transform.position.y );
				sb.AddFloat( transform.position.z );
				sb.AddFloat( transform.rotation.x );
				sb.AddFloat( transform.rotation.y );
				sb.AddFloat( transform.rotation.z );
				sb.AddFloat( transform.rotation.w );
                Vector3 origVec = Vector3.forward;
                Vector3 newVec = transform.rotation * origVec;
				sb.AddFloat( newVec.x );
				sb.AddFloat( newVec.y );
				sb.AddFloat( newVec.z );
                sb.Reduce();
				NetSocket.instance.SendPacket( 0, (int)id, sb.ptr );
                //Debug.Log("CNetPlayer1 Update: " + id);
				lastpos = transform.position;
				lastrot = transform.rotation;
			}
		} else {
            //float dist = Vector3.Distance( lastpos, transform.position );

            // get a "forward vector" for each rotation
            var forwardA = transform.rotation * Vector3.forward;
            var forwardB = lastrot * Vector3.forward;
            // get a numeric angle for each vector, on the X-Z plane (relative to world forward)
            var angleA = Mathf.Atan2(forwardA.x, forwardA.z) * Mathf.Rad2Deg;
            var angleB = Mathf.Atan2(forwardB.x, forwardB.z) * Mathf.Rad2Deg;
            // get the signed difference in these angles
            float angle = Mathf.DeltaAngle( angleA, angleB );

            //float angle = 0.0f;
            //float angle = Quaternion.Angle( lastrot, transform.rotation );

            Vector3 targetDist = (lastpos - transform.position);
            targetDist.y = 0f;
            float dist = targetDist.magnitude;

            if( dist > 0.1f ) {
                var targetDir = targetDist.normalized;
                Vector3 targetSpeed = (3f*ctrl.maxForwardSpeed*targetDir); // accelerate to full speed. 2 = lag compensation, accelerate faster.

                // on this calculation, we allow some distance overshoot to compensate for the fact that we don't have the latest movement information.
                if( targetSpeed.magnitude * ctrl.acceleration * Time.deltaTime > 3f*dist ) { // don't overshoot.
                    targetSpeed = targetDist * 3f / (ctrl.acceleration*Time.deltaTime);
                }

/* we don't need to do this though.
                if( targetSpeed.sqrMagnitude > 0.05f ) { // if moving in a direction, rotate first To that direction
                    var localTargetSpeed = transform.InverseTransformDirection(targetSpeed);                
                    angle = Vector3.SignedAngle(Vector3.forward, localTargetSpeed.normalized, Vector3.up);
                }*/

                var speedDiff = targetSpeed - ctrl.directSpeed;
                if (speedDiff.sqrMagnitude < ctrl.acceleration * ctrl.acceleration * Time.deltaTime * Time.deltaTime)
                {
                    ctrl.directSpeed = targetSpeed;
                }
                else if (speedDiff.sqrMagnitude > 0.0f)
                {
                    speedDiff.Normalize();

                    ctrl.directSpeed += speedDiff * ctrl.acceleration * Time.deltaTime * 2f;
                }

                if( ctrl.directSpeed.magnitude > ctrl.maxForwardSpeed ) {
                    ctrl.directSpeed = ctrl.directSpeed.normalized * ctrl.maxForwardSpeed;
                }
            } else {
                if( ctrl.directSpeed.x > 0.1f ) {
                    ctrl.directSpeed.x -= Mathf.Min(ctrl.directSpeed.x,1.0f) * (4f * ctrl.acceleration) * Time.deltaTime;
                    if( ctrl.directSpeed.x < 0 ) ctrl.directSpeed.x=0;
                } else if( ctrl.directSpeed.x < -0.1f ) {
                    ctrl.directSpeed.x += Mathf.Min(-ctrl.directSpeed.x,1.0f) * (4f * ctrl.acceleration) * Time.deltaTime;
                    if( ctrl.directSpeed.x > 0 ) ctrl.directSpeed.x=0;
                } else {
                    ctrl.directSpeed.x = 0f;
                }
                if( ctrl.directSpeed.z > 0.1f ) {
                    ctrl.directSpeed.z -= Mathf.Min(ctrl.directSpeed.z,1.0f) * (4f * ctrl.acceleration) * Time.deltaTime;
                    if( ctrl.directSpeed.z < 0 ) ctrl.directSpeed.z=0;
                } else if( ctrl.directSpeed.x < -0.1f ) {
                    ctrl.directSpeed.z += Mathf.Min(-ctrl.directSpeed.z,1.0f) * (4f * ctrl.acceleration) * Time.deltaTime;
                    if( ctrl.directSpeed.z > 0 ) ctrl.directSpeed.z=0;
                } else {
                    ctrl.directSpeed.z = 0f;
                }
            }
            ctrl.moveDelta = new Vector3( ctrl.directSpeed.x, ctrl.moveDelta.y, ctrl.directSpeed.z );
            ctrl.speed = ctrl.directSpeed.magnitude;
            if( Mathf.Abs(angle) > 1.0f ) {
                if( angle > 0 ) {
                    ctrl.rotateSpeed = ctrl.maxRotateSpeed;
                } else {
                    ctrl.rotateSpeed = -ctrl.maxRotateSpeed;
                }

                if( Mathf.Abs(ctrl.rotateSpeed) > Mathf.Abs(angle)/Time.deltaTime ) {
                    ctrl.rotateSpeed = Mathf.Abs(angle)/Time.deltaTime;
                }
            } else {
                ctrl.rotateSpeed = 0.0f;
            }
        }
	}
}