using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CNet;

namespace CNet
{
    public class CNetBPS : MonoBehaviour
    {
        GUIStyle style = new GUIStyle();
        Rect avginrect, avgoutrect;
        string inbps, outbps;

        void Start()
        {
            style.alignment = TextAnchor.UpperRight;
            style.fontSize = Screen.height * 3 / 100;
            style.normal.textColor = Color.green;
            avginrect = new Rect( Screen.width * 90/100, Screen.height * 5/100, Screen.width * 10/100, Screen.height * 5/100 );
            avgoutrect = new Rect( Screen.width * 90/100, Screen.height * 10/100, Screen.width * 10/100, Screen.height * 5/100 );
            inbps = "Recv: 0 bps";
            outbps = "Send: 0 bps";
        }
        void OnGUI()
        {
            GUI.Label(avginrect, inbps, style);
            GUI.Label(avgoutrect, outbps, style);
        }

        public void Update()
        {
            int total_bytes, count;
            float _outbps, _inbps;

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

            inbps = "Recv: " + _inbps.ToString() + " bps";
            outbps = "Send: " + _outbps.ToString() + " bps";
        }
    }
}
