using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
        inbps = "0";
        outbps = "0";
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

        total_bytes = NetSocket.instance.in_bps_measure;
        count = ( total_bytes == 0 ) ? 0 : 1;
        foreach( int bytes in NetSocket.instance.in_bytes ) {
            total_bytes += bytes;
            if( bytes != 0 ) count++;
        }
        if( count == 0 ) _inbps = 0;
        else _inbps = total_bytes / count;

        total_bytes = NetSocket.instance.out_bps_measure;
        count = ( total_bytes == 0 ) ? 0 : 1;
        foreach( int bytes in NetSocket.instance.out_bytes ) {
            total_bytes += bytes;
            if( bytes != 0 ) count++;
        }
        if( count == 0 ) _outbps = 0;
        else _outbps = total_bytes / count;

        inbps = _inbps.ToString();
        outbps = _outbps.ToString();
    }
}
