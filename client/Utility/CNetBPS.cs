using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CNet;
using System;

namespace CNet
{
    public class CNetBPS : MonoBehaviour
    {
        GUIStyle style = new GUIStyle();
        Rect avginrect, avgoutrect, avgrttrect, c2slrect, s2clrect;
        string inbps, outbps, rttavg, c2slavg, s2clavg;

        private CNetGraph inGraph;
        private CNetGraph outGraph;
        private CNetGraph rttGraph;
        private bool hasGraphs=false;

        void Awake()
        {
            var graphs = GetComponents<CNetGraph>();

            if( graphs.Length == 3 ) {
                hasGraphs = true;
                inGraph = graphs[0];
                inGraph.windowId = 0;
                inGraph.LimitDomain( 30 );
                inGraph.LimitRange( 1000 );
                inGraph.Rename("InBPS");
                inGraph.MoveTo( Screen.width - 125, Screen.height * 30/100 );
                outGraph = graphs[1];
                outGraph.windowId = 1;
                outGraph.LimitDomain( 30 );
                outGraph.LimitRange( 1000 );
                outGraph.Rename("OutBPS");
                outGraph.MoveTo( Screen.width - 125, Screen.height * 30/100 + 51 );
                rttGraph = graphs[2];
                rttGraph.windowId = 2;
                rttGraph.LimitDomain( 30 );
                rttGraph.LimitRange( 1000 );
                rttGraph.Rename("RTT");
                rttGraph.MoveTo( Screen.width - 125, Screen.height * 30/100 + 102 );
            }
        }

        void Start()
        {
            style.alignment = TextAnchor.UpperRight;
            style.fontSize = Screen.height * 3 / 100;
            style.normal.textColor = Color.green;
            avginrect = new Rect( Screen.width * 90/100, Screen.height * 5/100, Screen.width * 10/100, Screen.height * 5/100 );
            avgoutrect = new Rect( Screen.width * 90/100, Screen.height * 10/100, Screen.width * 10/100, Screen.height * 5/100 );
            avgrttrect = new Rect( Screen.width * 90/100, Screen.height * 15/100, Screen.width * 10/100, Screen.height * 5/100 );
            c2slrect = new Rect( Screen.width * 90/100, Screen.height * 20/100, Screen.width * 10/100, Screen.height * 5/100 );
            s2clrect = new Rect( Screen.width * 90/100, Screen.height * 25/100, Screen.width * 10/100, Screen.height * 5/100 );
            inbps = "Recv: 0 bps";
            outbps = "Send: 0 bps";
            rttavg = "RTT: 0 ms";
            c2slavg = "C2SL: 0 ms";
            s2clavg = "S2CL: 0 ms";
        }
        void OnGUI()
        {
            if( NetSocket.Instance.debugMode ) {
                GUI.Label(avginrect, inbps, style);
                GUI.Label(avgoutrect, outbps, style);
                GUI.Label(avgrttrect, rttavg, style);
                GUI.Label(c2slrect, c2slavg, style);
                GUI.Label(s2clrect, s2clavg, style);
            }
        }

        private ulong last_update = 0;

        public void Update()
        {
            if( NetSocket.Instance.debugMode ) {
                int total_bytes, count;
                int total_latency;
                float _outbps, _inbps, _rtt, _s2cl, _c2sl;

                total_bytes = NetSocket.Instance.in_bps_measure;
                count = ( total_bytes == 0 ) ? 0 : 1;
                foreach( int bytes in NetSocket.Instance.in_bytes ) {
                    total_bytes += bytes;
                    if( bytes != 0 ) count++;
                }
                if( count == 0 ) _inbps = 0;
                else _inbps = total_bytes / count;

                total_bytes = NetSocket.Instance.out_bps_measure;
                count = ( total_bytes == 0 ) ? 0 : 1;
                foreach( int bytes in NetSocket.Instance.out_bytes ) {
                    total_bytes += bytes;
                    if( bytes != 0 ) count++;
                }
                if( count == 0 ) _outbps = 0;
                else _outbps = total_bytes / count;

                total_bytes = 0;
                count = 0;
                foreach( int bytes in NetSocket.Instance.rtt_times ) {
                    total_bytes += bytes;
                    if( bytes != 0 ) count++;
                }
                if( count == 0 ) _rtt = 0;
                else _rtt = total_bytes / count;

                total_latency = 0;
                count = 0;
                foreach( int s2cl in NetSocket.Instance.s2cl_times ) {
                    total_latency += s2cl;
                    if( s2cl != 0 ) count++;
                }
                if( count == 0 ) _s2cl = 0;
                else _s2cl = total_latency / count;

                total_latency = 0;
                count = 0;
                foreach( int c2sl in NetSocket.Instance.c2sl_times ) {
                    total_latency += c2sl;
                    if( c2sl != 0 ) count++;
                }
                if( count == 0 ) _c2sl = 0;
                else _c2sl = total_latency / count;
                
                inbps = "Recv: " + _inbps.ToString() + " bps";
                outbps = "Send: " + _outbps.ToString() + " bps";
                rttavg = "RTT: " + _rtt.ToString() + " ms";
                s2clavg = "S2CL: " + _s2cl.ToString() + " ms";
                c2slavg = "C2SL: " + _c2sl.ToString() + " ms";

                if( hasGraphs ) {
                    TimeSpan ts = DateTime.Now - DateTime.UnixEpoch;
                    ulong now = (ulong)ts.TotalMilliseconds;

                    if( last_update == 0 || last_update < now - 1000 ) {
                        last_update = now;
                        inGraph.Add( now/1000, _inbps );
                        outGraph.Add( now/1000, _outbps );
                        rttGraph.Add( now/1000, _rtt );
                    }
                }
            }
        }
    }
}
