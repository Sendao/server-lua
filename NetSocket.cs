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

                    // if total size is greater than 128, compress and send 128 + data
                    // if not, send length + data
                }
            }
        }
    }

    public void RecvThread()
    {
        while (true)
        {
            byte[] data = new byte[1024];
            int recv = ws.Receive(data);
            byte[] data2 = new byte[recv];
            Array.Copy(data, data2, recv);

            //! Todo: decompose the data into commands!

            lock (_recvQLock)
            {
                recvQ.Enqueue(data2);
            }
        }
    }

}
