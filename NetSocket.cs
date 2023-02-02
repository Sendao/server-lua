using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using System.IO.Compression;

public class NetSocket : MonoBehaviour
{
    private readonly object _sendQLock = new object();
    private readonly Queue<byte[]> sendQ = new Queue<byte[]>();
    private readonly AutoResetEvent _sendQSig = new AutoResetEvent(false);

    private readonly object _recvQLock = new object();
    private readonly Queue<byte[]> recvQ = new Queue<byte[]>();

    private Thread _recvThread;
    private Thread _sendThread;

    private Socket ws;

    private readonly object _localIpEndLock = new object();
    private IPEndPoint localEnd;

    void Start()
    {
        //IPHostEntry ipHost = Dns.GetHostEntry("127.0.0.1");
        IPAddress ipAddr = IPAddress.Parse("127.0.0.1"); //ipHost.AddressList[0];

        localEnd = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2038);

        ws = new Socket(ipAddr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        _sendThread = new Thread(new ThreadStart(SendThread));
        _sendThread.IsBackground = true;
        _recvThread = new Thread(new ThreadStart(RecvThread));
        _recvThread.IsBackground = true;

        Debug.Log("Connecting to server...");
        ws.Connect(localEnd);
        Debug.Log("Socket connected to " + ws.RemoteEndPoint.ToString());

        _sendThread.Start();
        _recvThread.Start();

        // For testing purposes, let's request a file list
        SendMessage( (char)3, null );
    }

    public void SendMessage( char cmd, byte[] data )
    {
        byte[] msg = new byte[1];
        msg[0] = (byte)cmd;
        send(msg);

        if( data != null )
            send(data);
        _sendQSig.Set();
    }

    public void send(byte[] data) {
        lock (_sendQLock) {
            sendQ.Enqueue(data);
        }
    }

    public void Process() {
        IList<byte[]> res = recv();
        foreach( byte[] data in res ) {
            Debug.Log("recv: " + data.Length);
            switch( data[0] ) {
                case 0:
                    Debug.Log("VarInfo");
                    break;
                case 1:
                    Debug.Log("FileInfo");
                    break;
                case 2:
                    Debug.Log("EndOfFileList");
                    break;
            }
        }
    }

    public IList<byte[]> recv() {
        IList<byte[]> res = new List<byte[]>();
        lock (_recvQLock) {

            while (recvQ.Count > 0) {
                res.Add(recvQ.Dequeue());
            }
        }
        return res;
    }

    public void close() {
        ws.Close();
    }

    private void OnDestroy() {
        close();
    }


    public void SendThread()
    {
        long totalSize;
        byte[] data;

        while (true)
        {
            _sendQSig.WaitOne();
            lock (_sendQLock)
            {
                while (sendQ.Count > 0)
                {
                    totalSize=0;
                    foreach( byte[] dataitem in sendQ ) {
                        // measure total size
                        totalSize += dataitem.Length + 1;
                    }

                    if( totalSize < 128 ) {
                        byte[] idhead = new byte[1];
                        idhead[0] = (byte)totalSize;
                        ws.Send(idhead);
                        while( sendQ.Count > 0 ) {
                            data = sendQ.Dequeue();
                            ws.Send(data);
                        }
                        data = null;  // don't hold onto the data
                    } else {
                        byte[] idhead = new byte[1];
                        idhead[0] = (byte)255;
                        ws.Send(idhead);

                        byte[] sizehead = new byte[4];
                        sizehead[0] = (byte)(totalSize >> 24);
                        sizehead[1] = (byte)(totalSize >> 16);
                        sizehead[2] = (byte)(totalSize >> 8);
                        sizehead[3] = (byte)(totalSize);
                        ws.Send(sizehead);

                        var compressedStream = new MemoryStream();
                        var zipStream = new GZipStream(compressedStream, CompressionMode.Compress);
                        while( sendQ.Count > 0 ) {
                            data = sendQ.Dequeue();
                            idhead[0] = (byte)data.Length;
                            zipStream.Write(idhead, 0, 1);
                            zipStream.Write(data, 0, data.Length);
                        }
                        data = null;  // don't hold onto the data
                        zipStream.Close();
                        byte[] compressedData = compressedStream.ToArray();
                        ws.Send(compressedData);
                    }
                }
            }
        }
    }

    public void RecvThread()
    {
        byte[] readbuffer = new byte[1024];
        byte[] tmpbuf;
        int readlen = 0;

        while (true)
        {
            int recv = ws.Receive(readbuffer, readlen, 1024, SocketFlags.None);

            readlen += recv;
            
            int ptr, smallSize, endptr;

            for( ptr=0; ptr<readlen; ptr++ ) {
                int id = (int)readbuffer[ptr];
                if( id == 255 ) {
                    ulong compressedSize = (ulong)readbuffer[ptr+1] << 24 | (ulong)readbuffer[ptr+2] << 16 | (ulong)readbuffer[ptr+3] << 8 | (ulong)readbuffer[ptr+4];
                    if( ptr+5+(int)compressedSize > readlen ) {
                        break;
                    }
                    ptr += 5;

                    var compressedStream = new MemoryStream(readbuffer, ptr, (int)compressedSize);
                    var zipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
                    var decompressedStream = new MemoryStream();
                    zipStream.CopyTo(decompressedStream);
                    zipStream.Close();
                    decompressedStream.Close();
                    byte[] decompressedData = decompressedStream.ToArray();

                    int deptr;

                    for( deptr=0; deptr<decompressedData.Length; deptr++ ) {
                        smallSize = (int)decompressedData[deptr];
                        deptr++;
                        tmpbuf = new byte[smallSize];
                        Array.Copy(decompressedData, deptr, tmpbuf, 0, smallSize);
                        lock (_recvQLock)
                        {
                            recvQ.Enqueue(tmpbuf);
                        }
                        deptr += smallSize;
                    }
                } else if( ptr+id > readlen ) {
                    break;
                } else {
                    ptr++;
                    endptr = ptr+id;
                    while( ptr < endptr ) {
                        smallSize = (int)readbuffer[ptr];
                        ptr++;
                        tmpbuf = new byte[smallSize];
                        Array.Copy(readbuffer, ptr, tmpbuf, 0, smallSize);
                        ptr += smallSize;
                        lock (_recvQLock)
                        {
                            recvQ.Enqueue(tmpbuf);
                        }
                    }
                }
            }

            if( ptr < recv ) {
                tmpbuf = new byte[(recv-ptr)+1024];
                Array.Copy(readbuffer, ptr, tmpbuf, 0, recv-ptr);
                // free(readbuffer);
                readbuffer = tmpbuf;
                readlen = recv-ptr;
            } else {
                readlen = 0;
            }
        }
    }

}
