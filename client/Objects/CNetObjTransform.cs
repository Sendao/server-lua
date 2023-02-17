using UnityEngine;
using CNet;

public class CNetObjTransform : MonoBehaviour, ICNetUpdate
{
	private float distance;
	private float angle;

	private Vector3 direction;
	private Vector3 storedPosition;

	private Vector3 netPosition;
	private Quaternion netRotation;
	
	[Tooltip("Indicates if localPosition and localRotation should be used. Scale ignores this setting, and always uses localScale to avoid issues with lossyScale.")]
	public bool useLocal;

	private bool firstTake = false;
	private CNetId cni;

	public void Awake()
	{
		storedPosition = transform.localPosition;

		netPosition = Vector3.zero;
		netRotation = Quaternion.identity;
	}

	void Start()
	{
		firstTake = true;
		cni = GetComponent<CNetId>();
		if( cni.local ) {
			NetSocket.Instance.RegisterNetObject( this );
		}
	}

	public void Register()
	{
		NetSocket.Instance.RegisterPacket( CNetFlag.ObjTransformUpdate, cni.id, DoUpdate );
	}

	public void Update()
	{
		if (!NetSocket.Instance.authoritative) {
			if (useLocal) {
				transform.localPosition = Vector3.MoveTowards(transform.localPosition, this.netPosition, this.distance  * Time.deltaTime * NetSocket.Instance.updateRate);
				transform.localRotation = Quaternion.RotateTowards(transform.localRotation, this.netRotation, this.angle * Time.deltaTime * NetSocket.Instance.updateRate);
			}
			else
			{
				transform.position = Vector3.MoveTowards(transform.position, this.netPosition, this.distance * Time.deltaTime * NetSocket.Instance.updateRate);
				transform.rotation = Quaternion.RotateTowards(transform.rotation, this.netRotation, this.angle * Time.deltaTime *  NetSocket.Instance.updateRate);
			}
		}
	}

	public void NetUpdate()
	{
		NetStringBuilder sb = new NetStringBuilder();

		// Write
		if (useLocal)
		{
			this.direction = transform.localPosition - this.storedPosition;
			this.storedPosition = transform.localPosition;
			sb.AddVector3(transform.localPosition);
			sb.AddVector3(this.direction);
			sb.AddVector3(transform.localRotation.eulerAngles);
		}
		else
		{
			this.direction = transform.position - this.storedPosition;
			this.storedPosition = transform.position;
			sb.AddVector3(transform.position);
			sb.AddVector3(this.direction);
			sb.AddVector3(transform.rotation.eulerAngles);
		}

		sb.AddVector3(transform.localScale);
		NetSocket.Instance.SendPacket( CNetFlag.ObjTransformUpdate, cni.id, sb );
	}
	public void DoUpdate(ulong ts, NetStringReader stream)
	{
		this.netPosition = stream.ReadVector3();
		this.direction = stream.ReadVector3();

		if (firstTake) {
			if (useLocal) {
				transform.localPosition = this.netPosition;
			} else {
				transform.position = this.netPosition;
			}

			this.distance = 0f;
		} else {
			float lag = Mathf.Abs((float)(NetSocket.Instance.last_netupdate - ts));
			if( lag < 0f ) lag = 0f;
			this.netPosition += this.direction * lag;

			if (useLocal) {
				this.distance = Vector3.Distance(transform.localPosition, this.netPosition);
			} else {
				this.distance = Vector3.Distance(transform.position, this.netPosition);
			}
		}
		float rx, ry, rz;
		rx = stream.ReadFloat();
		ry = stream.ReadFloat();
		rz = stream.ReadFloat();

		this.netRotation = Quaternion.Euler(rx, ry, rz);

		if (firstTake)
		{
			this.angle = 0f;
			if (useLocal) {
				transform.localRotation = this.netRotation;
			} else {
				transform.rotation = this.netRotation;
			}
		} else {
			if (useLocal) {
				this.angle = Quaternion.Angle(transform.localRotation, this.netRotation);
			} else {
				this.angle = Quaternion.Angle(transform.rotation, this.netRotation);
			}
		}

		transform.localScale = (Vector3)stream.ReadVector3();

		firstTake = false;
	}
}
