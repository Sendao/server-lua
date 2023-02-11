using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using System.IO.Compression;

public class NetThreads
{
	NetSocket parent;
    public Thread _recvThread;
    public Thread _sendThread;

	public NetThreads(NetSocket ctrl)
	{
		parent = ctrl;
        _sendThread = new Thread(new ThreadStart(SendThread));
        _sendThread.IsBackground = true;
        _recvThread = new Thread(new ThreadStart(RecvThread));
        _recvThread.IsBackground = true;
        Debug.Log("NetThreads ready");
	}

    private void SendThread()
    {
        long totalSize, sentSize;
        byte[] data;

        while (true)
        {
            parent._sendQSig.WaitOne();
            while (parent.sendQ.Count > 0)
            {
                totalSize=0;
                lock (parent._sendQLock)
                {
                    foreach( byte[] dataitem in parent.sendQ ) {
                        totalSize += dataitem.Length;
                    }
                }

                //Debug.Log("Sending " + totalSize + " bytes");

                sentSize=0;

                if( totalSize < 128 ) {
                    byte[] idhead = new byte[1];
                    idhead[0] = (byte)totalSize;
                    parent.ws.Send(idhead);
                    sentSize = 1;
                    while( parent.sendQ.Count > 0 ) {
                        lock (parent._sendQLock) {
                            data = parent.sendQ.Dequeue();
                        }
                        parent.ws.Send(data);
                        sentSize += data.Length;
                        if( sentSize >= totalSize ) break;
                    }
                    data = null;  // don't hold onto the data
                } else {
                    Debug.Log("Compressing " + totalSize + " bytes");
                    byte[] idhead = new byte[1];
                    idhead[0] = (byte)255;
                    parent.ws.Send(idhead);

                    
                    var compressedStream = new MemoryStream();
                    var zipStream = new DeflateStream(compressedStream, System.IO.Compression.CompressionLevel.Optimal);
                    compressedStream.WriteByte(0x78);
                    compressedStream.WriteByte(0xDA);
                    uint a1 = 1, a2 = 0;
                    while( parent.sendQ.Count > 0 ) {
                        lock (parent._sendQLock) {
                            data = parent.sendQ.Dequeue();
                        }
                        zipStream.Write(data, 0, data.Length);
                        sentSize += data.Length;
                        foreach (byte b in data) {
                            a1 = (a1 + b) % 65521;
                            a2 = (a2 + a1) % 65521;
                        }
                        if( sentSize >= totalSize ) break;
                    }

                    data = null;  // don't hold onto the data
                    zipStream.Close(); // finish sending all data

                    byte[] compressedData = compressedStream.ToArray();
                    //new byte[compressedStream.Length];
                    //compressedStream.Read(compressedData, 0, (int)compressedData.Length);

                    Array.Resize(ref compressedData, compressedData.Length + 4);
                    compressedData[compressedData.Length - 4] = (byte)(a2 >> 8);
                    compressedData[compressedData.Length - 3] = (byte)(a2 & 0xFF);
                    compressedData[compressedData.Length - 2] = (byte)(a1 >> 8);
                    compressedData[compressedData.Length - 1] = (byte)(a1 & 0xFF);

                    long compSize = compressedData.Length;
                    //Debug.Log("Compressed to " + compSize + " bytes");

                    byte[] sizehead = new byte[4];
                    sizehead[0] = (byte)(compSize >> 24);
                    sizehead[1] = (byte)(compSize >> 16);
                    sizehead[2] = (byte)(compSize >> 8);
                    sizehead[3] = (byte)(compSize);
                    parent.ws.Send(sizehead);
                    
                    parent.ws.Send(compressedData);
                    sentSize = 5 + compSize;
                }

                lock( parent._outbpsLock ) {
                    parent.out_bps_measure += (int) sentSize;
                }
            }
        }
    }

    private void RecvThread()
    {
        byte[] readbuffer = new byte[1024];
        byte[] tmpbuf;
        int readlen = 0;
        byte cmdByte;
        long recv_bytes;

        while (true)
        {
            int recv = parent.ws.Receive(readbuffer, readlen, 1024, SocketFlags.None);
            recv_bytes=0;
            //Debug.Log("Received " + recv + " bytes");
            if( recv <= 0 ) {
                Debug.Log("Connection closed");
                break;
            }
            readlen += recv;
            
            int ptr, smallSize, endptr;

            ptr=0;
            while( ptr < readlen ) {
                int id = (int)readbuffer[ptr];
                if( id == 255 ) {
                    if( ptr+5 > readlen ) break;
                    int compressedSize =
                        readbuffer[ptr+1] << 24 |
                        readbuffer[ptr+2] << 16 |
                        readbuffer[ptr+3] << 8 |
                        readbuffer[ptr+4];
                    if( ptr+5+compressedSize > readlen ) {
                        //Debug.Log("Compressed size: " + compressedSize + " not ready yet.");
                        break;
                    }
                    //Debug.Log("Compressed size: " + compressedSize + ", buffer size: " + readlen + ", readbuffers: " + readbuffer[ptr+1] + ", " + readbuffer[ptr+2] + ", " + readbuffer[ptr+3] + ", " + readbuffer[ptr+4] + ", " + readbuffer[ptr+5] + ", " + readbuffer[ptr+6]);
                    ptr += 5;

                    var compressedStream = new MemoryStream(readbuffer, ptr, compressedSize);
                    var zipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
                    var decompressedStream = new MemoryStream();
                    zipStream.CopyTo(decompressedStream);
                    zipStream.Close();
                    decompressedStream.Close();
                    byte[] decompressedData = decompressedStream.ToArray();

                    //Debug.Log("Decompressed, Size: " + decompressedData.Length + ", CRC32: " + crc32(decompressedData));

                    ptr += (int)compressedSize;

                    //Debug.Log("Decompressed size: " + decompressedData.Length);                    
		            //Debug.Log("Byte check: " + (int)decompressedData[200] + "," + (int)decompressedData[201] + "," + (int)decompressedData[202] + "," + (int)decompressedData[203]);

                    int deptr;

                    for( deptr=0; deptr<decompressedData.Length; ) {
                        cmdByte = decompressedData[deptr];
                        smallSize = (int)( decompressedData[deptr+1] << 8 ) | (int)( decompressedData[deptr+2] );
                        deptr += 3;
                        tmpbuf = new byte[smallSize+3];
                        tmpbuf[0] = cmdByte;
                        tmpbuf[1] = decompressedData[deptr-2];
                        tmpbuf[2] = decompressedData[deptr-1];
                        //Debug.Log("Read block of " + smallSize + " bytes: " + tmpbuf[0] + "," + tmpbuf[1] + "," + tmpbuf[2] + ": " + deptr);
                        if( smallSize != 0 )
                            Array.Copy(decompressedData, deptr, tmpbuf, 3, smallSize);
                        recv_bytes += tmpbuf.Length;
                        deptr += smallSize;
                        lock (parent._recvQLock)
                        {
                            parent.recvQ.Enqueue(tmpbuf);
                        }
                    }
                } else if( ptr+id > readlen ) {
                    //Debug.Log("Not enough data to read: " + ptr + " + " + id + " > " + readlen);
                    break;
                } else {
                    ptr++;
                    endptr = ptr+id;
                    while( ptr < endptr ) {
                        cmdByte = readbuffer[ptr];
                        smallSize = (int)readbuffer[ptr+1]<<8 | (int)readbuffer[ptr+2];
                        ptr += 3;
                        tmpbuf = new byte[smallSize+3];
                        tmpbuf[0] = cmdByte;
                        tmpbuf[1] = readbuffer[ptr-2];
                        tmpbuf[2] = readbuffer[ptr-1];
                        if( smallSize != 0 )
                            Array.Copy(readbuffer, ptr, tmpbuf, 3, smallSize);
                        //Debug.Log("Read block of " + smallSize + " bytes: " + tmpbuf[0] + ": " + tmpbuf.Length);
                        ptr += smallSize;
                        recv_bytes += tmpbuf.Length;
                        lock (parent._recvQLock)
                        {
                            parent.recvQ.Enqueue(tmpbuf);
                        }
                    }
                }
            }

            if( ptr < readlen ) {
                tmpbuf = new byte[(readlen-ptr)+1024];
                Array.Copy(readbuffer, ptr, tmpbuf, 0, readlen-ptr);
                readbuffer = tmpbuf;
                readlen = readlen-ptr;
            } else {
                readlen = 0;
            }

            lock( parent._inbpsLock ) {
                parent.in_bps_measure += (int)recv_bytes;
            }
        }
    }
}
