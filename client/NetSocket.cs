using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;

public enum SCommand {
    SetKeyValue,
    RunLuaFile,
    RunLuaCommand,
    GetFileList,
    GetFile,
    IdentifyVar,
    SetVar,
    ClockSync,
    SetObjectPositionRotation,
    Register,
    DynPacket,
    Packet
};

public enum CCommand {
    VarInfo,
    FileInfo,
    EndOfFileList,
    FileData,
    NextFile,
    TimeSync,
    LinkToObject,
    SetObjectPositionRotation,
    RegisterUser,
    ChangeUserRegistration,
    DynPacket,
    Packet,
    NewUser,
    UserQuit
};

public class NetSocket : MonoBehaviour
{
    public static NetSocket instance;

    public GameObject playerPrefab;
    public GameObject Player = null;

    public bool authoritative = false;
    public bool connected = false;
    public bool registered = false;

    public int local_uid;

    public delegate void PacketCallback( long ts, NetStringReader data );

    public readonly object _sendQLock = new object();
    public readonly Queue<byte[]> sendQ = new Queue<byte[]>();
    public readonly AutoResetEvent _sendQSig = new AutoResetEvent(false);

    public readonly object _recvQLock = new object();
    public readonly Queue<byte[]> recvQ = new Queue<byte[]>();

    private NetThreads threads;
    private NetFiles files;
    public Socket ws;

    public readonly object _localIpEndLock = new object();
    public IPEndPoint localEnd;

    struct VarData {
        public string name;
        public long objid;
        public int type; // 0=object, 1=string, 2=int, 3=float, etc
    }

    struct ObjData {
        public long objid;
        public string name;
        public long prev_update;
        public float prev_x, prev_y, prev_z;
        public float prev_r0, prev_r1, prev_r2, prev_r3;
        public long last_update;
        public float x, y, z;
        public float r0, r1, r2, r3;
    };

    private Dictionary<String, VarData> serverAssets = new Dictionary<String, VarData>();
    private Dictionary<String, MonoBehaviour> loadingObjects = new Dictionary<String, MonoBehaviour>();
    private Dictionary<long, ObjData> serverObjects = new Dictionary<long, ObjData>();
    private Dictionary<long, MonoBehaviour> clientObjects = new Dictionary<long, MonoBehaviour>();
    private Dictionary<long, Rigidbody> clientBodies = new Dictionary<long, Rigidbody>();
    private Dictionary<int, Dictionary<int, PacketCallback>> commandHandlers = new Dictionary<int, Dictionary<int, PacketCallback>>();
    private Dictionary<int, int> packetSizes = new Dictionary<int, int>();
    private Dictionary<long, ObjData> serverUsers = new Dictionary<long, ObjData>();

    private long last_game_time = 0;
    private long last_local_time = 0;
    private long last_record_time = -10000;

    public void Awake()
    {
        instance = this;
        Debug.Log("Wake up");
    }
    
    public void Start()
    {
        //IPHostEntry ipHost = Dns.GetHostEntry("127.0.0.1");
        IPAddress ipAddr = IPAddress.Parse("127.0.0.1"); //ipHost.AddressList[0];

        localEnd = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2038);

        ws = new Socket(ipAddr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        threads = new NetThreads(this);
        files = new NetFiles(this);

        Debug.Log("Connecting to server...");
        ws.Connect(localEnd); // warning: this appears to block.
        Debug.Log("Socket connected to " + ws.RemoteEndPoint.ToString());

        threads._sendThread.Start();
        threads._recvThread.Start();
        connected = true;

        if( Player != null ) {
            NetStringBuilder sb = new NetStringBuilder();
            sb.AddFloat( Player.transform.position.x );
            sb.AddFloat( Player.transform.position.y );
            sb.AddFloat( Player.transform.position.z );
            sb.AddFloat( Player.transform.rotation.x );
            sb.AddFloat( Player.transform.rotation.y );
            sb.AddFloat( Player.transform.rotation.z );
            sb.AddFloat( Player.transform.rotation.w );
            SendMessage( SCommand.Register, sb ); // authenticate (for now)
        }
    }

    public void RegisterUser( CNetPlayer1 player ) {
        Debug.Log("-local player found-");
        Player = player.gameObject;
        if( connected ) {
            NetStringBuilder sb = new NetStringBuilder();
            sb.AddFloat( Player.transform.position.x );
            sb.AddFloat( Player.transform.position.y );
            sb.AddFloat( Player.transform.position.z );
            sb.AddFloat( Player.transform.rotation.x );
            sb.AddFloat( Player.transform.rotation.y );
            sb.AddFloat( Player.transform.rotation.z );
            sb.AddFloat( Player.transform.rotation.w );
            SendMessage( SCommand.Register, sb ); // authenticate (for now)
        }
    }


    // Process() gets called by Update()
    public void Process() {
        IList<byte[]> res = recv();
        NetStringReader stream;
        Rigidbody rb;
        int cmd;

        foreach( byte[] data in res ) {
            stream = new NetStringReader(data);
            cmd = stream.ReadByte();
            stream.ReadInt(); // size
            switch( (CCommand)cmd ) {
                case CCommand.VarInfo:
                    GotVarInfo(stream);
                    break;
                case CCommand.FileInfo:
                    files.GotFileInfo(stream);
                    break;
                case CCommand.EndOfFileList:
                    files.GotEndOfFileList(stream);
                    break;
                case CCommand.FileData:
                    files.GotFileData(stream);
                    break;
                case CCommand.NextFile:
                    files.GotNextFile(stream);
                    //! Step to next file in queue
                    break;
                case CCommand.TimeSync:
                    last_game_time = stream.ReadLongLong();
                    break;
                case CCommand.LinkToObject:
                    Debug.Log("LinkToObject");
                    break;
                case CCommand.SetObjectPositionRotation:
                    SetObjectPositionRotation(stream);
                    break;
                case CCommand.RegisterUser: // Register User
                    if( stream.ReadByte() == 0 ) {
                        authoritative = false;
                    } else {
                        authoritative = true;
                    }

                    //! Save uid
                    local_uid = stream.ReadInt();
                    CNetPlayer1 plr = Player.GetComponent<CNetPlayer1>();
                    plr.id = local_uid;

                    SendMessage( SCommand.GetFileList, null ); // request file list
                    registered = true;
                    // request variable idents
                    Debug.Log("Logged in as " + local_uid);
                    foreach( string key in loadingObjects.Keys ) {
                        Debug.Log("Registering object " + key);
                        NetStringBuilder sb = new NetStringBuilder();
                        sb.AddString(key);
                        sb.AddByte(0);
                        SendMessage( SCommand.IdentifyVar, sb );
                    }
                    break;
                case CCommand.ChangeUserRegistration:
                    if( stream.ReadByte() == 0 ) {
                        authoritative = false;
                    } else {
                        authoritative = true;
                    }
                    if( !authoritative ) {
                        foreach( long key in clientBodies.Keys ) {
                            rb = clientBodies[key];
                            rb.isKinematic = !authoritative;
                        }
                    }
                    break;
                case CCommand.NewUser:
                    NewUser(stream);
                    break;
                case CCommand.UserQuit:
                    UserQuit(stream);
                    break;
                case CCommand.Packet:
                    RecvPacket(stream);
                    break;
                case CCommand.DynPacket:
                    Debug.Log("Got dyn packet");
                    RecvDynPacket(stream);
                    break;
            }
        }
    }

    public void close() {
        ws.Close();
    }

    public void OnDestroy() {
        close();
    }


    

    public void RegisterObj( MonoBehaviour obj )
    {

    }

    public void GetObjects()
    {

    }

    public void SendObject( MonoBehaviour mb )
    {
        CNetId cni = (CNetId)mb.GetComponent<CNetId>();
        if( cni == null ) {
            Debug.Log("Object " + mb.name + " does not have a CNetId component");
            return;
        }
        if( cni.id == -1 ) {
            Debug.Log("Object " + mb.name + " does not have an id");
            return; // can't send if it's not id'd yet
        }

        NetStringBuilder sb = new NetStringBuilder();

        TimeSpan ts = DateTime.Now - DateTime.UnixEpoch;
        int ts_short = (int)(ts.TotalMilliseconds - last_record_time);

        sb.AddLong(cni.id);
        sb.AddInt(ts_short);

        Rigidbody rb = mb.GetComponent<Rigidbody>();
        if( rb == null ) {
            Debug.Log("Object " + mb.name + " does not have a Rigidbody component");
            return;
        }

        sb.AddFloat( rb.position.x );
        sb.AddFloat( rb.position.y );
        sb.AddFloat( rb.position.z );

        sb.AddFloat( rb.rotation.x );
        sb.AddFloat( rb.rotation.y );
        sb.AddFloat( rb.rotation.z );
        sb.AddFloat( rb.rotation.w );

        SendMessage(SCommand.SetObjectPositionRotation, sb);
        //Debug.Log("Sent object " + cni.id);
    }

    public void SetObjectPositionRotation(NetStringReader stream)
    {
        long objid;
        int ts_short;
        long ts;
        float x,y,z;
        float r0,r1,r2,r3;

        objid = stream.ReadLong();
        ts_short = stream.ReadInt();
        ts = ts_short + last_game_time;

        x = stream.ReadFloat();
        y = stream.ReadFloat();
        z = stream.ReadFloat();
        r0 = stream.ReadFloat();
        r1 = stream.ReadFloat();
        r2 = stream.ReadFloat();
        r3 = stream.ReadFloat();

        //Debug.Log("SetObjectPositionRotation: " + objid + ": " + x + " " + y + " " + z + " " + r0 + " " + r1 + " " + r2 + " " + r3);
        Rigidbody rb = clientBodies[objid];
        rb.MovePosition(new Vector3(x,y,z));
        rb.MoveRotation(new Quaternion(r0,r1,r2,r3));
    }



    public void RegisterId( MonoBehaviour obj, String oname )
    {
        CNetId cni = (CNetId)obj;
        if( cni == null ) {
            Debug.Log("Object " + oname + " does not have a CNetId component");
            return;
        }

        GameObject go = obj.gameObject;
        //int objid = go.GetInstanceID();
        string name = "o_" + oname;

        loadingObjects[name] = obj;
        if( registered ) {
            Debug.Log("Registering object " + name);
            NetStringBuilder sb = new NetStringBuilder();
            sb.AddString(name);
            sb.AddByte(0); // byte 0 specifies object type
            SendMessage(SCommand.IdentifyVar, sb);
        }
    }

    public void RegisterPacket( int cmd, int tgt, PacketCallback callback, int packetSize )
    {
        if( !commandHandlers.ContainsKey(cmd) ) {
            commandHandlers[cmd] = new Dictionary<int, PacketCallback>();

            if( packetSizes.ContainsKey(cmd) && packetSizes[cmd] != packetSize )
                Debug.Log("Warning: packet size for command " + cmd + " already set to " + packetSizes[cmd] + " (new size: " + packetSize + ")");
            packetSizes[cmd] = packetSize;
        }
        commandHandlers[cmd][tgt] = callback;
    }

    public void SendDynPacket( int cmd, int tgt, byte[] data )
    {
        NetStringBuilder sb = new NetStringBuilder();

        sb.AddInt(cmd);
        sb.AddInt(tgt);

        TimeSpan ts = DateTime.Now - DateTime.UnixEpoch;
        int ts_short = (int)(ts.TotalMilliseconds - last_record_time);
        sb.AddInt(ts_short);

        if( data != null ) sb.AddShortBytes(data);
        SendMessage(SCommand.DynPacket, sb);
    }
    public void RecvDynPacket( NetStringReader stream )
    {
        int cmd, tgt, ts_short;
        long ts;
        byte[] detail;

        cmd = stream.ReadInt();
        tgt = stream.ReadInt();

        ts_short = stream.ReadInt();
        ts = ts_short + last_game_time;

        detail = stream.ReadShortBytes();
        NetStringReader param = new NetStringReader(detail);

        if( commandHandlers.ContainsKey(cmd) ) {
            if( commandHandlers[cmd].ContainsKey(tgt) ) {
                commandHandlers[cmd][tgt](ts, param);
            } else {
                Debug.Log("Unknown object: " + tgt);
            }
        } else {
            Debug.Log("Unknown command: " + cmd);
        }
    }
    
    public void SendPacket( int cmd, int tgt, byte[] data )
    {
        NetStringBuilder sb = new NetStringBuilder();

        sb.AddInt(cmd);
        sb.AddInt(tgt);

        TimeSpan ts = DateTime.Now - DateTime.UnixEpoch;
        int ts_short = (int)(ts.TotalMilliseconds - last_record_time);
        sb.AddInt(ts_short);

        if( data != null ) sb.AddBytes(data);
        SendMessage(SCommand.Packet, sb);
    }
    public void RecvPacket( NetStringReader stream )
    {
        int cmd, tgt, ts_short;
        long ts;
        byte[] detail;

        cmd = stream.ReadInt();
        tgt = stream.ReadInt();

        ts_short = stream.ReadInt();
        ts = ts_short + last_game_time;

        NetStringReader param;
        if( packetSizes.ContainsKey(cmd) ) {
            detail = stream.ReadFixedBytes(packetSizes[cmd]);
            param = new NetStringReader(detail);
        } else {
            param = null;
        }

        if( commandHandlers.ContainsKey(cmd) ) {
            if( commandHandlers[cmd].ContainsKey(tgt) ) {
                commandHandlers[cmd][tgt](ts, param);
            } else {
                Debug.Log("Unknown object: " + tgt);
            }
        } else {
            Debug.Log("Unknown command: " + cmd);
        }
    }

    public void NewUser(NetStringReader stream)
    {
        int uid = stream.ReadInt();
        Debug.Log("NewUser " + uid);
        Vector3 startPos = new Vector3();
        startPos.x = stream.ReadFloat();
        startPos.y = stream.ReadFloat();
        startPos.z = stream.ReadFloat();
        Quaternion startRot = new Quaternion();
        startRot.x = stream.ReadFloat();
        startRot.y = stream.ReadFloat();
        startRot.z = stream.ReadFloat();
        startRot.w = stream.ReadFloat();

        GameObject go = Instantiate(playerPrefab, startPos, startRot);
        go.name = "Player";

        CNetPlayer1 cplayer = go.GetComponent<CNetPlayer1>();
        cplayer.isLocalPlayer = false;
        cplayer.id = uid;
        cplayer.Register();
    }

    public void UserQuit(NetStringReader stream)
    {
        //! Do this.
    }

    public void GotVarInfo(NetStringReader stream)
    {
        VarData v = new VarData();
        v.name = stream.ReadString();
        v.type = stream.ReadByte();
        v.objid = stream.ReadLong();

        Debug.Log("VarInfo: " + v.name + " " + v.objid + " " + v.type);
        serverAssets[v.name] = v;
        if( !loadingObjects.ContainsKey(v.name) ) {
            Debug.Log("Object " + v.name + " not found");
            return;
        }

        MonoBehaviour mb = loadingObjects[v.name];
        CNetId cni = mb.GetComponent<CNetId>();
        if( cni != null ) {
            cni.id = (int)v.objid;
        } else {
            Debug.Log("Object " + v.name + " does not have a CNetId component");
        }
        clientObjects[v.objid] = mb;
        Rigidbody rb = mb.GetComponent<Rigidbody>();
        if( rb != null ) {
            clientBodies[v.objid] = rb;
        }
        loadingObjects.Remove(v.name);
        if( rb != null && authoritative ) { // send back information about the object
            Debug.Log("Sending object " + v.name + " to server");
            SendObject( mb );
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
            SendMessage(SCommand.ClockSync, sb);
        }

        Process();
    }

    public void SendMessage( SCommand cmd, NetStringBuilder sb )
    {
        byte[] data = null;
        if( sb != null ) {
            sb.Reduce();
            data = sb.ptr;
        }
        byte[] msg = new byte[3];
        int len;
        msg[0] = (byte)cmd;
        if( sb == null || data == null ) {
            len = 0;
        } else {
            len = data.Length;
        }
        msg[1] = (byte)(len >> 8);
        msg[2] = (byte)(len & 0xFF);
        //Debug.Log("Encode len: " + len + " " + msg[1] + " " + msg[2]);
        send(msg);

        if( data != null )
            send(data);
        _sendQSig.Set();
    }

    public void SendMessage2( SCommand cmd, byte[] data )
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
        //Debug.Log("Encode len: " + len + " " + msg[1] + " " + msg[2]);
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

    public uint crc32( byte[] data )
    {
        int i;
        uint crc=0;

        for( i=0; i<data.Length; i++ ) {
            crc = (crc+data[i]) & 0xFFFFFFFF;
        }
        return crc;
    }
}
