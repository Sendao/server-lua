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

    struct FileData {
        public string filename;
        public long filesize;
        public long filetime;
        public string contents;
    };

    private Dictionary<String, FileData> localAssets = new Dictionary<String, FileData>(); // file data for local files
    private bool readingFiles = false;
    private FileData readingFile; // file currently being read from server
    private StreamWriter fileWriter;
    private Queue<FileData> fileQ = new Queue<FileData>(); // files to be read

    public void Start()
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
        Debug.Log("Sent request");
    }

    public void ReadCurrentFileList()
    {
        // read directory
        string[] files = Directory.GetFiles("Assets/StreamingAssets");
        foreach( string file in files ) {
            FileInfo fx = new System.IO.FileInfo(file);

            FileData fi = new FileData();
            fi.filename = file;
            fi.filesize = fx.Length;
            fi.filetime = fx.LastWriteTime.Ticks;
            fi.contents = null;

            localAssets[file] = fi;
        }
    }

    public void Update()
    {
        Process();
    }

    public void SendMessage( char cmd, byte[] data )
    {
        byte[] msg = new byte[3];
        int len;
        msg[0] = (byte)cmd;
        if( data == null ) {
            len = 0;
        } else {
            len = data.Length;
        }
        msg[1] = (byte)(len >> 8);
        msg[2] = (byte)(len & 0xFF);
        Debug.Log("Encode len: " + len + " " + msg[1] + " " + msg[2]);
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
        NetStringReader stream;
        int cmd;

        foreach( byte[] data in res ) {
            stream = new NetStringReader(data);
            Debug.Log("recv: " + data.Length);
            cmd = stream.ReadByte();
            stream.ReadInt();
            switch( cmd ) {
                case 0:
                    Debug.Log("VarInfo");
                    break;
                case 1:
                    GotFileInfo(stream);
                    break;
                case 2:
                    GotEndOfFileList(stream);
                    break;
                case 3:
                    GotFileData(stream);
                    break;
                case 4:
                    GotNextFile(stream);
                    //! Step to next file in queue
                    break;
                case 5:
                    Debug.Log("ObjInfo");
                    break;
                case 6:
                    Debug.Log("TimeSyncTo");
                    break;
            }
        }
    }

    public void GotEndOfFileList(NetStringReader stream)
    {
        Debug.Log("EndOfFileList");
    }

    public void GotNextFile(NetStringReader stream)
    {
        Debug.Log("Total length: " + readingFile.contents.Length);
        Debug.Log("EndOfFile");
        if( fileQ.Count > 0 ) {
            readingFile = fileQ.Dequeue();
            Debug.Log("Next: file " + readingFile.filename);
            // open the streamwriter
            fileWriter = new StreamWriter("Assets\\StreamingAssets\\" + readingFile.filename);
        } else {
            readingFiles = false;
            Debug.Log("End of files");
        }
    }

    public void GotFileData(NetStringReader stream)
    {
        Debug.Log("FileData");
        if( !readingFiles ) {
            Debug.Log("Got file data but no file is being read");
            return;
        }
        string str = System.Text.Encoding.ASCII.GetString(stream.data, 0, stream.data.Length);
        Debug.Log("data length: " + stream.data.Length + ", string length: " + str.Length);
        fileWriter.Write(str);
        readingFile.contents += str;
    }

    public void GotFileInfo(NetStringReader stream)
    {
        string filename;
        long filesize;
        long filetime;
        //NetStringBuilder sb;
        byte[] buf;

        Debug.Log("FileInfo");
        filename = stream.ReadString();
        filesize = stream.ReadLongLong();
        filetime = stream.ReadLongLong();
        Debug.Log("FileInfo " + filename + ": size=" + filesize + ", time=" + filetime);

        if( localAssets.ContainsKey(filename) ) {
            FileData fi = localAssets[filename];
            if( fi.filesize != filesize || fi.filetime != filetime ) {
                // file has changed, request it
                Debug.Log("File " + filename + " has changed from filetime " + fi.filetime + ", requesting");
                buf = new byte[filename.Length];
                System.Text.Encoding.ASCII.GetBytes(filename, 0, filename.Length, buf, 0);
                if( !readingFiles ) {
                    readingFile = fi;
                    fileWriter = new StreamWriter("Assets\\StreamingAssets\\" + readingFile.filename);
                    readingFiles = true;
                } else {
                    fileQ.Enqueue(fi);
                }
                SendMessage( (char)4, buf );
                fi.contents = null;
            } else {
                Debug.Log("Duplicate file " + filename + " found, skipping");
            }
        } else {
            // file is new, request it + save info
            FileData fi = new FileData();
            fi.filename = filename;
            fi.filesize = filesize;
            fi.filetime = filetime;
            fi.contents = null;
            localAssets[filename] = fi;
            Debug.Log("File " + filename + " is new, requesting");
            buf = new byte[filename.Length];
            System.Text.Encoding.ASCII.GetBytes(filename, 0, filename.Length, buf, 0);
            if( !readingFiles ) {
                readingFile = fi;
                fileWriter = new StreamWriter("Assets\\StreamingAssets\\" + readingFile.filename);
                readingFiles = true;
            } else {
                fileQ.Enqueue(fi);
            }
            SendMessage( (char)4, buf );
        }
    }

    public IList<byte[]> recv() {
        IList<byte[]> res = new List<byte[]>();
        if( recvQ.Count == 0 )
            return res;
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

    public void OnDestroy() {
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
                        totalSize += dataitem.Length;
                    }

                    if( totalSize < 128 ) {
                        byte[] idhead = new byte[1];
                        idhead[0] = (byte)totalSize;
                        ws.Send(idhead);
                        while( sendQ.Count > 0 ) {
                            data = sendQ.Dequeue();
                            Debug.Log("Sending: " + data.Length + ": " + data[0] + " " + data[1] + " " + data[2]);
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

    public uint crc32( byte[] data )
    {
        int i;
        uint crc=0;

        for( i=0; i<data.Length; i++ ) {
            crc += (uint)data[i];
        }
        return crc;
    }


    public void RecvThread()
    {
        byte[] readbuffer = new byte[1024];
        byte[] tmpbuf;
        int readlen = 0;
        byte cmdByte;

        while (true)
        {
            int recv = ws.Receive(readbuffer, readlen, 1024, SocketFlags.None);
            Debug.Log("Received " + recv + " bytes");
            if( recv == 0 ) {
                Debug.Log("Connection closed");
                break;
            }
            readlen += recv;
            
            int ptr, smallSize, endptr;

            for( ptr=0; ptr<readlen; ptr++ ) {
                int id = (int)readbuffer[ptr];
                if( id == 255 ) {
                    ulong compressedSize = (ulong)readbuffer[ptr+1] << 24 | (ulong)readbuffer[ptr+2] << 16 | (ulong)readbuffer[ptr+3] << 8 | (ulong)readbuffer[ptr+4];
                    Debug.Log("Compressed size: " + compressedSize + ", Read size: " + readlen);
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

                    //Debug.Log("Decompressed CRC32: " + crc32(decompressedData));

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
                        Debug.Log("Read block of " + smallSize + " bytes: " + tmpbuf[0] + "," + tmpbuf[1] + "," + tmpbuf[2] + ": " + deptr);
                        if( smallSize != 0 )
                            Array.Copy(decompressedData, deptr, tmpbuf, 3, smallSize);
                        lock (_recvQLock)
                        {
                            recvQ.Enqueue(tmpbuf);
                        }
                        deptr += smallSize;
                    }
                } else if( ptr+id > readlen ) {
                    Debug.Log("Not enough data to read: " + ptr + " + " + id + " > " + readlen);
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
                        Debug.Log("Read block of " + smallSize + " bytes: " + tmpbuf[0] + ": " + tmpbuf.Length);
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
