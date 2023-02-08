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
	private CNetId ident;
	private MinifigController ctrl;
	public Vector3 lastpos;
	public Quaternion lastrot;

	public void Start()
	{
		ident = GetComponent<CNetId>();
		ctrl = GetComponent<MinifigController>();
		lastpos = transform.position;
		lastrot = transform.rotation;
	}

	public void OnPacketReceived(long id, NetStringReader stream) {
        Vector3 target = new Vector3();
		Vector3 tgt = new Vector3();

        float x,y,z, rx,ry,rz,rw, tx, ty, tz;

        x = stream.ReadFloat();
        y = stream.ReadFloat();
        z = stream.ReadFloat();

        target.Set(x,y,z);
        //t.position.Set(x,y,z);
        

        rx = stream.ReadFloat();
        ry = stream.ReadFloat();
        rz = stream.ReadFloat();
        rw = stream.ReadFloat();

        //t.rotation.Set(rx,ry,rz,rw);

        tx = stream.ReadFloat();
        ty = stream.ReadFloat();
        tz = stream.ReadFloat();

        tgt.Set(tx,ty,tz);

		float speed = 1.0f;
		float rospeed = 1.0f;
		
		ctrl.Follow2( target, 0.0f, null, 0.0f, 0.0f, true, speed, rospeed, tgt );
	}

	public void Register()
	{
		NetSocket.instance.RegisterPacket( 0, (int)ident.id, OnPacketReceived, 40 );
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
				sb.AddFloat( ctrl.currentTurnTarget.target.x );
				sb.AddFloat( ctrl.currentTurnTarget.target.y );
				sb.AddFloat( ctrl.currentTurnTarget.target.z );
				NetSocket.instance.SendPacket( 0, (int)ident.id, sb.ptr );
				lastpos = transform.position;
				lastrot = transform.rotation;
			}
		}
	}
}