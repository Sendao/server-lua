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
		public float maxdist;

		public ulong updt, tick;
		public I value;
		public I speed;
		public I rot;
		public I newrot;
		public float powerFactor;
		public bool useLinear;
		public bool firstUpdate;
		public bool debug;
		public bool powerAccel;
		public float lastDist;
		public bool useSpherical;
		public float angularVelocity;

		public LagData( I goal, float maxaccel, float maxspeed, float mindist, float maxdist, bool powerAccel=false, bool uselinear=false, bool debug=false, float powerFactor=0.1f, bool useSpherical=false )
		{
			this.value = this.goal = goal;
			this.maxaccel = maxaccel;
			this.maxspeed = maxspeed;
			this.updt = 0;
			this.tick = 0;
			this.mindist = mindist;
			this.maxdist = maxdist;
			this.firstUpdate = true;
			this.powerAccel = powerAccel;
			this.useLinear = uselinear;
			this.debug = debug;
			this.powerFactor = powerFactor;
			this.useSpherical = useSpherical;
			this.angularVelocity = 0f;
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
			if( val.magnitude > bound ) {
				return val.normalized * bound;
			}
			return val;
		}

		static public void Update( ulong updt, ref LagData<float> obj, float approxVelocity = 0.0f, bool useApprox = false )
		{
			if( obj.firstUpdate ) {
				obj.value = obj.goal;
				obj.firstUpdate = false;
			} else if( obj.speed == 0f && obj.goal == obj.value ) {
				if( updt - obj.updt > 30 ) {
					Speed(updt, ref obj, true);
				}
			} else {
				if( updt - obj.updt > 30 ) {
					Speed(updt, ref obj, true);
				}

				float hangTime = (float)(updt - obj.tick) / 1000f;
				if( hangTime > 1f ) hangTime = 1f;

				float maxAccel;
				if( obj.powerAccel ) {
					maxAccel = (obj.speed * obj.powerFactor + obj.maxaccel) * hangTime;
				} else {
					maxAccel = obj.maxaccel * hangTime;
				}

				float dist = (obj.goal - obj.value);
				if( Mathf.Abs(dist) < 1.5f*obj.mindist ) {
					var targetSpeed = 0f;//(obj.goal-obj.value)/hangTime;
					var speedDiff = targetSpeed - obj.speed;
					if( speedDiff > 0.5f * maxAccel ) {
						if( speedDiff > 0 )
							speedDiff = 0.25f * maxAccel;
						else
							speedDiff = -0.25f * maxAccel;
						obj.speed = obj.speed + speedDiff;
					} else {
						obj.speed = targetSpeed;
					}
				} else if( Mathf.Abs(obj.goal - (obj.value + obj.speed*hangTime)) > Mathf.Abs(dist) ) {
					var targetSpeed = (obj.goal-obj.value)/hangTime;
					var speedDiff = targetSpeed - obj.speed;
					if( speedDiff > 0.25f * maxAccel ) {
						if( speedDiff > 0 )
							speedDiff = 0.1f * maxAccel;
						else
							speedDiff = -0.1f * maxAccel;
						obj.speed = obj.speed + speedDiff;
					} else {
						obj.speed = targetSpeed;
					}
				}
				obj.value = obj.value + obj.speed * hangTime;
			}
			obj.tick = updt;
		}
		static public void Update( ulong updt, ref LagData<Vector3> obj, float approxVelocity = 0.0f, bool useApprox = false )
		{
			if( obj.firstUpdate ) {
				obj.value = obj.goal;
				obj.firstUpdate = false;
				if( obj.debug ) {
					Debug.Log("First value: " + obj.value);
				}
			} else if( obj.useSpherical ) {
				if( updt - obj.updt > 20 ) {
					if( obj.debug ) {
						//Debug.Log("Speed check - " + ( updt - obj.updt ));
					}
					Speed(updt, ref obj, true, approxVelocity, useApprox);
				}
				
				float hangTime = (float)(updt - obj.tick) / 1000f;
				if( hangTime > 0.33f ) hangTime = 0.33f;
				if( hangTime < 0.001f ) {
					if( obj.debug ) {
						//Debug.Log("Hangtime too small: " + hangTime + ", updt=" + updt + ", tick=" + obj.tick);
					}
					return;
				}

				float angles = Vector3.Angle(obj.value, obj.goal);
				if( angles < obj.mindist || angles > obj.maxdist ) {
					obj.value = obj.goal;
					if( obj.debug ) {
						/*
						if( angles > obj.maxdist ) {
							Debug.Log("Angles too large: " + angles + ", maxdist=" + obj.maxdist + " , " + obj.value + " - " + obj.goal);
						} else {
							Debug.Log("Angles too small: " + angles + ", mindist=" + obj.mindist + " , " + obj.value + " - " + obj.goal);
						}
						*/
					}
				} else {
					if( obj.debug ) {
						//Debug.Log(obj.value + "-" + obj.goal + ": " + (hangTime * obj.angularVelocity / angles) + " == Angles: " + angles + ", hangtime=" + hangTime + ", angularVelocity=" + obj.angularVelocity);
					}
					obj.value = Vector3.Slerp(obj.value, obj.goal, 10f*hangTime);
					/*
					if( angles > obj.angularVelocity*hangTime ) {
						obj.value = Vector3.Slerp(obj.value, obj.goal, obj.angularVelocity*hangTime / angles);
					} else {
						//obj.value = Vector3.Slerp(obj.value, obj.goal, 0.717f );
						obj.value = obj.goal;
					}
					*/
					if( obj.debug ) {
						//Debug.Log("New value: " + obj.value);
					}
				}
			} else if( obj.speed == Vector3.zero && (obj.goal-obj.value).magnitude < obj.mindist ) {
				obj.updt = updt;
				/*if( updt - obj.updt > 50 ) {
					Speed(updt, ref obj, true);
				}*/
				obj.value = obj.goal;
			} else {
				if( updt - obj.updt > 20 ) {
					if( obj.debug ) {
						Debug.Log("Speed check - " + ( updt - obj.updt ));
					}
					Speed(updt, ref obj, true, approxVelocity, useApprox);
				}

				float hangTime = (float)(updt - obj.tick) / 1000f;
				if( hangTime > 1f ) hangTime = 1f;
				if( hangTime < 0.001f ) {
					if( obj.debug ) {
						Debug.Log("Hangtime too small: " + hangTime + ", updt=" + updt + ", tick=" + obj.tick);
					}
					return;
				}

				float maxAccel;
				if( obj.powerAccel ) {
					maxAccel = (obj.speed.magnitude * obj.powerFactor + obj.maxaccel) * hangTime;
				} else {
					maxAccel = obj.maxaccel * hangTime;
				}

				float dist = (obj.goal - obj.value).magnitude;
				if( dist < obj.mindist ) {
					obj.speed = (obj.speed + (obj.goal - obj.value)) / (2);
					obj.value = obj.goal - obj.speed*hangTime; //(obj.goal - obj.value) / (hangTime);
					if( obj.debug ) {
						Debug.Log("Braking1 - dist=" + dist + ", speed=" + obj.speed);
					}
				} else if( (obj.goal - (obj.value + obj.speed*hangTime)).magnitude > dist ) { // we are overshooting. by a lot.
					var targetSpeed = obj.speed * (1.0f - Clamp( hangTime/0.5f, 1.0f )); // but take half a second to slow down.
					var speedDiff = (targetSpeed - obj.speed) / hangTime;
					if( speedDiff.magnitude > maxAccel ) {
						speedDiff = speedDiff.normalized * maxAccel;
						obj.speed = obj.speed + speedDiff * hangTime;
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
				if( obj.debug ) {
					Debug.Log("Update " + obj.value +" + " + obj.speed*hangTime + ", hangTime=" + hangTime);
				}
				obj.value = obj.value + obj.speed * hangTime;
			}
			obj.tick = updt;
		}



		static public void Speed( ulong updt, ref LagData<float> obj, bool updateTicks=true, float approxVelocity = 0.0f, bool useApprox = false )
		{
			float delta = obj.goal - obj.value;
			if( Mathf.Abs(delta) > obj.maxdist ) {
				obj.value = obj.goal;
				if( updateTicks ) {
					obj.updt = updt;
				}
				obj.speed = 0;
				return;
			}
			float targetSpeed, targetAccel;
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

			float maxAccel;

			if( obj.powerAccel ) {
				maxAccel = obj.maxaccel + ( obj.speed * obj.powerFactor );
			} else {
				maxAccel = obj.maxaccel;
			}

			if( Mathf.Abs(delta) < obj.mindist ) {
				targetSpeed = 0;
			
				targetAccel = (targetSpeed - obj.speed) / hangTime;
				targetAccel = Clamp( targetAccel, maxAccel );
				obj.speed = obj.speed + targetAccel * hangTime;
				if( updateTicks ) {
					obj.updt = updt;
				}
				return;
			}


			targetSpeed = ( delta / hangTime );
			targetAccel = (targetSpeed - obj.speed) / hangTime;

			if (targetAccel > maxAccel)
				targetAccel = maxAccel;
			else if (targetAccel < -maxAccel)
				targetAccel = -maxAccel;

			targetSpeed = obj.speed + targetAccel * hangTime;
			if (targetSpeed > obj.maxspeed)
				targetSpeed = obj.maxspeed;
			else if (targetSpeed < -obj.maxspeed)
				targetSpeed = -obj.maxspeed;

			if( delta > 0 && targetSpeed*fullHang > delta ) {
				targetSpeed = delta;
			} else if( delta < 0 && targetSpeed*fullHang < delta ) {
				targetSpeed = delta;
			}

			if( Mathf.Abs(obj.goal - (obj.value + targetSpeed*hangTime)) > Mathf.Abs(delta) ) {
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
		}
		static public void Speed( ulong updt, ref LagData<Vector3> obj, bool updateTicks=true, float approxVelocity = 0.0f, bool useApprox = false )
		{
			if( obj.firstUpdate ) {
				obj.speed = Vector3.zero;
				if( updateTicks ) {
					obj.updt = updt;
				}
				obj.value = obj.goal;
				return;
			}

			Vector3 delta = obj.goal - obj.value;
			float dist = delta.magnitude;
			/*
			if( obj.debug && Mathf.Abs(dist - obj.lastDist) > 0.1f) {
				Debug.Log("Distance = " + dist);
				obj.lastDist = dist;
			}
			*/
			if( dist > obj.maxdist ) {
				obj.value = obj.goal;
				if( updateTicks ) {
					obj.updt = updt;
				}
				obj.speed = Vector3.zero;
				return;
			}
			Vector3 targetSpeed, targetAccel;
			TimeSpan ts = DateTime.Now - DateTime.UnixEpoch;
			ulong now = (ulong)ts.TotalMilliseconds;
			float hangTime = (float)(updt - obj.updt) / 1000f;
			float fullHang = (float)(now - obj.updt) / 1000f;
			float longHang = (float)(updt - obj.tick) / 1000f;

			if( fullHang < 0.0001f) {
				if( obj.debug ) {
					Debug.Log("fullHang < 0:" + fullHang);
				}
				return;
			}
			if( hangTime < 0.0001f ) {
				if( obj.debug ) {
					Debug.Log("hangTime < 0:" + fullHang);
				}
				return;
			}
			if( hangTime > .33f ) hangTime = 0.33f;
			if( fullHang > .66f ) fullHang = 0.66f;

			float maxAccel;
			if( obj.useSpherical ) {
				float totalAngle = Vector3.Angle(obj.value, obj.goal);

				if( obj.debug ) {
					//Debug.Log("total angle: " + totalAngle + " between " + obj.value + " and " + obj.goal);
				}

				if( useApprox ) {
					maxAccel = (obj.maxaccel + approxVelocity*1.2f-obj.angularVelocity) / (3*hangTime);
				} else {
					maxAccel = obj.maxaccel / (3*hangTime);
				}

				if( totalAngle < obj.mindist ) {
					obj.angularVelocity /= 2;
					/*
				} else if( maxAccel>0 && obj.angularVelocity > 2*totalAngle ) {
					if( obj.debug ) {
						Debug.Log("Braking " + maxAccel + ", " + hangTime + ", " + approxVelocity + ": " + obj.angularVelocity);
					}
					obj.angularVelocity -= maxAccel * 2f * hangTime;
					if( obj.angularVelocity < 0f ) obj.angularVelocity = 0f;
					*/
				} else {
					if( obj.debug ) {
						Debug.Log("Accel " + maxAccel + ", " + hangTime + ", " + approxVelocity + ": " + obj.angularVelocity);
					}
					if( maxAccel < 0 ) {
						obj.angularVelocity += maxAccel;// * 0.333f * hangTime;
					} else {
						obj.angularVelocity += maxAccel;// * 1.777f * hangTime;
					}
				}
				obj.angularVelocity = Clamp( obj.angularVelocity, obj.maxspeed );

				if( updateTicks ) {
					obj.updt = updt;
				}
				return;
			}

			if( useApprox ) {
				if( approxVelocity < 0.25f ) {
					if( obj.powerAccel ) {
						maxAccel = obj.maxaccel + (obj.speed.magnitude*obj.powerFactor);
					} else {
						maxAccel = obj.maxaccel;
					}
				} else {
					if( obj.powerAccel ) {
						maxAccel = obj.maxaccel + (obj.speed.magnitude*obj.powerFactor) * 0.5f + (approxVelocity-obj.speed.magnitude) * 0.6f;
					} else {
						maxAccel = obj.maxaccel + (approxVelocity-obj.speed.magnitude) * 1.2f;
					}
				}
			} else if( obj.powerAccel ) {
				maxAccel = obj.maxaccel + ( obj.speed.magnitude * obj.powerFactor );
			} else {
				maxAccel = obj.maxaccel;
			}

			if( dist < obj.mindist ) {
				targetSpeed = Vector3.zero;
			
				targetAccel = (targetSpeed - obj.speed) / hangTime;
				targetAccel = Clamp( targetAccel, maxAccel );
				obj.speed = obj.speed + targetAccel * hangTime;
				if( updateTicks ) {
					obj.updt = updt;
				}
				return;
			}
			targetSpeed = delta / ( hangTime );//( delta / hangTime );

			
			Vector3 linearSpeed;
			if( obj.useLinear ) {
				Quaternion q = new Quaternion(), r = new Quaternion(), s = new Quaternion();
				q.SetLookRotation( obj.newrot );

				r.SetLookRotation( obj.rot );
				s = Quaternion.Inverse(r);

				linearSpeed = s * obj.speed;
				// let it drift (a lot)
				linearSpeed.x *= Clamp( Mathf.Pow( 0.40f, hangTime ), 1.0f );
				linearSpeed.y *= Clamp( Mathf.Pow( 0.05f, hangTime ), 1.0f );
				linearSpeed = q * linearSpeed;
				
				targetAccel = (targetSpeed - linearSpeed) / hangTime;
				/*
				targetAccel = q * targetAccel;
				// ... let it drift still.
				targetAccel.x *= Clamp( Mathf.Pow( 0.75f, hangTime ), 1.0f );
				targetAccel.y *= Clamp( Mathf.Pow( 0.1f, hangTime ), 1.0f );
				targetAccel = t * targetAccel;
				*/
				targetAccel = Clamp( targetAccel, maxAccel );
				
				targetSpeed = linearSpeed + targetAccel * hangTime;
				//Debug.Log("Speed components " + obj.speed + ", " + linearSpeed + ", " + (targetAccel*hangTime) + ", " + hangTime);

				obj.rot = obj.newrot;
			} else {
				targetAccel = (targetSpeed - obj.speed) / hangTime;
				targetAccel = Clamp( targetAccel, maxAccel );
				targetSpeed = obj.speed + targetAccel * hangTime;
			}

			targetSpeed = Clamp( targetSpeed, obj.maxspeed );

			if( (obj.goal - (obj.value + targetSpeed*hangTime)).magnitude > dist ) {
				var targetSpeed2 = (obj.goal-obj.value)/(2*hangTime);
				obj.speed = targetSpeed2;
				if( obj.debug ) {
					Debug.Log("Braking3 -" + targetSpeed2 + " - " + dist + ", rot: " + obj.rot);
				}
			} else {
				obj.speed = targetSpeed;
				if( obj.debug ) {
					Debug.Log("Normal - " + obj.speed + " - " + dist + ", rot: " + obj.rot);
				}
			}
			if( obj.debug ) {
				Debug.Log("Delta: " + delta + ", speed = " + obj.speed);
			}

			if( updateTicks ) {
				obj.updt = updt;
			}
		}
	}
}