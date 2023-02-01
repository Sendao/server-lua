using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

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

        ws.Connect(localEnd);
        Debug.Log("Socket connected to " + ws.RemoteEndPoint.ToString());

        _sendThread.Start();
        _recvThread.Start();
    }

    public void send(byte[] data) {
        lock (_sendQLock) {
            sendQ.Enqueue(data);
        }
        _sendQSig.Set();
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
        byte[] data;
        var compressedStream = new MemoryStream();
        var zipStream = new GZipStream(compressedStream, CompressionMode.Compress);
        long totalSize;

        while (true)
        {
            _sendQSig.WaitOne();
            lock (_sendQLock)
            {
                while (sendQ.Count > 0)
                {
                    totalSize=0;
                    foreach( data in sendQ ) {
                        // measure total size
                        totalSize += data.Length + 1;
                    }

                    if( totalSize < 128 ) {
                        byte[] idhead = new byte[1];
                        idhead[0] = (byte)totalSize;
                        ws.Send(idhead);
                        while( sendQ.Count > 0 ) {
                            data = sendQ.Dequeue();
                            idhead[0] = (byte)data.Length;
                            ws.Send(idhead);
                            ws.Send(data);
                        }
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

                        while( sendQ.Count > 0 ) {
                            data = sendQ.Dequeue();
                            idhead[0] = (byte)data.Length;
                            zipStream.Write(idhead, 0, 1);
                            zipStream.Write(data, 0, data.Length);
                        }
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
        long readlen = 0;

        while (true)
        {
            if( readlen > 0 ) {
                tmpbuf = new byte[readlen+1024];
                Array.Copy(readbuffer, tmpbuf, readlen);
                delete readbuffer;
                readbuffer = tmpbuf;
            }
            int recv = ws.Receive(readbuffer, readlen, 1024);

            readlen += recv;
            
            int ptr, smallSize, endptr;

            for( ptr=0; ptr<readlen; ptr++ ) {
                int id = (int)data[ptr];
                if( id == 255 ) {
                    compressedSize = (long)data[ptr+1] << 24 | (long)data[ptr+2] << 16 | (long)data[ptr+3] << 8 | (long)data[ptr+4];
                    if( ptr+4+compressedSize > readlen ) {
                        break;
                    }
                    ptr += 4;
                    var compressedStream = new MemoryStream(data, ptr, compressedSize);
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
                } else if( ptr+id < readlen ) {
                    break;
                } else {
                    ptr++;
                    endptr = ptr+id;
                    do {
                        smallSize = (int)data[ptr];
                        ptr++;
                        tmpbuf = new byte[smallSize];
                        Array.Copy(data, ptr, tmpbuf, 0, smallSize);
                        lock (_recvQLock)
                        {
                            recvQ.Enqueue(tmpbuf);
                        }
                        ptr += smallSize;
                    } while( ptr < endptr );
                }
            }

            if( ptr < recv ) {
                tmpbuf = new byte[(recv-ptr)+1024];
                Array.Copy(data, ptr, tmpbuf, 0, recv-ptr);
                delete readbuffer;
                readbuffer = tmpbuf;
                readlen = recv-ptr;
            } else {
                readlen = 0;
            }
        }
    }

}
