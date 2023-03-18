using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using Opsive.UltimateCharacterController.Objects;
using Opsive.UltimateCharacterController.Inventory;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Objects.CharacterAssist;
using Opsive.Shared.Input.VirtualControls;
using Opsive.Shared.Input;
using Opsive.Shared.Events;
using UMA;
using UMA.CharacterSystem;
using System;

namespace CNet {
    public interface ICNetReg
    {
        void Register( );
        void Delist( );
    }
    public interface ICNetUpdate
    {
        void NetUpdate( );
    };
    public interface ICNetEvent
    {
        void Event( CNetEvent ev, uint from, NetStringReader data );
    }

    [ExecuteInEditMode]
    public class NetSocket : MonoBehaviour
    {
        public static NetSocket Instance=null;

        public NetSocket() {
            if( Instance != null && Instance.connected ) {
                Instance.close();
            }
            Instance = this;
        }

#if (UNITY_EDITOR)
        [UnityEditor.Callbacks.DidReloadScripts]
        private static void OnScriptsReloaded() {
            if( Instance == null ) {
                //Debug.Log("Instance was null");
                return;
            }
            ObjectIdentifier[] comps = GameObject.FindObjectsOfType<ObjectIdentifier>();
            int i;
            Instance.top_objid = 0;
            for( i=0; i<comps.Length; i++ ) {
                if( comps[i].ID > Instance.top_objid ) {
                    Instance.top_objid = comps[i].ID;
                }
            }
            //Debug.Log("Found " + comps.Length + " components with ObjectIdentifier, top ID " + Instance.top_objid);
        }
#endif

        [Tooltip("Rate of updates per second. This is for testing purposes.")]
        public float updateRate = 15f;

        public GameObject Player = null;

        public bool authoritative = false;
        public bool connected = false;
        public bool registered = false;

        public uint local_uid;

        [SerializeField]
        public uint top_objid;

        public delegate void PacketCallback( ulong ts, NetStringReader data );
        public delegate void SetHourCb( int hour );
        public delegate void SetSpeedCb( float speed );

        private SetHourCb clockHourCb;
        private SetSpeedCb clockSpeedCb;
        public void SetupClock( SetHourCb hourCb, SetSpeedCb speedCb )
        {
            clockHourCb = hourCb;
            clockSpeedCb = speedCb;
        }

        public delegate GameObject CreateRemoteAvatarFunc( Vector3 pos, float rot );
        private CreateRemoteAvatarFunc CreateRemoteAvatar;
        public delegate GameObject CreateRandomAvatarFunc( Vector3 pos, float rot );
        private CreateRandomAvatarFunc CreateRandomAvatar;
        public void SetupCharacterManager( CreateRemoteAvatarFunc cb1, CreateRandomAvatarFunc cb2 )
        {
            CreateRemoteAvatar = cb1;
            CreateRandomAvatar = cb2;
        }



        public readonly object _sendQLock = new object();
        public readonly Queue<byte[]> sendQ = new Queue<byte[]>();
        public readonly AutoResetEvent _sendQSig = new AutoResetEvent(false);

        public readonly object _recvQLock = new object();
        public readonly Queue<byte[]> recvQ = new Queue<byte[]>();

        public readonly object _regLock = new object();
        public readonly ManualResetEvent _regSig = new ManualResetEvent(false);

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

        private Dictionary<string, VarData> serverAssets = new Dictionary<string, VarData>();
        private Dictionary<string, MonoBehaviour> loadingObjects = new Dictionary<string, MonoBehaviour>();
        private List<ICNetUpdate> netObjects = new List<ICNetUpdate>();
        private Dictionary<uint, ObjData> serverObjects = new Dictionary<uint, ObjData>();
        private Dictionary<uint, MonoBehaviour> clientObjects = new Dictionary<uint, MonoBehaviour>();
        private Dictionary<uint, Rigidbody> clientBodies = new Dictionary<uint, Rigidbody>();
        private Dictionary<uint, Dictionary<uint, PacketCallback>> commandHandlers = new Dictionary<uint, Dictionary<uint, PacketCallback>>();
        private Dictionary<uint, int> packetSizes = new Dictionary<uint, int>();
        private Dictionary<uint, ObjData> serverUsers = new Dictionary<uint, ObjData>();
        private Dictionary<uint, GameObject> serverUserObjects = new Dictionary<uint, GameObject>();
        private Dictionary<uint, GameObject> serverNpcs = new Dictionary<uint, GameObject>();
        private Dictionary<uint, Dictionary<uint, List<PacketData>>> waitingHandlers = new Dictionary<uint, Dictionary<uint, List<PacketData>>>();
        private Dictionary<byte, List<ICNetEvent>> eventHandlers = new Dictionary<byte, List<ICNetEvent>>();

        private ulong last_game_time = 0;
        private ulong last_local_time = 0;
        private ulong last_record_time = 0;
        public long net_clock_offset = 0;
        public ulong last_netupdate = 0;
        public ulong last_rtt_packet = 0;

        public bool record_bps = true;
        public bool record_rtt = true;
        private bool foundMainUser = false;

        public List<int> out_bytes = new List<int>();
        public List<int> in_bytes = new List<int>();
        public List<int> rtt_times = new List<int>();
        public List<int> c2sl_times = new List<int>();
        public List<int> s2cl_times = new List<int>();
        public List<long> clocksyncs = new List<long>();
        public long clocksync;

        public ulong last_out_time = 0;
        public ulong last_in_time = 0;
        public ulong last_rtt_time = 0;

        public int in_bps_measure = 0;
        public int out_bps_measure = 0;

        public readonly object _inbpsLock = new object();
        public readonly object _outbpsLock = new object();
        public readonly object _rttLock = new object();
        public readonly object _c2slLock = new object();
        public readonly object _s2clLock = new object();
        public uint maxTargets = 0;

        public void Awake()
        {
            if( Instance != null && Instance != this ) {
                if( Instance.connected ) {
                    Instance.close();
                }
            }
            Instance = this;
            CreateRemoteAvatar = null;
            CreateRandomAvatar = null;
        }
        
        public void Start()
        {
            if( !Application.IsPlaying(gameObject) ) {
                //Debug.Log("Hello, editor!");
                return;
            }
        }

        private string playerName = "Player1";
        public void SetPlayerName( string name )
        {
            playerName = name;
        }
        public bool debugMode = false;
        public void ToggleDebug()
        {
            debugMode = !debugMode;
        }

        public bool Connect( string host )
        {
            NetStringBuilder sb;

            lock( _regLock ) {
                IPHostEntry ipHost = Dns.GetHostEntry(host);
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
                    Debug.Log("No IPv4 address found");
                    return false;
                }

                //localEnd = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2038);
                ws = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                threads = new NetThreads(this);
                threads.disconnected = false;
                files = new NetFiles(this);

                Debug.Log("Connecting to server...");
                try {
                    ws.Connect(localEnd);
                } catch( SocketException e ) {
                    Debug.Log("SocketException: " + e.ToString());
                    threads.disconnected = true;
                    return false;
                }

                if( !ws.Connected ) {
                    Debug.Log("Socket not connected");
                    threads.disconnected = true;
                    return false;
                }

                Debug.Log("Socket connected to " + ws.RemoteEndPoint.ToString());
                TimeSpan ts = DateTime.Now - DateTime.UnixEpoch;
                last_in_time = (ulong)ts.TotalMilliseconds;
                last_out_time = (ulong)ts.TotalMilliseconds;

                delayQ.Clear();
                sendQ.Clear();
                sendBuffers.Clear();
                sendHeaders.Clear();

                threads._sendThread.Start();
                threads._recvThread.Start();
                connected = true;
            }

            if( Player != null ) {
                //Debug.Log("[authenticating-1]");
                sb = new NetStringBuilder();
                sb.AddFloat( Player.transform.position.x );
                sb.AddFloat( Player.transform.position.y );
                sb.AddFloat( Player.transform.position.z );
                sb.AddFloat( Player.transform.rotation.eulerAngles.x );
                sb.AddFloat( Player.transform.rotation.eulerAngles.y );
                sb.AddFloat( Player.transform.rotation.eulerAngles.z );
                sb.AddString( playerName );
                SendMessage( SCommand.Register, sb ); // authenticate (for now)
            }

            sb = new NetStringBuilder();
            sb.AddUint(top_objid);
            SendMessage( SCommand.ObjectTop, sb ); // send what we think the top object id should be

            return true;
        }

        public void RegisterEvent( ICNetEvent obj, CNetEvent evt ) {
            if( !eventHandlers.ContainsKey( (byte)evt ) ) {
                eventHandlers[(byte)evt] = new List<ICNetEvent>();
            }
            eventHandlers[(byte)evt].Add( obj );
        }
        public void UnregisterEvent( ICNetEvent obj, CNetEvent evt ) {
            if( eventHandlers.ContainsKey( (byte)evt ) ) {
                eventHandlers[(byte)evt].Remove( obj );
            }
        }
        public void ExecuteEvent( CNetEvent evt, uint source, NetStringReader stream ) {
            if( eventHandlers.ContainsKey( (byte)evt ) ) {
                foreach( ICNetEvent obj in eventHandlers[(byte)evt] ) {
                    obj.Event( evt, source, stream );
                }
            }
        }


        public void RegisterNetObject( ICNetUpdate obj ) {
            netObjects.Add( obj );
        }
        public void UnregisterNetObject( ICNetUpdate obj ) {
            netObjects.Remove( obj );
        }


        // Process() gets called by Update()
        public void Process() {
            IList<byte[]> res = recv();
            NetStringReader stream;
            Rigidbody rb;
            int cmd, sz;
            TimeSpan ts;
            ulong now;

            foreach( byte[] data in res ) {
                stream = new NetStringReader(data);
                cmd = stream.ReadByte();
                sz = (int)stream.ReadUint(); // size
                if( stream.data.Length - stream.offset != sz ) {
                    Debug.LogError("Error: packet size mismatch " + sz + " vs " + (stream.data.Length - stream.offset));
                    continue;
                }
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
                        ts = DateTime.Now - DateTime.UnixEpoch;
                        now = (ulong)ts.TotalMilliseconds;
                        net_clock_offset = (long)(now - last_game_time);
                        clocksyncs.Add( net_clock_offset );
                        if( clocksyncs.Count > 10 ) {
                            clocksyncs.RemoveAt(0);
                        }
                        long sum = 0;
                        int count = 0;
                        foreach( long l in clocksyncs ) {
                            sum += (long)l;
                            count++;
                        }
                        clocksync = (sum / (long)count);
                        Debug.Log("Avg clock offset: " + clocksync);
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
                        max_out_bps = 16000;

                        //! Save uid
                        local_uid = stream.ReadUint();
                        CNetId cni = Player.GetComponent<CNetId>();
                        cni.id = local_uid;
                        cni.local = true;
                        Debug.Log("Logged in as " + local_uid + ( authoritative ? " (authoritative)" : " (client)"));
                        
                        serverUserObjects[cni.id] = Player;
                        cni.Register();

                        SendMessage( SCommand.GetFileList, null ); // request file list
                        break;
                    case CCommand.TopObject:
                        registered = true;
                        // request variable idents
                        FinishConnectionWait();
                        /*
                        foreach( string key in loadingObjects.Keys ) {
                            Debug.Log("Late-registering object " + key);
                            sb = new NetStringBuilder();
                            sb.AddString(key);
                            sb.AddByte(0);
                            SendMessage( SCommand.IdentifyVar, sb );
                        }
                        */
                        break;
                    case CCommand.ChangeUserRegistration:
                        if( stream.ReadByte() == 0 ) {
                            authoritative = false;
                            max_out_bps = 4096;
                        } else {
                            authoritative = true;
                            max_out_bps = 16000;
                        }

                        foreach( uint key in clientBodies.Keys ) {
                            rb = clientBodies[key];
                            CNetId cni2 = rb.GetComponent<CNetId>();
                            cni2.Delist();
                            cni2.local = authoritative;
                            cni2.Register();
                            rb.isKinematic = !authoritative;
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
                    case CCommand.ClockSetHour:
                        var hour = stream.ReadUint();
                        clockHourCb((int)hour);
                        Debug.Log("Set Hour to " + hour);
                        break;
		            case CCommand.ClockSetDaySpeed:
                        var speed = stream.ReadFloat();
                        clockSpeedCb(speed);
                        Debug.Log("Set Day Speed to " + speed);
                        break;
                    case CCommand.RTTEcho:
                        var rtt = stream.ReadULongLong();
                        var c2sl_measure = (int)stream.ReadLong();

                        ts = DateTime.Now - DateTime.UnixEpoch;
                        now = (ulong)ts.TotalMilliseconds;

                        lock( _c2slLock ) {
                            if( c2sl_times.Count > 14 ) {
                                c2sl_times.RemoveAt(0);
                            }
                            c2sl_times.Add(c2sl_measure);
                        }
                        
                        int my_rtt = (int)(now - rtt);
                        lock( _rttLock ) {
                            if( rtt_times.Count > 14 ) { // keep an average of 5 seconds
                                rtt_times.RemoveAt(0);
                            }
                            rtt_times.Add(my_rtt);
                        }

                        break;
                    case CCommand.ObjectClaim:
                        uint objid = stream.ReadUint();
                        uint uid = stream.ReadUint();

                        Debug.Log("Object " + objid + " claimed by " + uid + (uid == local_uid ? " (me)" : " (other)"));

                        if( clientBodies.ContainsKey(objid) ) {
                            rb = clientBodies[objid];
                            if( uid == local_uid ) {
                                rb.isKinematic = false;
                                CNetId cni2 = rb.GetComponent<CNetId>();
                                cni2.Delist();
                                cni2.local = true;
                                cni2.Register();
                            } else {
                                rb.isKinematic = true;
                                CNetId cni2 = rb.GetComponent<CNetId>();
                                cni2.Delist();
                                cni2.local = false;
                                cni2.Register();
                            }
                        }
                        break;
                    case CCommand.Spawn:
                        byte type = stream.ReadByte();

                        Debug.Log("Spawn " + type);

                        if( type == 99 ) {
                            ResolveSpawn(stream);
                        } else if( type == 1 ) {
                            SpawnNpc(stream);
                        } else if( type == 0 ) {
                            Debug.Log("err: " + stream.ReadUint() + ", " + stream.ReadUint());
                            //SpawnObject(stream);
                        }
                        break;
                    case CCommand.NPCRecipe:
                        Debug.Log("Got npc recipe command");
                        uint npc_id = stream.ReadUint();
                        string recipe = stream.ReadLongString();

                        if( serverNpcs.ContainsKey(npc_id) ) {
                            Debug.Log("Got npc recipe command target");
                            GameObject npc = serverNpcs[npc_id];
                            var avatar = npc.GetComponent<DynamicCharacterAvatar>();
                            //var asset = ScriptableObject.CreateInstance<UMATextRecipe>();
                            //asset.Save(avatar.umaData.umaRecipe, avatar.context);
                            //asset.recipeString = recipe;
                            avatar.LoadFromRecipeString(recipe);
                            Debug.Log("NPC Loaded");
                        }
                        break;
                }
            }
        }

        public void close() {
            threads.disconnected = true;
            if( connected ) {
                ws.Close();
                connected = registered = false;
            }
        }

        public void OnDestroy() {
            if( connected ) {
                close();
            }
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

        public void UnregisterPacket( CNetFlag cmd, uint tgt )
        {
            uint icmd = (uint)cmd;
            if( commandHandlers.ContainsKey(icmd) ) {
                if( commandHandlers[icmd].ContainsKey(tgt) ) {
                    commandHandlers[icmd].Remove(tgt);
                }
            }
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
                    if( debugMode )
                        Debug.Log("Got " + waitingHandlers[tgt][icmd].Count + " waiting packets for " + cmd + " " + tgt);
                    foreach( PacketData data in waitingHandlers[tgt][icmd] ) {
                        if( debugMode )
                            Debug.Log("Datastream contains " + data.stream.data.Length + " bytes");
                        callback((ulong)( (long)data.ts + this.net_clock_offset ), data.stream);
                    }
                    waitingHandlers[tgt].Remove(icmd);
                }
                if( waitingHandlers[tgt].Count == 0 )
                    waitingHandlers.Remove(tgt);
            }
            if( tgt > maxTargets ) maxTargets = tgt;
        }

        private Dictionary<uint, ObjectIdentifier> s_SceneIDMap = new Dictionary<uint, ObjectIdentifier>();
        private Dictionary<GameObject, Dictionary<uint, ObjectIdentifier>> s_IDObjectIDMap = new Dictionary<GameObject, Dictionary<uint, ObjectIdentifier>>();

        public void RegisterObjectIdentifier(ObjectIdentifier cnetobjid)
        {
            if (s_SceneIDMap.ContainsKey(cnetobjid.ID)) {
                Debug.LogError($"Error: The scene object ID {cnetobjid.ID} already exists. This can be corrected by running Scene Setup again on this scene.", cnetobjid);
                return;
            }
            s_SceneIDMap.Add(cnetobjid.ID, cnetobjid);
        }
        public void UnregisterObjectIdentifier(ObjectIdentifier cnetobjid)
        {
            s_SceneIDMap.Remove(cnetobjid.ID);
        }

        private class WaitHandler
        {
            public MonoBehaviour obj;
            public string name;
            public int type;
        }

        private List<WaitHandler> waitingObjects = new List<WaitHandler>();

        public void FinishConnectionWait()
        {
            foreach( WaitHandler wh in waitingObjects ) {
                CNetId cni = (CNetId)wh.obj.GetComponent<CNetId>();
                //Debug.Log("Round2, Registering object " + wh.name + ", type " + wh.type);

                if( wh.type == 0 ) {
                    loadingObjects[wh.name] = wh.obj;
                    NetStringBuilder sb = new NetStringBuilder();
                    sb.AddString(wh.name);
                    sb.AddByte((byte)wh.type); // byte 0 specifies object type
                    SendMessage(SCommand.IdentifyVar, sb);
                } else if( wh.type == 1 ) {
                    clientObjects[cni.id] = wh.obj;
                    Rigidbody rb = wh.obj.GetComponent<Rigidbody>();
                    cni.local = authoritative;
                    cni.Register();
                    if( rb != null ) {
                        clientBodies[cni.id] = rb;
                        if( authoritative ) { // send back information about the object
                            //Debug.Log("Sending object " + wh.obj.name + " to server");
                            SendObject( wh.obj );
                        }
                    }
                }  
            }
            waitingObjects.Clear();
        }
        public void RegisterId( MonoBehaviour obj, string oname, int type )
        {
            CNetId cni = (CNetId)obj;
            if( cni == null ) {
                Debug.Log("Object " + oname + " does not have a CNetId component");
                return;
            }

            if( !registered ) {
                //Debug.Log("Object " + oname + " registered before client");
                WaitHandler wh = new WaitHandler();
                wh.obj = obj;
                wh.name = oname;
                wh.type = type;
                waitingObjects.Add(wh);
            } else {
                if( debugMode )
                    Debug.Log("Registering object " + oname + ", type " + type);
                if( type == 0 ) {
                    loadingObjects[oname] = obj;
                    NetStringBuilder sb = new NetStringBuilder();
                    sb.AddString(oname);
                    sb.AddByte((byte)type); // byte 0 specifies object type
                    SendMessage(SCommand.IdentifyVar, sb);
                    // IdentifyVar will call GotVarInfo which will add the object to the clientObjects and clientBodies lists
                } else if( type == 1 ) {
                    clientObjects[cni.id] = obj;
                    Rigidbody rb = obj.GetComponent<Rigidbody>();
                    if( rb != null ) {
                        clientBodies[cni.id] = rb;
                    }
                    cni.local = authoritative;
                    cni.Register();
                    if( rb != null && authoritative ) { // send back information about the object
                        //Debug.Log("Sending object " + obj.name + " to server");
                        SendObject( obj );
                    }
                } else if( type == 2 ) {
                    Debug.LogError("Character object " + oname + " registered");
                }
            }
        }

        public int GetMoveTowardsId( MoveTowardsLocation mcl )
        {
            var parentObject = mcl.gameObject.transform.parent.gameObject;

            CNetId cni = parentObject.GetComponent<CNetId>();
            if( cni == null ) {
                return -1;
            }
            int i;
            var mcls = parentObject.GetComponentsInChildren<MoveTowardsLocation>();
            int itemSlotID = -1;
            for( i=0; i<mcls.Length; i++ ) {
                if( mcls[i] == mcl ) {
                    itemSlotID = 64+i;
                    break;
                }
            }
            if( itemSlotID == -1 ) {
                Debug.Log("Can't find MoveTowardsLocation");
                return -1;
            } else {
                Debug.Log("Found MoveTowardsLocation " + mcl + " in object " + parentObject.name + " at slot " + itemSlotID);
            }
            return itemSlotID;
        }

        public int GetCollider( Collider collider )
        {
            var parentObject = collider.transform.parent.gameObject;
            CNetId cni = parentObject.GetComponent<CNetId>();
            int i;
            var colliders = parentObject.GetComponentsInChildren<Collider>();
            int itemSlotID = -1;
            for( i=0; i<colliders.Length; i++ ) {
                if( colliders[i] == collider ) {
                    itemSlotID = 64+i;
                    break;
                }
            }
            if( itemSlotID == -1 ) {
                Debug.Log("Can't find collider");
                return -1;
            } else {
                Debug.Log("Found collider " + collider + " in object " + parentObject.name + " at slot " + itemSlotID);
            }
            return itemSlotID;
        }

        public uint GetIdent( GameObject obj, out int itemSlotID )
        {
            itemSlotID = -1;

            if (obj == null) {
                return 0;
            }

            // If we're just looking at a normal object, return its netid.
            CNetId cni = obj.GetComponent<CNetId>();
            if( cni != null ) {
                return cni.id;
            }

            // Try to get the ObjectIdentifier.
            var objectIdentifier = obj.GetComponent<ObjectIdentifier>();
            if (objectIdentifier != null) {
                return objectIdentifier.ID;
            }

            // The object may be an item.
            var inventory = obj.GetComponentInParent<InventoryBase>();
            if (inventory != null) {
                for (int i = 0; i < inventory.SlotCount; ++i) {
                    var item = inventory.GetActiveItem(i);
                    if (item == null) {
                        continue;
                    }
                    var visibleObject = item.ActivePerspectiveItem.GetVisibleObject();
                    if (obj == visibleObject) {
                        itemSlotID = item.SlotID;
                        return item.ItemIdentifier.ID;
                    }
                }

                var allItems = inventory.GetAllItems();
                for (int i = 0; i < allItems.Count; ++i) {
                    var visibleObject = allItems[i].ActivePerspectiveItem.GetVisibleObject();
                    if (obj == visibleObject) {
                        itemSlotID = allItems[i].SlotID;
                        return allItems[i].ItemIdentifier.ID;
                    }
                }
            }

            return 0;
        }
        public GameObject GetMoveTowards( GameObject parent, int slotid )
        {
            if (slotid < 64) {
                return null;
            }

            var mcls = parent.GetComponentsInChildren<MoveTowardsLocation>();
            if( slotid-64 < mcls.Length ) {
                return mcls[slotid-64].gameObject;
            }
            return null;
        }
        public GameObject GetIdObj( GameObject parent, uint id, int slotid )
        {
            if (id == 0) {
                return null;
            }

            GameObject go = null;
            if (slotid == -1 || slotid >= 64) {
                Dictionary<uint, ObjectIdentifier> idObjectIDMap;
                if (parent == null) {
                    idObjectIDMap = s_SceneIDMap;
                } else {
                    if (!s_IDObjectIDMap.TryGetValue(parent, out idObjectIDMap)) {
                        idObjectIDMap = new Dictionary<uint, ObjectIdentifier>();
                        s_IDObjectIDMap.Add(parent, idObjectIDMap);
                    }
                }
                ObjectIdentifier objectIdentifier = null;
                if (!idObjectIDMap.TryGetValue(id, out objectIdentifier)) {
                    //! Todo: cache all items that have cnetids
                    var objectIdentifiers = parent == null ? GameObject.FindObjectsOfType<ObjectIdentifier>() : parent.GetComponentsInChildren<ObjectIdentifier>(true);
                    if (objectIdentifiers != null) {
                        for (int i = 0; i < objectIdentifiers.Length; ++i) {
                            if (objectIdentifiers[i].ID == id) {
                                objectIdentifier = objectIdentifiers[i];
                                break;
                            }
                        }
                    }
                    idObjectIDMap.Add(id, objectIdentifier);
                }
                if (objectIdentifier == null) {
                    go = GetView(id);
                } else {
                    go = objectIdentifier.gameObject;
                }
                Debug.Log("Found object " + go);
                if( slotid >= 64 ) {
                    var colliders = objectIdentifier.gameObject.GetComponentsInChildren<Collider>();
                    slotid -= 64;
                    Debug.Log("Get collider " + slotid + ": " + colliders[slotid]);
                    go = colliders[slotid].gameObject;
                }
            } else {
                if (parent == null) {
                    Debug.LogError("Error: The parent must exist in order to retrieve the item ID.");
                    return null;
                }

                CNetCharacter netChar = parent.GetComponent<CNetCharacter>();

                var itemIdentifier = netChar.GetItemID(id);
                if (itemIdentifier == null) {
                    Debug.LogError($"Error: The ItemIdentifier with id {id} does not exist.");
                    return null;
                }

                var inventory = parent.GetComponent<InventoryBase>();
                if (inventory == null) {
                    Debug.LogError("Error: The parent does not contain an inventory.");
                    return null;
                }

                var item = inventory.GetItem(itemIdentifier, slotid);
                if (item == null) {
                    return null;
                }

                return item.ActivePerspectiveItem.GetVisibleObject();
            }

            return go;
        }

        public GameObject GetView( uint id )
        {
            if( serverUserObjects.ContainsKey(id) ) {
                return serverUserObjects[id];
            }
            if( clientObjects.ContainsKey(id) ) {
                return clientObjects[id].gameObject;
            }
            return null;
        }

        public GameObject GetObject( uint id )
        {
            if( !clientObjects.ContainsKey(id) ) {
                if( id != 0 )
                    Debug.Log("Object " + id + " not found");
                else
                    Debug.Log("Request for object 0");
                return null;
            }
            return clientObjects[id].gameObject;
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

            if( debugMode )
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
                        cni.id = v.objid;
                        if( debugMode )
                            Debug.Log("Set " + v.name + " id to " + cni.id);
                    } else {
                        Debug.Log("Object " + v.name + " does not have a CNetId component");
                        break;
                    }
                    clientObjects[v.objid] = mb;
                    rb = mb.GetComponent<Rigidbody>();
                    if( rb != null ) {
                        clientBodies[v.objid] = rb;
                    }
                    loadingObjects.Remove(v.name);
                    cni.local = authoritative;
                    cni.Register();
                    if( rb != null && authoritative ) { // send back information about the object
                        //Debug.Log("Sending object " + v.name + " to server");
                        SendObject( mb );
                    }
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
                Debug.Log("Object " + cni + " does not have an id");
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
            if( debugMode )
                Debug.Log("Sent object " + cni.id + ": " + rb.position.x + ", " + rb.position.y + ", " + rb.position.z + "  " + rb.rotation.x + ", " + rb.rotation.y + ", " + rb.rotation.z + ", " + rb.rotation.w);
        }

        public void SetObjectPositionRotation(NetStringReader stream)
        {
            uint objid;
            uint ts_short;
            ulong ts;
            float x,y,z;
            float r0,r1,r2;

            objid = stream.ReadUint();
            ts_short = stream.ReadUint();
            ts = S2CL(ts_short);

            x = stream.ReadFloat();
            y = stream.ReadFloat();
            z = stream.ReadFloat();
            r0 = stream.ReadFloat();
            r1 = stream.ReadFloat();
            r2 = stream.ReadFloat();

            if( debugMode )
                Debug.Log("SetObjectPositionRotation: " + objid + ": " + x + " " + y + " " + z + " " + r0 + " " + r1 + " " + r2);
            //Rigidbody rb = clientBodies[objid];
            //rb.MovePosition(new Vector3(x,y,z));
            //rb.MoveRotation(new Quaternion(r0,r1,r2,r3));
            CNetRigidbodyView trx = clientObjects[objid].GetComponent<CNetRigidbodyView>();
            trx.MoveTo( new Vector3(x,y,z) );
            trx.RotateTo( new Vector3(r0,r1,r2) );
        }

        public void BuildHealthMonitors( GameObject obj )
        {
            CNetAttributeMonitor attr = obj.AddComponent<CNetAttributeMonitor>();
            attr.enabled = true;
            CNetHealthMonitor health = obj.AddComponent<CNetHealthMonitor>();
            health.enabled = true;
            CNetRespawnerMonitor respawn = obj.AddComponent<CNetRespawnerMonitor>();
            respawn.enabled = true;

            if( debugMode )
                Debug.Log("Added health monitoring scripts");
        }

        
        public void SetupUser( CNetCharacter player ) {
            //Debug.Log("-local player found-");
            Player = player.gameObject;
            if( connected ) {
                //Debug.Log("[authenticating]");
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
            if( this.spawningNpcs.ContainsValue(obj) ) {
                this.waitingBuilds.Add(obj);
                return;
            }
            if( this.waitingSpawns.Contains(obj) ) {
                this.waitingSpawns.Remove(obj);
                this.BuildNPC(obj);
                return;
            }
            CNetId id = obj.GetComponent<CNetId>();
            CNetInfo info = obj.GetComponent<CNetInfo>();
            CNetCharacter ch = obj.GetComponent<CNetCharacter>();

            if( foundMainUser ) {
                Debug.Log("Found another player: " + id.id);
                id.local = false;
            } else {
                Debug.Log("Found main player: " + id.id);
                foundMainUser=true;
                id.local = true;
                SetupUser( ch );
            }
            ch.enabled = true;
            //obj.AddComponent<CNetCharacterLocomotionHandler>();

            var ctrans = obj.AddComponent<CNetTransform>();

            UnityInput ui = obj.GetComponent<UnityInput>();
            if( id.local ) {
                //ui.ForceInput = (UnityInput.ForceInputType)1;
                //mgr.AllowAxisInput = true;
                //mgr.AllowButtonInput = true;
                //mgr.Character = obj;
                //obj.SetActive(true);
            } else {
                if( ui != null ) {
                    Destroy(ui);
                    
                    UltimateCharacterLocomotionHandler[] handlers = obj.GetComponents<UltimateCharacterLocomotionHandler>();
                    int i;
                    for( i=0; i<handlers.Length; i++ ) {
                        Destroy( handlers[i] );
                    }
                    if( i > 0 )
                        Debug.Log("Removed " + i + " handlers");
                }
                //VirtualControlsManager mgr = obj.AddComponent<VirtualControlsManager>();
                /*
                ui.ForceInput = (UnityInput.ForceInputType)2;
                Opsive.Shared.Events.EventHandler.ExecuteEvent(obj, "OnEnableGameplayInput", false);
                */
                //mgr.AllowAxisInput = false;
                //mgr.AllowButtonInput = false;
                //mgr.Character = obj;
                //ctrans.RegisterControls(mgr);
                //LocalLookSource lls = obj.GetComponent<LocalLookSource>();
                //lls.enabled = true;
            }
            obj.AddComponent<CNetLookSource>();

            //obj.AddComponent<CNetMecanim>();
            //if( !id.local ) {
                //Debug.Log("Setup VCM");
                //CNetVirtualControlsManager manager = obj.AddComponent<CNetVirtualControlsManager>();
                //UnityInput ui = obj.GetComponent<UnityInput>();
                //manager.Register();
                //ui.RegisterVirtualControlsManager((VirtualControlsManager)manager);
                //obj.SetActive(true);
                //ui.ForceInput = (UnityInput.ForceInputType)2;
                
                //obj.SetActive(true);
            //}
        }
        public void BuildNPC( GameObject obj )
        {
            if( this.spawningNpcs.ContainsValue(obj) ) {
                this.waitingBuilds.Add(obj);
                Debug.Log("Defer building npc");
                return;
            }

            Debug.Log("Finish building NPC");
            CNetId id = obj.GetComponent<CNetId>();
            //id.Register();
            CNetInfo info = obj.GetComponent<CNetInfo>();

            //obj.AddComponent<CNetLookSource>();
            CNetCharacter ch = obj.GetComponent<CNetCharacter>();
            ch.enabled = true;
            
            var ctrans = obj.AddComponent<CNetTransform>();

            Debug.Log("Add NPC");
            var cnetNpc = obj.AddComponent<CNetNPC>();

            if( id.local ) {
                Debug.Log("Serialize random attributes");
                var avatar = obj.GetComponent<UMAAvatarBase>();
                var asset = ScriptableObject.CreateInstance<UMATextRecipe>();
                asset.Save(avatar.umaData.umaRecipe, avatar.context);
                NetStringBuilder sb = new NetStringBuilder();
                sb.AddUint( id.id );
                sb.AddLongString( asset.recipeString );
                SendMessage( SCommand.NPCRecipe, sb, 0, true );
            }

            //obj.AddComponent<CNetMecanim>();
            //Debug.Log("Setup LLS");
            //LocalLookSource lls = obj.GetComponent<LocalLookSource>();
            //lls.enabled = true;
        }

        public void NewUser(NetStringReader stream)
        {
            uint uid = stream.ReadUint();

            if( debugMode )
                Debug.Log("NewUser " + uid);

            Vector3 startPos = new Vector3();
            startPos.x = stream.ReadFloat();
            startPos.y = stream.ReadFloat();
            startPos.z = stream.ReadFloat();
            float r0 = stream.ReadFloat();
            float r1 = stream.ReadFloat();
            float r2 = stream.ReadFloat();
            //Quaternion startRot = Quaternion.Euler(r0, r1, r2);

            if( debugMode )
                Debug.Log("NewUser Create Avatar at " + startPos + " " + r0 + " " + r1 + " " + r2);
            
            GameObject avatar = this.CreateRemoteAvatar(startPos, r1);
            Rigidbody rb = avatar.GetComponent<Rigidbody>();
            rb.detectCollisions = false;

            serverUserObjects[uid] = avatar;
            CNetId cni = avatar.GetComponent<CNetId>();
            cni.local = false;
            cni.id = uid;
            cni.type = 2;
            cni.Register();

            rb.detectCollisions = true;

            //BuildPlayer(serverUserObjects[uid]);
            Debug.Log("NewUser Done, Building can continue");
        }

        private Dictionary<uint, GameObject> spawningNpcs = new Dictionary<uint, GameObject>();
        private List<GameObject> waitingSpawns = new List<GameObject>();
        private List<GameObject> waitingBuilds = new List<GameObject>();

        public void DoSpawn(Vector3 pos, float angle)
        {
            NetStringBuilder stream = new NetStringBuilder();

            uint spawnid;
            int i;

            for( i=0; i<spawningNpcs.Count; i++ ) {
                if( !spawningNpcs.ContainsKey((uint)i) ) {
                    break;
                }
            }
            spawnid = (uint)i;

            stream.AddByte((byte)1);
            stream.AddUint(spawnid);
            stream.AddVector3(pos);
            stream.AddFloat(0f);
            stream.AddFloat(0f);
            stream.AddFloat(angle);
            stream.AddVector3(new Vector3(1f,1f,1f));
            Debug.Log("SendSpawn " + spawnid);
            SendMessage( SCommand.Spawn, stream, 0 );
            var obj = this.CreateRandomAvatar(pos, angle);
            spawningNpcs[spawnid] = obj;
        }

        public void ResolveSpawn( NetStringReader stream )
        {
            uint spawnid = stream.ReadUint();
            uint uid = stream.ReadUint();
            Debug.Log("ResolveSpawn " + spawnid + ", " + uid);

            GameObject go = spawningNpcs[spawnid];
            CNetId cni = go.GetComponent<CNetId>();
            cni.id = uid;
            cni.local = authoritative;
            cni.type = 2;
            cni.Register();

            serverNpcs[uid] = go;            
            spawningNpcs.Remove(spawnid);

            // finish building the player components
            if( waitingBuilds.Contains(go) ) {
                waitingBuilds.Remove(go);
                Debug.Log("Finish waiting build " + go);
                BuildNPC(go);
            }
        }

        public void SpawnNpc(NetStringReader stream)
        {
            uint uid = stream.ReadUint();

            //if( debugMode )
                Debug.Log("SpawnNpc " + uid);

            Vector3 startPos = stream.ReadVector3();
            float r0 = stream.ReadFloat();
            float r1 = stream.ReadFloat();
            float r2 = stream.ReadFloat();
            Vector3 startScale = stream.ReadVector3();

            GameObject go;
            serverNpcs[uid] = go = this.CreateRemoteAvatar(startPos, r1);
            waitingSpawns.Add( go );

            CNetId cni = go.GetComponent<CNetId>();
            cni.id = uid;
            cni.local = authoritative;
            cni.type = 2;
            cni.Register();
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



        public void SendDynPacketTo( uint playerid, CNetFlag cmd, uint tgt, NetStringBuilder dataptr=null )
        {
            if( tgt == 0 || playerid == 0 ) {
                if( debugMode )
                    Debug.Log("Early packet(for " + playerid +") drop: " + cmd + " from " + tgt + "(" + (dataptr!=null?dataptr.used:0) + ")");
                return;
            }
            NetStringBuilder sb = new NetStringBuilder();
            uint icmd = (uint)cmd;

            sb.AddUint(icmd);
            sb.AddUint(playerid);
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
            if( debugMode )
                Debug.Log("SendDynPacketTo: player " + playerid + ", cmd " + cmd + " to tgt " + tgt + " (" + (dataptr!=null?dataptr.used:0) + ")");
            SendMessage(SCommand.DynPacketTo, sb);
        }

        public void SendDynPacket( CNetFlag cmd, uint tgt, byte[] data )
        {
            if( tgt == 0 ) {
                Debug.Log("Early packet drop: " + cmd + " from " + tgt + "(" + (data!=null?data.Length:0) + ")");
                return;
            }
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
            //Debug.Log("SendDynPacket: cmd " + cmd + " to tgt " + tgt + " at " + ts + " (" + ts_short + ")");
            SendMessage(SCommand.DynPacket, sb);
        }
        public void SendDynPacket( CNetFlag cmd, uint tgt, NetStringBuilder dataptr=null )
        {
            if( tgt == 0 ) {
                Debug.Log("Early packet drop: " + cmd + " from " + tgt + "(" + (dataptr!=null?dataptr.used:0) + ")");
                return;
            }
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
            //Debug.Log("SendDynPacket: cmd " + cmd + " to tgt " + tgt + " at " + ts + " (" + ts_short + ")");
            SendMessage(SCommand.DynPacket, sb);
        }
        public ulong S2CL( uint ts_short )
        {
            ulong this_update = (ulong)( (long)last_game_time + (long)ts_short + clocksync );

            TimeSpan ts = DateTime.Now - DateTime.UnixEpoch;
            ulong now = (ulong)ts.TotalMilliseconds;

            long diff = Math.Abs( (long)now - (long)this_update );
            //Debug.Log("S2CL: " + ts_short + " -> " + now + " - " + this_update + " (" + diff + ")");
            this.s2cl_times.Add( (int)diff );
            if( this.s2cl_times.Count > 10 ) {
                this.s2cl_times.RemoveAt(0);
            }

            return this_update;
        }
        public void RecvDynPacket( NetStringReader stream )
        {
            uint cmd, tgt, ts_short;
            ulong ts;
            byte[] detail;

            cmd = stream.ReadUint();
            tgt = stream.ReadUint();
            ts_short = stream.ReadUint();
            ts = S2CL(ts_short);

            detail = stream.ReadShortBytes();
            if( detail == null ) {
                //Debug.Log("RecvDynPacket: no detail for cmd " + (CNetFlag)cmd + " to tgt " + tgt);
            } else {
                //Debug.Log("RecvDynPacket: cmd " + (CNetFlag)cmd + " to tgt " + tgt + " at " + ts + " (" + ts_short + ")");
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
        
        public void SendPacketTo( uint playerid, CNetFlag cmd, uint tgt, NetStringBuilder dataptr=null )
        {
            if( tgt == 0 || playerid == 0 ) {
                if( debugMode )
                    Debug.Log("Early packet(for " + playerid +") drop: " + cmd + " from " + tgt + "(" + (dataptr!=null?dataptr.used:0) + ")");
                return;
            }
            uint icmd = (uint)cmd;
            NetStringBuilder sb = new NetStringBuilder();

            sb.AddUint(icmd);
            sb.AddUint(playerid);
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
            SendMessage(SCommand.PacketTo, sb, 0);
        }
        public void SendPacket( CNetFlag cmd, uint tgt, NetStringBuilder dataptr=null, bool instantSend=false )
        {
            if( tgt == 0 ) {
                if( debugMode )
                    Debug.Log("Early packet drop: " + cmd + " from " + tgt + "(" + (dataptr!=null?dataptr.used:0) + ")");
                return;
            }
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
            //Debug.Log("SendPacket " + cmd + " for " + tgt + " with " + sb.used + " bytes (" + (dataptr!=null?dataptr.used:0) + " payload), " + (instantSend?"instant":"delayed"));
            /*
            byte b;
            int i;
            sb.Reduce();
            for( i=0; i<sb.used; i++ ) {
                b = sb.ptr[i];
                Debug.Log("Packet: " + b);
            }
            */
            if( !instantSend ) {
                Debug.Log("Packet not instant " + cmd);
            }
            SendMessage(SCommand.Packet, sb, instantSend?0:code);
        }
        public void SendPacket( CNetFlag cmd, uint tgt, byte[] data, bool instantSend=false )
        {
            if( tgt == 0 ) {
                if( debugMode )
                    Debug.Log("Early packet drop: " + cmd + " from " + tgt + "(" + (data!=null?data.Length:0) + ")");
                return;
            }
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
            ts = S2CL(ts_short);

            NetStringReader param;
            PacketData p;
            if( packetSizes.ContainsKey(cmd) && packetSizes[cmd] != 0 ) {
                if( packetSizes[cmd] != stream.data.Length - stream.offset ) {
                    Debug.LogError("Wrong size " + (stream.data.Length-stream.offset) + " for packet " + (CNetFlag)cmd + ": expected " + packetSizes[cmd]);
                    return;
                }
                detail = stream.ReadFixedBytes(packetSizes[cmd]);
            } else {
                detail = stream.ReadFixedBytes( stream.data.Length - stream.offset );
            }
            param = new NetStringReader(detail);
            //Debug.Log("Recv " + (CNetFlag)cmd + ": " + tgt + " param size " + param.data.Length);

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



        public void Update()
        {
            if( !connected ) {
                return;
            }

            Process();

            TimeSpan ts = DateTime.Now - DateTime.UnixEpoch;
            last_local_time = (ulong)ts.TotalMilliseconds;

            if( record_bps && last_local_time-last_in_time >= 1000 ) {
                last_in_time = last_local_time;
                lock( _inbpsLock ) {
                    if( in_bps_measure == 0 ) {
                        in_bytes.Clear();
                    } else {
                        if( in_bytes.Count > 4 ) {
                            in_bytes.RemoveAt(0);
                        }
                        in_bytes.Add( in_bps_measure );
                        in_bps_measure = 0;
                    }
                }
            }
            if( record_bps && last_local_time-last_out_time >= 1000 ) {
                last_out_time = last_local_time;
                lock( _outbpsLock ) {
                    if( out_bps_measure == 0 ) {
                        out_bytes.Clear();
                    } else {
                        if( out_bytes.Count > 4 ) { // keep an average of 5 seconds
                            out_bytes.RemoveAt(0);
                        }
                        out_bytes.Add( out_bps_measure );
                        out_bps_measure = 0;
                    }
                }
            }

            if( last_local_time-last_netupdate >= 1000f/updateRate ) {
                last_netupdate = last_local_time;

                if( connected && registered ) {
                    foreach( ICNetUpdate item in netObjects ) {
                        //! Todo: check if item is still valid.
                        item.NetUpdate();
                    }
                }
            }

            if( record_rtt && last_local_time-last_rtt_packet >= 333 ) {
                last_rtt_packet = last_local_time;
                NetStringBuilder sb = new NetStringBuilder();
                sb.AddULongLong(last_local_time);
                SendMessage(SCommand.EchoRTT, sb);
            }

            if( last_local_time-last_record_time >= 1000 ) {
                last_record_time = last_local_time;
                NetStringBuilder sb = new NetStringBuilder();
                if( debugMode )
                    Debug.Log("Sync: " + last_local_time + ", " + sizeof(ulong));
                sb.AddULongLong(last_local_time);
                SendMessage(SCommand.ClockSync, sb);
            }

            flushDelayQueue();
            if( sendBuffers.Count > 0 ) {
                sendPackets();
            }
        }

        public void SendMessage( SCommand cmd, NetStringBuilder sb, long code=0, bool noLimit=false )
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
                lock( _sendQLock ) {
                    send(msg, data != null ? data.Length : 0, noLimit);

                    if( data != null )
                        send(data, 0, noLimit);
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
                lock( _sendQLock ) {
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

        public long last_send_time=0, last_dsend_time=0, last_psend_time=0;
        public int max_out_bps=4096;
        public int second_bytes=0;
        private Queue<byte[]> delayQ = new Queue<byte[]>();

        public void send(byte[] data, int additionalData=0, bool noLimit=false)
        {
            TimeSpan ts = DateTime.Now - DateTime.UnixEpoch;
            long cur_send_time = (long)ts.TotalMilliseconds;
            float elapsed = (cur_send_time-last_send_time)/1000.0f;
            if( elapsed > 1.0f ) {
                elapsed=1.0f;
                last_send_time = cur_send_time;
                second_bytes = 0;
            }
            if( delayQ.Count > 0 || ( data.Length == 3 && !noLimit && second_bytes+additionalData+data.Length > max_out_bps*elapsed ) ) {
                //Debug.Log("overflow to delayQ");
                delayQ.Enqueue(data);
                return;
            }
            second_bytes += data.Length;
            sendQ.Enqueue(data);
        }

        public void flushDelayQueue()
        {
            if( delayQ.Count <= 0 ) return;
            //Debug.Log("flushDelayQueue");

            TimeSpan ts = DateTime.Now - DateTime.UnixEpoch;
            long cur_send_time = (long)ts.TotalMilliseconds;
            float elapsed = (cur_send_time-last_dsend_time)/1000.0f;

            if( elapsed > 1.0f ) {
                elapsed = 1.0f;
                last_dsend_time = cur_send_time;
                second_bytes = 0;
            }

            if( second_bytes > elapsed*max_out_bps ) {
                return;
            }

            lock (_sendQLock) {
                while( delayQ.Count > 0 ) {
                    byte[] data = delayQ.Dequeue();
                    second_bytes += data.Length;
                        sendQ.Enqueue(data);
                    //Debug.Log("flushQueue: " + data.Length + " bytes");
                    if( data.Length != 3 && second_bytes > elapsed*max_out_bps ) { // do not end if we just sent a header.
                        //Debug.Log("end on flushQueue");
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
            float elapsed = (cur_time-last_psend_time)/1000.0f;
            if( elapsed > 1.0f ) {
                elapsed = 1.0f;
                last_psend_time = cur_time;
                second_bytes = 0;
            }
            if( second_bytes >= max_out_bps*elapsed ) {
                if( debugMode )
                    Debug.Log("sendPackets delayed: " + elapsed + " " + second_bytes);
                return;
            }
            if( sendBuffers.Count <= 0 )
                return;
            
            //Debug.Log("sendPackets: " + sendBuffers.Count);
            _sendQSig.Reset();
            lock( _sendQLock ) {
                List<long> keys = new List<long>();
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
                foreach( var k in keys ) {
                    sendHeaders.Remove(k);
                    sendBuffers.Remove(k);
                }
            }
            _sendQSig.Set();
        }

        public void sendBuffer(long code, byte[] head, byte[] data) {
            //Debug.Log("sendBuffer(" + head[0] + " " + head[1] + " " + head[2] + "): " + data.Length + " bytes ");
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
