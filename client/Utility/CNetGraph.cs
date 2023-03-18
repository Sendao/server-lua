using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CNet
{
	public class CNetGraph : MonoBehaviour
	{
		[SerializeField]
		public Material mat;

		[SerializeField]
		private Material matBack;

		private Rect windowRect = new Rect(20, 20, 125, 50);
		private Dictionary<ulong, float> chartValues = new Dictionary<ulong, float>();
		
		private ulong max_x = 0;
		private ulong min_x = ulong.MaxValue;
		private float max_y = 0;
		private float min_y = float.MaxValue;

		private float myrange = 10000f;
		private float realrange = 0;
		private ulong mydomain = 1000;
		private string myname = "Graph";

		public int windowId = 0;

		public void Awake()
		{
			#if UNITY_EDITOR
			matBack = (Material)AssetDatabase.LoadAssetAtPath("Assets/_IMPUNES/Shaders/AlphaTransparency/grid_s_26.mat", typeof(Material));			
			#endif
		}

		public void Rename( string newname )
		{
			myname = newname;
		}
		public void MoveTo( float x, float y )
		{
			windowRect.x = x;
			windowRect.y = y;
		}
		public void LimitRange( float range )
		{
			myrange = range;
		}
		public void LimitDomain( ulong range )
		{
			mydomain = range;
		}
		public void Add(ulong x, float y)
		{
			if( x < min_x ) min_x = x;
			if( x > max_x ) max_x = x;
			if( y < min_y ) min_y = y;
			if( y > max_y ) max_y = y;

			if( max_y - min_y > realrange ) {
				realrange = max_y - min_y;
			}

			chartValues[x] = y;
			Trim();
		}

		private void Trim()
		{
			var x = max_x - mydomain;
			bool modified=false;

			while( min_x <= x ) {
				if( chartValues.ContainsKey(min_x) ) {
					chartValues.Remove(min_x);
					modified=true;
				}
				min_x++;
			}

			// recalculate range
			if( modified ) {
				max_y = 0;
				min_y = float.MaxValue;
				foreach( var v in chartValues ) {
					if( v.Value < min_y ) min_y = v.Value;
					if( v.Value > max_y ) max_y = v.Value;
				}
				realrange = max_y - min_y;
			}
		}

		private void OnGUI()
		{
			if( NetSocket.Instance.debugMode ) {
			/*
			showWindow0 = GUI.Toggle(new Rect(windowRect.x, windowRect.y-10, 512, 20), showWindow0, "Show " + myname);

			if (showWindow0)
			{*/
			/*windowRect = */GUI.Window(windowId, windowRect, DrawGraph, "");
			//}
			}
		}

		private void DrawGraph(int windowID)
		{
			if (Event.current.type == EventType.Repaint)
			{
				GL.PushMatrix();
				//GL.Clear(true, false, Color.black);
				
				// Draw the lines of the graph
				GL.Begin(GL.LINES);
        		mat.SetPass(0);

				ulong x;
				float totalWidth = (float)windowRect.width - 4f;
				float heightOffset = (float)windowRect.height - 4f;
				float userange;
				if( myrange > realrange ) {
					userange = myrange;
				} else {
					userange = realrange;
				}
				float yfactor = 1f/(float)userange;
				float xfactor = 1f/(float)(max_x+1-min_x)*totalWidth;
				float calcx, calcy, lastx=0, lasty=0;

				//Debug.Log("Window size: " + windowRect.width + "x" + windowRect.height);

				for( x = min_x; x <= max_x; x++ ) {
					if( chartValues.ContainsKey(x) ) {
						float y = (chartValues[x] - min_y) * yfactor;

						calcx = ((float)(x-min_x)*xfactor);
						calcy = (1f - y)*heightOffset;

						if( lastx != 0 ) {
							GL.Vertex3(lastx, lasty, 0);
						} else {
							GL.Vertex3(0, heightOffset, 0);
						}
						GL.Vertex3(calcx, calcy, 0);						
						lastx = calcx;
						lasty = calcy;
					}
				}
				GL.End();
				GL.PopMatrix();
			}

			//GUI.DragWindow(new Rect(0, 0, 10000, 10000));
		}
	}
}
