using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using CNet;
using TwoNibble.Impunes;
using TwoNibble.Impunes.Controllers;
using TwoNibble.Impunes.Character;

namespace CNet {
    public interface ICNetUpdate
    {
        void Register( );
        void NetUpdate( );
    };

    public class NetSocket : MonoBehaviour
    {
        public static NetSocket Instance=null;

        [Tooltip("Rate of updates per second. This is for testing purposes.")]
        public float updateRate = 10;

        public GameObject Player = null;

        public bool authoritative = false;
        public bool connected = false;
        public bool registered = false;

        public uint local_uid;

        public delegate void PacketCallback( ulong ts, NetStringReader data );

        public readonly object _sendQLock = new object();
        public readonly object _sendBlock = new object();
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
            public uint objid;
            public int type; // 0=object, 1=string, 2=int, 3=float, etc
        }

        struct ObjData {
            public uint objid;
            public string name;
            public long prev_update;
            public float prev_x, prev_y, prev_z;
            public float prev_r0, prev_r1, prev_r2, prev_r3;
            public long last_update;
            public float x, y, z;
            public float r0, r1, r2, r3;
        };

        struct PacketData {
            public ulong ts;
            public NetStringReader stream;
        };

        private Dictionary<String, VarData> serverAssets = new Dictionary<String, VarData>();
        private Dictionary<String, MonoBehaviour> loadingObjects = new Dictionary<String, MonoBehaviour>();
        private List<ICNetUpdate> netObjects = new List<ICNetUpdate>();
        private Dictionary<uint, ObjData> serverObjects = new Dictionary<uint, ObjData>();
        private Dictionary<uint, MonoBehaviour> clientObjects = new Dictionary<uint, MonoBehaviour>();
        private Dictionary<uint, Collider> clientColliders = new Dictionary<uint, Collider>();
        private Dictionary<uint, Rigidbody> clientBodies = new Dictionary<uint, Rigidbody>();
        private Dictionary<uint, Dictionary<uint, PacketCallback>> commandHandlers = new Dictionary<uint, Dictionary<uint, PacketCallback>>();
        private Dictionary<uint, int> packetSizes = new Dictionary<uint, int>();
        private Dictionary<uint, ObjData> serverUsers = new Dictionary<uint, ObjData>();
        private Dictionary<uint, GameObject> serverUserObjects = new Dictionary<uint, GameObject>();
        private Dictionary<uint, Dictionary<uint, List<PacketData>>> waitingHandlers = new Dictionary<uint, Dictionary<uint, List<PacketData>>>();

        private ulong last_game_time = 0;
        private ulong last_local_time = 0;
        private ulong last_record_time = 0;
        public long net_clock_offset = 0;
        public ulong last_netupdate = 0;

        public bool record_bps = false;
        private bool foundMainUser = false;

        public List<int> out_bytes = new List<int>();
        public List<int> in_bytes = new List<int>();
        public ulong last_out_time = 0;
        public ulong last_in_time = 0;
        public int in_bps_measure = 0;
        public int out_bps_measure = 0;
        public readonly object _inbpsLock = new object();
        public readonly object _outbpsLock = new object();
        public uint maxTargets = 0;

        public void Awake()
        {
            Instance = this;
            UMACharacterBuilder.AfterBuildCharacter += BuildPlayer;
        }
        
        public void Start()
        {
            IPHostEntry ipHost = Dns.GetHostEntry("spiritshare.org");
            IPAddress ipAddr=null;
            bool found=false;

            foreach( IPAddress addr in ipHost.AddressList ) {
                if( addr.AddressFamily == AddressFamily.InterNetwork ) {
                    Debug.Log("Found IPv4 address: " + addr.ToString());
                    localEnd = new IPEndPoint(addr, 2038);
                    found=true;
                    break;
                }
            }

            if( !found ) {
                Debug.Log("No IPv4 address found, using localhost");
                ipAddr = IPAddress.Parse("127.0.0.1");
                localEnd = new IPEndPoint(ipAddr, 2038);
            }

            //localEnd = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2038);
            ws = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            threads = new NetThreads(this);
            files = new NetFiles(this);

            Debug.Log("Connecting to server...");
            ws.Connect(localEnd);
            
            Debug.Log("Socket connected to " + ws.RemoteEndPoint.ToString());
            TimeSpan ts = DateTime.Now - DateTime.UnixEpoch;
            last_in_time = (ulong)ts.TotalMilliseconds;
            last_out_time = (ulong)ts.TotalMilliseconds;

            threads._sendThread.Start();
            threads._recvThread.Start();
            connected = true;
            if( Player != null ) {
                Debug.Log("[authenticating-1]");
                NetStringBuilder sb = new NetStringBuilder();
                sb.AddFloat( Player.transform.position.x );
                sb.AddFloat( Player.transform.position.y );
                sb.AddFloat( Player.transform.position.z );
                sb.AddFloat( Player.transform.rotation.eulerAngles.x );
                sb.AddFloat( Player.transform.rotation.eulerAngles.y );
                sb.AddFloat( Player.transform.rotation.eulerAngles.z );
                SendMessage( SCommand.Register, sb ); // authenticate (for now)
            }
        }

        public void BuildPlayer( GameObject obj )
        {
            CNetId id = obj.GetComponent<CNetId>();
            if( foundMainUser ) {
                Debug.Log("Found another player: " + id.id);
                id.local = false;
            } else {
                Debug.Log("Found main player: " + id.id);
                foundMainUser=true;
                id.local = true;
            }
            obj.AddComponent<CNetCharacter>();
            if( id.local ) {
                RegisterUser( obj.GetComponent<CNetCharacter>() );
            }
            obj.AddComponent<CNetInfo>();
            obj.AddComponent<CNetLookSource>();
            obj.AddComponent<CNetCharacterLocomotionHandler>();
            obj.AddComponent<CNetTransform>();
        }

        public void RegisterNetObject( ICNetUpdate obj ) {
            netObjects.Add( obj );
        }
        public void RemoveNetObject( ICNetUpdate obj ) {
            netObjects.Remove( obj );
        }

        public void RegisterUser( CNetCharacter player ) {
            Debug.Log("-local player found-");
            Player = player.gameObject;
            if( connected ) {
                Debug.Log("[authenticating]");
                NetStringBuilder sb = new NetStringBuilder();
                sb.AddFloat( Player.transform.position.x );
                sb.AddFloat( Player.transform.position.y );
                sb.AddFloat( Player.transform.position.z );
                sb.AddFloat( Player.transform.rotation.eulerAngles.x );
                sb.AddFloat( Player.transform.rotation.eulerAngles.y );
                sb.AddFloat( Player.transform.rotation.eulerAngles.z );
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
                        last_game_time = (ulong)stream.ReadLongLong();
                        TimeSpan ts = DateTime.Now - DateTime.UnixEpoch;
                        ulong now = (ulong)ts.TotalMilliseconds;
                        net_clock_offset = (long)(now - last_game_time);
                        Debug.Log("Net clock offset: " + net_clock_offset);
                        break;
                    case CCommand.LinkToObject: //unused.
                        Debug.Log("LinkToObject");
                        break;
                    case CCommand.SetObjectPositionRotation:
                        SetObjectPositionRotation(stream);
                        break;
                    case CCommand.RegisterUser: // Register User
                        if( stream.ReadByte() == 0 ) {
                            authoritative = false;
                            max_out_bps = 4096;
                        } else {
                            authoritative = true;
                            max_out_bps = 16000;
                        }

                        //! Save uid
                        local_uid = stream.ReadUint();
                        CNetId cni = Player.GetComponent<CNetId>();
                        cni.id = local_uid;
                        cni.local = true;
                        cni.registered = true;
                        // Do not call cni.Register()

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
                            max_out_bps = 4096;
                        } else {
                            authoritative = true;
                            max_out_bps = 16000;
                        }
                        if( !authoritative ) {
                            foreach( uint key in clientBodies.Keys ) {
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


        public Rigidbody GetRigidbody( uint uid ) {
            if( clientBodies.ContainsKey(uid) )
                return clientBodies[uid];
            return null;
        }

        public void RegisterObj( MonoBehaviour obj )
        {

        }

        public void GetObjects()
        {

        }

        public void RegisterPacket( CNetFlag cmd, uint tgt, PacketCallback callback, int packetSize=0 )
        {
            uint icmd = (uint)cmd;
            if( !commandHandlers.ContainsKey(icmd) ) {
                commandHandlers[icmd] = new Dictionary<uint, PacketCallback>();

                if( packetSize != 0 ) {
                    if( packetSizes.ContainsKey(icmd) && packetSizes[icmd] != packetSize )
                        Debug.Log("Warning: packet size for command " + cmd + " already set to " + packetSizes[icmd] + " (new size: " + packetSize + ")");
                    packetSizes[icmd] = packetSize;
                }
            }
            commandHandlers[icmd][tgt] = callback;
            if( waitingHandlers.ContainsKey(tgt) ) {
                if( waitingHandlers[tgt].ContainsKey(icmd) ) {
                    Debug.Log("Got " + waitingHandlers[tgt][icmd].Count + " waiting packets for " + cmd + " " + tgt);
                    foreach( PacketData data in waitingHandlers[tgt][icmd] ) {
                        callback((ulong)( (long)data.ts + this.net_clock_offset ), data.stream);
                    }
                    waitingHandlers[tgt].Remove(icmd);
                }
                if( waitingHandlers[tgt].Count == 0 )
                    waitingHandlers.Remove(tgt);
            }
            if( tgt > maxTargets ) maxTargets = tgt;
        }



        public void RegisterId( MonoBehaviour obj, String oname, int type )
        {
            CNetId cni = (CNetId)obj;
            if( cni == null ) {
                Debug.Log("Object " + oname + " does not have a CNetId component");
                return;
            }

            if( !registered ) {
                Debug.Log("Object " + oname + " registered before client");
                return;
            }

            if( registered && type != 2 ) {
                GameObject go = obj.gameObject;
                string name = "o_" + oname;

                Debug.Log("Registering object " + name + ", type " + type);
                loadingObjects[name] = obj;
                NetStringBuilder sb = new NetStringBuilder();
                sb.AddString(name);
                sb.AddByte((byte)type); // byte 0 specifies object type
                SendMessage(SCommand.IdentifyVar, sb);
            }
        }

        public GameObject GetObject( uint id )
        {
            if( !clientObjects.ContainsKey(id) ) {
                Debug.Log("Object " + id + " not found");
                return null;
            }
            return clientObjects[id].gameObject;
        }
        public Collider GetCollider( uint id )
        {
            if( !clientColliders.ContainsKey(id) ) {
                Debug.Log("Collider " + id + " not found");
                return null;
            }
            return clientColliders[id];
        }

        public void GotVarInfo(NetStringReader stream)
        {
            VarData v = new VarData();
            v.name = stream.ReadString();
            v.type = stream.ReadByte();
            v.objid = stream.ReadUint();
            MonoBehaviour mb = null;
            CNetId cni = null;
            Rigidbody rb = null;

            Debug.Log("VarInfo: " + v.name + " " + v.objid + " " + v.type);
            serverAssets[v.name] = v;
            if( !loadingObjects.ContainsKey(v.name) ) {
                Debug.Log("Object " + v.name + " not found");
                return;
            }

            switch( v.type ) {
                case 0: // object
                    mb = loadingObjects[v.name];
                    cni = mb.GetComponent<CNetId>();
                    if( cni != null ) {
                        cni.id = (uint)v.objid;
                    } else {
                        Debug.Log("Object " + v.name + " does not have a CNetId component");
                    }
                    clientObjects[v.objid] = mb;
                    rb = mb.GetComponent<Rigidbody>();
                    if( rb != null ) {
                        clientBodies[v.objid] = rb;
                    } else {
                        Debug.Log("Object " + v.name + " does not have a RigidBody component");
                    }
                    loadingObjects.Remove(v.name);
                    if( rb != null && authoritative ) { // send back information about the object
                        Debug.Log("Sending object " + v.name + " to server");
                        SendObject( mb );
                    }
                    break;
                case 1: // collider
                    mb = loadingObjects[v.name];
                    cni = mb.GetComponent<CNetId>();
                    if( cni != null ) {
                        cni.id = (uint)v.objid;
                    } else {
                        Debug.Log("Object " + v.name + " does not have a CNetId component");
                    }
                    clientColliders[v.objid] = mb.GetComponent<Collider>();
                    break;
                case 2: // player
                    Debug.Log("[info] Player " + v.name + " has id " + v.objid);
                    break;
            }
        }



        public void SendObject( MonoBehaviour mb )
        {
            CNetId cni = (CNetId)mb.GetComponent<CNetId>();
            if( cni == null ) {
                Debug.Log("Object " + mb.name + " does not have a CNetId component");
                return;
            }
            if( !cni.registered ) {
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

            SendMessage(SCommand.SetObjectPositionRotation, sb, 50000 + cni.id*1 + (int)SCommand.SetObjectPositionRotation );
            Debug.Log("Sent object " + cni.id + ": " + rb.position.x + ", " + rb.position.y + ", " + rb.position.z + "  " + rb.rotation.x + ", " + rb.rotation.y + ", " + rb.rotation.z + ", " + rb.rotation.w);
        }

        public void SetObjectPositionRotation(NetStringReader stream)
        {
            uint objid;
            int ts_short;
            ulong ts;
            float x,y,z;
            float r0,r1,r2,r3;

            objid = stream.ReadUint();
            ts_short = stream.ReadInt();
            ts = (ulong)ts_short + (ulong)last_game_time;

            x = stream.ReadFloat();
            y = stream.ReadFloat();
            z = stream.ReadFloat();
            r0 = stream.ReadFloat();
            r1 = stream.ReadFloat();
            r2 = stream.ReadFloat();
            r3 = stream.ReadFloat();

            Debug.Log("SetObjectPositionRotation: " + objid + ": " + x + " " + y + " " + z + " " + r0 + " " + r1 + " " + r2 + " " + r3);
            Rigidbody rb = clientBodies[objid];
            rb.MovePosition(new Vector3(x,y,z));
            rb.MoveRotation(new Quaternion(r0,r1,r2,r3));
        }

        public void NewUser(NetStringReader stream)
        {
            uint uid = stream.ReadUint();
            Debug.Log("NewUser " + uid);

            Vector3 startPos = new Vector3();
            startPos.x = stream.ReadFloat();
            startPos.y = stream.ReadFloat();
            startPos.z = stream.ReadFloat();
            float r0 = stream.ReadFloat();
            float r1 = stream.ReadFloat();
            float r2 = stream.ReadFloat();
            Quaternion startRot = Quaternion.Euler(r0, r1, r2);

            Debug.Log("NewUser Create Avatar at " + startPos + " " + startRot);
            var dynamicCharacterAvatar = CharactersManager.Instance.CreateDynamicCharacter(startPos, r1,
                                CharactersManager.CharacterFeatures.Pedestrian, CharactersManager.CharacterQuality.Full);
            if (!dynamicCharacterAvatar) {
                Debug.Log("NewUser rejected");
                return;
            }

            var characterBuilder = dynamicCharacterAvatar.GetComponent<UMACharacterBuilder>();
            //characterBuilder.m_Build = false;
            characterBuilder.m_AIAgent = true;
            characterBuilder.m_AddItems = true;

            Application.backgroundLoadingPriority = UnityEngine.ThreadPriority.Low;
            dynamicCharacterAvatar.BuildCharacter();
            Application.backgroundLoadingPriority = UnityEngine.ThreadPriority.Low;

            serverUserObjects[uid] = dynamicCharacterAvatar.gameObject;

            Debug.Log("NewUser Get CNetId");
            CNetId cni = dynamicCharacterAvatar.GetComponent<CNetId>();
            cni.local = false;
            cni.id = uid;
            cni.Register();

            Debug.Log("NewUser Done, Building can continue");
        }

        public void UserQuit(NetStringReader stream)
        {
            uint uid = stream.ReadUint();
            GameObject go = serverUserObjects[uid];

            Destroy(go);
            serverUserObjects.Remove(uid);
        }

        public CNetCharacter GetUser( uint id )
        {
            return serverUserObjects[id].GetComponent<CNetCharacter>();
        }



        public void SendDynPacket( CNetFlag cmd, uint tgt, byte[] data )
        {
            uint icmd = (uint)cmd;
            NetStringBuilder sb = new NetStringBuilder();

            sb.AddUint(icmd);
            sb.AddUint(tgt);

            TimeSpan ts = DateTime.Now - DateTime.UnixEpoch;
            uint ts_short = (uint)(ts.TotalMilliseconds - last_record_time);
            sb.AddUint(ts_short);

            if( data != null ) {
                sb.AddShortBytes(data);
            } else {
                sb.AddUint(0);
            }
            SendMessage(SCommand.DynPacket, sb);
        }
        public void SendDynPacket( CNetFlag cmd, uint tgt, NetStringBuilder dataptr=null )
        {
            NetStringBuilder sb = new NetStringBuilder();
            uint icmd = (uint)cmd;

            sb.AddUint(icmd);
            sb.AddUint(tgt);

            TimeSpan ts = DateTime.Now - DateTime.UnixEpoch;
            uint ts_short = (uint)(ts.TotalMilliseconds - last_record_time);
            sb.AddUint(ts_short);

            if( dataptr != null ) {
                dataptr.Reduce();
                sb.AddShortBytes(dataptr.ptr);
            } else {
                sb.AddUint(0);
            }
            SendMessage(SCommand.DynPacket, sb);
        }
        public void RecvDynPacket( NetStringReader stream )
        {
            uint cmd, tgt, ts_short;
            ulong ts;
            byte[] detail;

            cmd = stream.ReadUint();
            tgt = stream.ReadUint();
            ts_short = stream.ReadUint();
            ts = (ulong)ts_short + (ulong)last_game_time;

            detail = stream.ReadShortBytes();
            if( detail == null ) {
                Debug.Log("RecvDynPacket: no detail for cmd " + cmd + " to tgt " + tgt);
            }
            NetStringReader param = new NetStringReader(detail);
            PacketData p;

            if( commandHandlers.ContainsKey(cmd) ) {
                if( commandHandlers[cmd].ContainsKey(tgt) ) {
                    commandHandlers[cmd][tgt]((ulong)( (long)ts + this.net_clock_offset ), param);
                } else {
                    if( !waitingHandlers.ContainsKey(tgt) ) {
                        waitingHandlers[tgt] = new Dictionary<uint, List<PacketData>>();
                    }
                    if( !waitingHandlers[tgt].ContainsKey(cmd) ) {
                        waitingHandlers[tgt][cmd] = new List<PacketData>();
                    }
                    p = new PacketData();
                    p.ts = (ulong)( (long)ts + this.net_clock_offset );
                    p.stream = param;
                    waitingHandlers[tgt][cmd].Add(p);
                }
            } else {
                if( !waitingHandlers.ContainsKey(tgt) ) {
                    waitingHandlers[tgt] = new Dictionary<uint, List<PacketData>>();
                }
                if( !waitingHandlers[tgt].ContainsKey(cmd) ) {
                    waitingHandlers[tgt][cmd] = new List<PacketData>();
                }
                p = new PacketData();
                p.ts = (ulong)( (long)ts + this.net_clock_offset );
                p.stream = param;
                waitingHandlers[tgt][cmd].Add(p);
            }
        }
        
        public void SendPacket( CNetFlag cmd, uint tgt, NetStringBuilder dataptr=null, bool instantSend=false )
        {
            uint icmd = (uint)cmd;
            NetStringBuilder sb = new NetStringBuilder();
            long code = (long)(tgt+1) + ((long)(icmd+1) * (long)maxTargets);

            sb.AddUint(icmd);
            sb.AddUint(tgt);

            TimeSpan ts = DateTime.Now - DateTime.UnixEpoch;
            uint ts_short = (uint)(ts.TotalMilliseconds - last_record_time);
            sb.AddUint(ts_short);

            if( dataptr != null ) {
                dataptr.Reduce();
                if( packetSizes.ContainsKey(icmd) ) {
                    if( dataptr.used != packetSizes[icmd] ) {
                        Debug.LogError("Packet size mismatch: " + icmd + " from: " + tgt + ": found " + dataptr.used + ", expected " + packetSizes[icmd]);
                        return; // Do not send it.
                    }
                }
                sb.AddBytes(dataptr.ptr);
            }
            Debug.Log("SendPacket " + cmd + " for " + tgt + " with " + sb.used + " bytes, " + (instantSend?"instant":"delayed"));
            /*
            byte b;
            int i;
            sb.Reduce();
            for( i=0; i<sb.used; i++ ) {
                b = sb.ptr[i];
                Debug.Log("Packet: " + b);
            }
            */
            SendMessage(SCommand.Packet, sb, instantSend?0:code);
        }
        public void SendPacket( CNetFlag cmd, uint tgt, byte[] data, bool instantSend=false )
        {
            uint icmd = (uint)cmd;
            NetStringBuilder sb = new NetStringBuilder();
            long code = (long)(tgt+1) + ((long)(icmd+1) * (long)maxTargets);

            sb.AddUint(icmd);
            sb.AddUint(tgt);

            TimeSpan ts = DateTime.Now - DateTime.UnixEpoch;
            uint ts_short = (uint)(ts.TotalMilliseconds - last_record_time);
            sb.AddUint(ts_short);

            if( data != null ) {
                if( packetSizes.ContainsKey(icmd) ) {
                    if( data.Length != packetSizes[icmd] ) {
                        Debug.LogError("Packet size mismatch: " + icmd + " from: " + tgt + ": found " + data.Length + ", expected " + packetSizes[icmd]);
                        return; // Do not send it.
                    }
                }
                sb.AddBytes(data);
            }
            //Debug.Log("SendPacket " + cmd + " for " + tgt + " with " + sb.used + " bytes, " + (instantSend?"instant":"delayed"));
            SendMessage(SCommand.Packet, sb, instantSend?0:code);
        }
        public void RecvPacket( NetStringReader stream )
        {
            uint cmd, tgt, ts_short;
            ulong ts;
            byte[] detail;

            cmd = stream.ReadUint();
            tgt = stream.ReadUint();
            ts_short = stream.ReadUint();
            ts = ts_short + last_game_time;

            NetStringReader param;
            PacketData p;
            if( packetSizes.ContainsKey(cmd) ) {
                detail = stream.ReadFixedBytes(packetSizes[cmd]);
            } else {
                detail = stream.ReadFixedBytes( stream.data.Length - stream.offset );
            }
            param = new NetStringReader(detail);

            if( commandHandlers.ContainsKey(cmd) ) {
                if( commandHandlers[cmd].ContainsKey(tgt) ) {
                    commandHandlers[cmd][tgt]((ulong)( (long)ts + this.net_clock_offset ), param);
                } else {
                    if( !waitingHandlers.ContainsKey(tgt) ) {
                        waitingHandlers[tgt] = new Dictionary<uint, List<PacketData>>();
                    }
                    if( !waitingHandlers[tgt].ContainsKey(cmd) ) {
                        waitingHandlers[tgt][cmd] = new List<PacketData>();
                    }
                    p = new PacketData();
                    p.ts = (ulong)( (long)ts + this.net_clock_offset );
                    p.stream = param;
                    waitingHandlers[tgt][cmd].Add(p);
                }
            } else {
                if( !waitingHandlers.ContainsKey(tgt) ) {
                    waitingHandlers[tgt] = new Dictionary<uint, List<PacketData>>();
                }
                if( !waitingHandlers[tgt].ContainsKey(cmd) ) {
                    waitingHandlers[tgt][cmd] = new List<PacketData>();
                }
                p = new PacketData();
                p.ts = (ulong)( (long)ts + this.net_clock_offset );
                p.stream = param;
                waitingHandlers[tgt][cmd].Add(p);            }
        }



        public void Update()
        {
            TimeSpan ts = DateTime.Now - DateTime.UnixEpoch;
            last_local_time = (ulong)ts.TotalMilliseconds;

            if( last_local_time-last_in_time >= 1000 ) {
                last_in_time = last_local_time;
                lock( _inbpsLock ) {
                    if( in_bps_measure == 0 ) {
                        in_bytes.Clear();
                    } else if( in_bytes.Count > 4 ) {
                        in_bytes.RemoveAt(0);
                    }
                    if( in_bps_measure != 0 )
                        in_bytes.Add( in_bps_measure );
                    in_bps_measure = 0;
                }

            }
            if( last_local_time-last_out_time >= 1000 ) {
                last_out_time = last_local_time;
                lock( _outbpsLock ) {
                    if( out_bps_measure == 0 ) {
                        out_bytes.Clear();
                    } else if( out_bytes.Count > 4 ) {
                        out_bytes.RemoveAt(0);
                    }
                    if( out_bps_measure != 0 )
                        out_bytes.Add( out_bps_measure );
                    out_bps_measure = 0;
                }
            }

            if( last_local_time-last_netupdate >= 1000f/updateRate ) {
                last_netupdate = last_local_time;

                foreach( ICNetUpdate item in netObjects ) {
                    //! Todo: check if item is still valid.
                    item.NetUpdate();
                }                    
            }

            if( last_local_time-last_record_time >= 10000 ) {
                last_record_time = last_local_time;
                NetStringBuilder sb = new NetStringBuilder();
                sb.AddULongLong(last_local_time);
                SendMessage(SCommand.ClockSync, sb);
            }

            flushDelayQueue();
            if( sendBuffers.Count > 0 ) {
                sendPackets();
            }
            Process();
        }

        public void SendMessage( SCommand cmd, NetStringBuilder sb, long code=0 )
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
            //Debug.Log("Encode len: " + len + " " + cmd + " " + msg[1] + " " + msg[2]);

            if( code == 0 ) {
                _sendQSig.Reset();
                lock( _sendBlock ) {
                    send(msg, data != null ? data.Length : 0);

                    if( data != null )
                        send(data);
                }
                _sendQSig.Set();
            } else {
                sendBuffer(code, msg, data);
            }
        }

        public void SendMessage2( SCommand cmd, byte[] data, long code=0 )
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

            if( code == 0 ) {
                _sendQSig.Reset();
                lock( _sendBlock ) {
                    send(msg, data != null ? data.Length : 0);

                    if( data != null )
                        send(data);
                    //else
                    //    Debug.Log("Message with no data: " + cmd);
                }
                _sendQSig.Set();
            } else {
                sendBuffer(code, msg, data);
            }
        }

        public long last_send_time=0;
        public int max_out_bps=4096;
        public int second_bytes=0;
        private Queue<byte[]> delayQ = new Queue<byte[]>();

        public void send(byte[] data, int additionalData=0) {

            TimeSpan ts = DateTime.Now - DateTime.UnixEpoch;
            long cur_send_time = (long)ts.TotalMilliseconds;
            float elapsed = (cur_send_time-last_send_time)/1000.0f;
            if( elapsed > 1.0f ) {
                elapsed=1.0f;
            }
            if( second_bytes+additionalData+data.Length > max_out_bps*elapsed ) {
                //Debug.Log("overflow to delayQ");
                delayQ.Enqueue(data);
                return;
            }
            if( delayQ.Count > 0 ) { // do not let any data through if we have data in the delay queue
                //Debug.Log("delayQ blocking data");
                delayQ.Enqueue(data);
                return;
            }
            second_bytes += data.Length;
            lock (_sendQLock) {
                sendQ.Enqueue(data);
            }
        }
        public void flushDelayQueue()
        {
            if( delayQ.Count <= 0 ) return;

            TimeSpan ts = DateTime.Now - DateTime.UnixEpoch;
            long cur_send_time = (long)ts.TotalMilliseconds;
            float elapsed = (cur_send_time-last_send_time)/1000.0f;

            if( elapsed > 1.0f ) {
                elapsed = 1.0f;
                last_send_time = cur_send_time;
                second_bytes = 0;
            }

            if( second_bytes > elapsed*max_out_bps ) {
                return;
            }

            _sendQSig.Reset();
            lock( _sendBlock ) {
                while( delayQ.Count > 0 ) {
                    byte[] data = delayQ.Dequeue();
                    second_bytes += data.Length;
                    lock (_sendQLock) {
                        sendQ.Enqueue(data);
                    }
                    //Debug.Log("flushQueue: " + data.Length + " bytes");
                    if( data.Length != 3 && second_bytes > elapsed*max_out_bps ) { // do not end if we just sent a header.
                        Debug.Log("end on flushQueue");
                        break;
                    }
                }
            }
            _sendQSig.Set();
        }

        private Dictionary<long, byte[]> sendHeaders = new Dictionary<long, byte[]>();
        private Dictionary<long, byte[]> sendBuffers = new Dictionary<long, byte[]>();
        public void sendPackets() {
            TimeSpan ts = DateTime.Now - DateTime.UnixEpoch;
            long cur_time = (long)ts.TotalMilliseconds;
            float elapsed = (cur_time-last_send_time)/1000.0f;
            if( second_bytes >= max_out_bps*elapsed ) {
                Debug.Log("sendPackets delayed: " + elapsed + " " + second_bytes);
                return;
            }
            if( sendBuffers.Count > 0 )
                //Debug.Log("sendPackets: " + sendBuffers.Count);
            lock( _sendQLock ) {
                List<long> keys = new List<long>();
                _sendQSig.Reset();
                foreach( var ks in sendBuffers ) {
                    var k = ks.Key;
                    second_bytes += sendHeaders[k].Length;
                    sendQ.Enqueue(sendHeaders[k]);
                    second_bytes += sendBuffers[k].Length;
                    sendQ.Enqueue(sendBuffers[k]);
                    keys.Add(k);
                    /* send at least one of every packet.
                    if( second_bytes > max_out_bps*elapsed ) {
                        Debug.Log("-partial sendPackets: " + keys.Count + " sent");
                        break;
                    } */
                }
                _sendQSig.Set();
                foreach( var k in keys ) {
                    sendHeaders.Remove(k);
                    sendBuffers.Remove(k);
                }
            }
        }

        public void sendBuffer(long code, byte[] head, byte[] data) {
            //Debug.Log("sendBuffer(" + code + ")");
            sendHeaders[code] = head;
            sendBuffers[code] = data;
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

}
