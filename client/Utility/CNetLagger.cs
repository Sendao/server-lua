using UnityEngine;
using System;

namespace CNet
{
	public class LagData<I>
	{
		public I goal;
		public float maxaccel;
		public float maxspeed;
		public float mindist;

		public ulong updt, tick;
		public I value;
		public I speed;
		public bool firstUpdate;
		public bool debug;

		public LagData( I goal, float maxaccel, float maxspeed, float mindist, bool debug=false )
		{
			this.value = this.goal = goal;
			this.maxaccel = maxaccel;
			this.maxspeed = maxspeed;
			this.updt = 0;
			this.tick = 0;
			this.mindist = mindist;
			this.firstUpdate = true;
			this.debug = debug;
		}
	}

	// updt is the timestamp of the update
	// obj.goal should be the update's value
	// obj.value should be initialized to the current value
	// obj.speed should be set to 0 when not moving

	public class Lagger
	{
		private static float Clamp( float val, float bound )
		{
			if( val > bound ) return bound;
			else if( val < -bound ) return -bound;
			else return val;
		}
		private static Vector3 Clamp( Vector3 val, float bound )
		{
			if( val.sqrMagnitude > bound ) {
				return val.normalized * bound;
			}
			return val;
		}

		static public void Update( ulong updt, ref LagData<float> obj )
		{
			if( obj.firstUpdate ) {
				obj.value = obj.goal;
				obj.firstUpdate = false;
			} else {
				float hangTime = (float)(updt - obj.tick) / 1000f;
				if( hangTime > 1f ) hangTime = 1f;
				if( Mathf.Abs(obj.goal - obj.value) * hangTime < obj.mindist ) {
					var targetSpeed = (obj.goal-obj.value)/hangTime;
					var speedDiff = targetSpeed - obj.speed;
					if( speedDiff > obj.maxaccel * hangTime ) {
						if( speedDiff > 0 )
							speedDiff = obj.maxaccel * hangTime;
						else
							speedDiff = -obj.maxaccel * hangTime;
						obj.speed = obj.speed + speedDiff;
					} else {
						obj.speed = targetSpeed;
					}
				}
				float dist = (obj.goal - obj.value);
				if( Mathf.Abs(obj.goal - (obj.value + obj.speed*hangTime)) > dist ) {
					var targetSpeed = (obj.goal-obj.value)/hangTime;
					var speedDiff = targetSpeed - obj.speed;
					if( speedDiff > obj.maxaccel * hangTime ) {
						if( speedDiff > 0 )
							speedDiff = obj.maxaccel * hangTime;
						else
							speedDiff = -obj.maxaccel * hangTime;
						obj.speed = obj.speed + speedDiff;
					} else {
						obj.speed = targetSpeed;
					}
				}
				obj.value = obj.value + obj.speed * hangTime;
			}
			obj.tick = updt;
		}
		static public void Speed( ulong updt, ref LagData<float> obj )
		{
			float delta = obj.goal - obj.value;
			TimeSpan ts = DateTime.Now - DateTime.UnixEpoch;
			ulong now = (ulong)ts.TotalMilliseconds;
			float hangTime = (float)(updt - obj.updt) / 1000f;
			float fullHang = (float)(now - obj.updt) / 1000f;

			if( fullHang > hangTime*2f )
				fullHang = hangTime*2f;

			if( hangTime < 0 ) {
				Debug.Log("Hang time should never be <0");
				hangTime = 0.01f;
			}
			if( fullHang < 0 ) {
				Debug.Log("Full hang time should never be <0");
				fullHang = 0.01f;
			}

			if( hangTime > .33f ) hangTime = 0.33f;
			if( fullHang > .33f ) fullHang = 0.33f;

			float targetSpeed = 1f * ( delta / hangTime );
			float targetAccel = 1f * (targetSpeed - obj.speed) / hangTime;

			if (targetAccel > obj.maxaccel)
				targetAccel = obj.maxaccel;
			else if (targetAccel < -obj.maxaccel)
				targetAccel = -obj.maxaccel;

			targetSpeed = obj.speed + targetAccel * fullHang;

			if (targetSpeed > obj.maxspeed)
				targetSpeed = obj.maxspeed;
			else if (targetSpeed < -obj.maxspeed)
				targetSpeed = -obj.maxspeed;

			if( delta > 0 && targetSpeed*fullHang > delta ) {
				targetSpeed = delta;
			} else if( delta < 0 && targetSpeed*fullHang < delta ) {
				targetSpeed = delta;
			}

			obj.speed = targetSpeed;
			obj.updt = updt;
		}
		static public void Update( ulong updt, ref LagData<Vector3> obj )
		{
			if( obj.firstUpdate ) {
				obj.value = obj.goal;
				obj.firstUpdate = false;
				if( obj.debug ) {
					Debug.Log("First value: " + obj.value);
				}
			} else if( obj.speed == Vector3.zero && obj.goal == obj.value ) {
				if( updt - obj.updt > 50 ) {
					Speed(updt, ref obj, true);
				}
			} else {
				if( updt - obj.updt > 50 ) {
					Speed(updt, ref obj, true);
				}

				float hangTime = (float)(updt - obj.tick) / 1000f;
				if( hangTime > 1f ) hangTime = 1f;
				float dist = (obj.goal - obj.value).magnitude;
				if( dist < 1.5f*obj.mindist ) {
					var targetSpeed = Vector3.zero;
					var speedDiff = targetSpeed - obj.speed;
					if( speedDiff.magnitude > 0.5f * obj.maxaccel * hangTime ) {
						speedDiff = speedDiff.normalized * 0.25f * obj.maxaccel * hangTime;
						obj.speed = obj.speed + speedDiff;
						if( obj.debug ) {
							Debug.Log("Braking1a");
						}
					} else {
						obj.value = obj.goal;
						obj.speed = Vector3.zero;
						if( obj.debug ) {
							Debug.Log("Braking1b");
						}
					}
				} else if( (obj.goal - (obj.value + obj.speed*hangTime)).magnitude > dist ) {
					var targetSpeed = Vector3.zero;//(obj.goal-obj.value)/(hangTime);
					var speedDiff = targetSpeed - obj.speed;
					if( speedDiff.magnitude > 0.25f * obj.maxaccel * hangTime ) {
						speedDiff = speedDiff.normalized * 0.1f * obj.maxaccel * hangTime;
						obj.speed = obj.speed + speedDiff;
						if( obj.debug ) {
							Debug.Log("Braking2a");
						}
					} else {
						obj.speed = targetSpeed;
						if( obj.debug ) {
							Debug.Log("Braking2b");
						}
					}
				}
				obj.value = obj.value + obj.speed * hangTime;
			}
			obj.tick = updt;
		}
		static public void Speed( ulong updt, ref LagData<Vector3> obj, bool updateTicks=true )
		{
			if( obj.firstUpdate ) {
				obj.speed = Vector3.zero;
				if( updateTicks ) {
					obj.updt = updt;
				}
				return;
			}
			Vector3 delta = obj.goal - obj.value;
			Vector3 targetSpeed, targetAccel;
			TimeSpan ts = DateTime.Now - DateTime.UnixEpoch;
			ulong now = (ulong)ts.TotalMilliseconds;
			float hangTime = (float)(updt - obj.updt) / 1000f;
			float fullHang = (float)(now - obj.updt) / 1000f;

			if( fullHang < 0.0001f) {
				if( obj.debug ) {
					Debug.Log("fullHang < 0:" + fullHang);
				}
				return;
			}
			if( hangTime < 0.0001f ) {
				hangTime = fullHang;
			}
			if( hangTime > .33f ) hangTime = 0.33f;
			if( fullHang > .66f ) fullHang = 0.66f;

			if( delta.magnitude < obj.mindist ) {
				targetSpeed = Vector3.zero;
			
				targetAccel = (targetSpeed - obj.speed) / hangTime;
				targetAccel = Clamp( targetAccel, obj.maxaccel );
				obj.speed = obj.speed + targetAccel * hangTime;
				if( updateTicks ) {
					obj.updt = updt;
				}
				return;
			}
			targetSpeed = ( delta / hangTime );
			targetAccel = (targetSpeed - obj.speed) / hangTime;
			targetAccel = Clamp( targetAccel, obj.maxaccel );

			targetSpeed = obj.speed + targetAccel * hangTime;
			targetSpeed = Clamp( targetSpeed, obj.maxspeed );

			float dist = (obj.goal - obj.value).magnitude;
			if( (obj.goal - (obj.value + targetSpeed*hangTime)).magnitude > dist ) {
				var targetSpeed2 = (obj.goal-obj.value)/(hangTime);
				obj.speed = targetSpeed2;
				if( obj.debug ) {
					Debug.Log("Braking3");
				}
			} else {
				obj.speed = targetSpeed;
			}

			if( updateTicks ) {
				obj.updt = updt;
			}

			if( obj.debug ) {
				Debug.Log("Speed: " + obj.speed);
			}
		}
	}
}