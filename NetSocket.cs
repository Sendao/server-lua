using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using System.IO.Compression;


enum SCommand {
    SetKeyValue,
    RunLuaFile,
    RunLuaCommand,
    GetFileList,
    GetFile,
    IdentifyVar,
    SetVar,
    ClockSync,
    SetObjectPositionRotation,
    Register
};

enum CCommand {
    VarInfo,
    FileInfo,
    EndOfFileList,
    FileData,
    NextFile,
    TimeSync,
    LinkToObject,
    SetObjectPositionRotation,
    RegisterUser
};

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

    struct VarData {
        public string name;
        public int objid;
        public int type; // 0=object, 1=string, 2=int, 3=float, etc
    }

    struct ObjData {
        public int objid;
        public string name;
        public long prev_update;
        public float prev_x, prev_y, prev_z;
        public float prev_r0, prev_r1, prev_r2;
        public long last_update;
        public float x, y, z;
        public float r0, r1, r2;
    };

    private Dictionary<String, FileData> localAssets = new Dictionary<String, FileData>(); // file data for local files
    private bool readingFiles = false;
    private FileData readingFile; // file currently being read from server
    private BinaryWriter fileWriter;
    private Queue<FileData> fileQ = new Queue<FileData>(); // files to be read

    private Dictionary<String, VarData> serverAssets;
    private Dictionary<String, MonoBehaviour> loadingObjects;
    private Dictionary<long, ObjData> serverObjects;

    private long last_game_time = 0;
    private long last_local_time = 0;
    private long last_record_time = -10000;

    public static NetSocket instance;
    public bool authoritative = false;
    public bool connected = false;
    public bool registered = false;

    public void Awake()
    {
        instance = this;
    }
    
    public void OnEnable()
    {
        Debug.Log("Enable NetSocket - Scanning for objects")
        MonoBehaviour[] sceneActive = GameObject.FindObjectsOfType<MonoBehaviour>();

        foreach (MonoBehaviour mono in sceneActive)
        {
            Type monoType = mono.GetType();

            // Retreive the fields from the mono instance
            FieldInfo[] objectFields = monoType.GetFields(BindingFlags.Instance | BindingFlags.Public);

            // search all fields and find the attribute [Position]
            for (int i = 0; i < objectFields.Length; i++)
            {
                CNetSync attribute = Attribute.GetCustomAttribute(objectFields[i], typeof(CNetSync)) as CNetSync;
                if (attribute != null)
                {
                    //! Do the thing
                }
            }
        }
    }
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
        ws.Connect(localEnd); // warning: this appears to block.
        connected = true;
        Debug.Log("Socket connected to " + ws.RemoteEndPoint.ToString());

        _sendThread.Start();
        _recvThread.Start();

        SendMessage( SCommand.Register, null ); // authenticate (for now)
        Debug.Log("Sent request");
    }


    public void RegisterID( MonoBehaviour obj, String oname )
    {
        CNetID cni = (CNetID)obj;
        if( cni == null ) {
            Debug.Log("Object " + oname + " does not have a CNetID component");
            return;
        }

        GameObject go = obj.gameObject;
        int objid = go.GetInstanceID();
        string name = "o" + objid + "_" + oname;

        loadingObjects[name] = obj;

        if( registered ) {
            NetStringBuilder sb = new NetStringBuilder();
            sb.AddString(name);
            sb.AddByte(0); // byte 0 specifies object type
            SendMessage(SCommand.IdentifyVar, sb.ptr);
        }
    }

    public void SendObject( RigidBody rb, long objid )
    {
        NetStringBuilder sb = new NetStringBuilder();
        sb.AddLong(objid);
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
        TimeSpan ts = DateTime.Now - DateTime.UnixEpoch;
        last_local_time = (long)ts.TotalMilliseconds;
        if( last_local_time-last_record_time >= 10000 ) {
            last_record_time = last_local_time;
            NetStringBuilder sb = new NetStringBuilder();
            sb.AddLongLong(last_local_time);
            SendMessage(SCommand.ClockSync, sb.ptr);
        }

        Process();
    }

    public void SendObject( MonoBehavior mb )
    {
        CNetID cni = (CNetID)mb;
        if( cni == null ) {
            Debug.Log("Object " + mb.name + " does not have a CNetID component");
            return;
        }
        NetStringBuilder sb = new NetStringBuilder();

        TimeSpan ts = DateTime.Now - DateTime.UnixEpoch;
        long ts_short = (long)ts.TotalMilliseconds - last_record_time;

        sb.AddLong(cni.id);
        sb.AddInt(ts_short);

        RigidBody rb = mb;
        //GameObject go = mb;

        sb.AddFloat( rb.position.x );
        sb.AddFloat( rb.position.y );
        sb.AddFloat( rb.position.z );

        sb.AddFloat( go.rotation.x );
        sb.AddFloat( go.rotation.y );
        sb.AddFloat( go.rotation.z );
        sb.AddFloat( go.rotation.w );

        SendMEssage(SCommand.SetObjectPositionRotation, sb.ptr);

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
        long uid;

        foreach( byte[] data in res ) {
            stream = new NetStringReader(data);
            Debug.Log("recv: " + data.Length);
            cmd = stream.ReadByte();
            stream.ReadInt(); // size
            switch( cmd ) {
                case CCommand.VarInfo:
                    VarData v = new VarData();
                    v.name = stream.ReadString();
                    v.type = stream.ReadByte();
                    v.objid = stream.ReadLongLong();
                    Debug.Log("VarInfo: " + v.name + " " + v.objid + " " + v.type);
                    serverAssets[v.name] = v;
                    if( !loadingObjects.ContainsKey(v.name) ) {
                        Debug.Log("Object " + v.name + " not found");
                        break;
                    }
                    MonoBehavior mb = loadingObjects[v.name];
                    CNetId cni = mb;
                    cni.id = v.objid;
                    loadingObjects.Remove(v.name);
                    if( authoritative ) { // send back information about the object
                        SendObject( mb );
                    }
                    break;
                case CCommand.FileInfo:
                    GotFileInfo(stream);
                    break;
                case CCommand.EndOfFileList:
                    GotEndOfFileList(stream);
                    break;
                case CCommand.FileData:
                    GotFileData(stream);
                    break;
                case CCommand.NextFile:
                    GotNextFile(stream);
                    //! Step to next file in queue
                    break;
                case CCommand.TimeSync:
                    Debug.Log("TimeSync"); // gonna be every 10 seconds or so I think
                    last_game_time = stream.ReadLongLong();
                    break;
                case CCommand.LinkToObject:
                    Debug.Log("LinkToObject");
                    break;
                case CCommand.SetObjectPositionRotation:
                    Debug.Log("SetObjectPositionRotation");
                    break;
                case CCommand.RegisterUser: // Register User
                    if( stream.ReadByte() == 0 ) {
                        authoritative = false;
                    } else {
                        authoritative = true;
                    }
                    SendMessage( SCommand.GetFileList, null ); // request file list
                    registered = true;
                    // request variable idents
                    foreach( string key in loadingObjects.Keys ) {
                        NetStringBuilder sb = new NetStringBuilder();
                        sb.AddString(key);
                        sb.AddByte(0);
                        SendMessage( SCommand.IdentifyVar, sb.ptr );
                    }
                    break;
            }
        }
    }

    public void GetObjects()
    {

    }

    public void GotEndOfFileList(NetStringReader stream)
    {
        Debug.Log("EndOfFileList");

        if( !readingFiles ) {
            GetObjects();
        }
    }

    public void GotNextFile(NetStringReader stream)
    {
        Debug.Log("EndOfFile");
        if( fileQ.Count > 0 ) {
            readingFile = fileQ.Dequeue();
            Debug.Log("Next: file " + readingFile.filename);
            // open the streamwriter
            if( File.Exists("Assets\\StreamingAssets\\" + readingFile.filename) )
                File.Delete("Assets\\StreamingAssets\\" + readingFile.filename);
            fileWriter = new BinaryWriter(File.Create("Assets\\StreamingAssets\\" + readingFile.filename));
        } else {
            readingFiles = false;
            Debug.Log("End of files");
            GetObjects();
        }
    }

    public void GotFileData(NetStringReader stream)
    {
        Debug.Log("FileData");
        if( !readingFiles ) {
            Debug.Log("Got file data but no file is being read");
            return;
        }
        //string str = System.Text.Encoding.ASCII.GetString(stream.data, 0, stream.data.Length);
        //Debug.Log("data length: " + stream.data.Length + ", string length: " + str.Length);
        fileWriter.Write(stream.data, 3, stream.data.Length-3);
        //readingFile.contents += str;
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
                    File.Delete("Assets\\StreamingAssets\\" + readingFile.filename);
                    fileWriter = new BinaryWriter(File.Create("Assets\\StreamingAssets\\" + readingFile.filename));
                    readingFiles = true;
                } else {
                    fileQ.Enqueue(fi);
                }
                SendMessage( SCommand.GetFile, buf );
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
                fileWriter = new BinaryWriter(File.Create("Assets\\StreamingAssets\\" + readingFile.filename));
                readingFiles = true;
            } else {
                fileQ.Enqueue(fi);
            }
            SendMessage( SCommand.GetFile, buf );
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
                            int i;
                            string str = "";
                            for( i=0; i<data.Length; i++ ) {
                                str += (int)data[i] + " ";
                            }
                            Debug.Log("Sending: " + data.Length + ": " + str);
                            ws.Send(data);
                        }
                        data = null;  // don't hold onto the data
                    } else {
                        Debug.Log("Compressing " + totalSize + " bytes");
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
                            zipStream.Write(data, 0, data.Length);
                        }
                        data = null;  // don't hold onto the data
                        zipStream.Close();
                        byte[] compressedData = compressedStream.ToArray();
                        Debug.Log("Compressed to " + compressedData.Length + " bytes");
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
            crc = (crc+data[i]) & 0xFFFFFFFF;
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
                        Debug.Log("Compressed size: " + compressedSize + " not ready yet.");
                        break;
                    }
                    Debug.Log("Compressed size: " + compressedSize + ", buffer size: " + readlen + ", readbuffers: " + readbuffer[ptr+1] + ", " + readbuffer[ptr+2] + ", " + readbuffer[ptr+3] + ", " + readbuffer[ptr+4] + ", " + readbuffer[ptr+5] + ", " + readbuffer[ptr+6]);
                    ptr += 5;

                    var compressedStream = new MemoryStream(readbuffer, ptr, compressedSize);
                    var zipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
                    var decompressedStream = new MemoryStream();
                    zipStream.CopyTo(decompressedStream);
                    zipStream.Close();
                    decompressedStream.Close();
                    byte[] decompressedData = decompressedStream.ToArray();

                    Debug.Log("Decompressed, Size: " + decompressedData.Length + ", CRC32: " + crc32(decompressedData));

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

            if( ptr < readlen ) {
                tmpbuf = new byte[(readlen-ptr)+1024];
                Array.Copy(readbuffer, ptr, tmpbuf, 0, readlen-ptr);
                // free(readbuffer);
                readbuffer = tmpbuf;
                readlen = readlen-ptr;
            } else {
                readlen = 0;
            }
        }
    }

}
