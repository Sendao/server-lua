using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System.Runtime.InteropServices;
using UnityEngine;

namespace CNet
{
	public class NetStringReader
	{
		public byte[] data;
		public int offset;

		public NetStringReader(byte[] ptr) {
			data = ptr;
			offset = 0;
		}
		public object[] ReadObjects() {
			if( offset == data.Length )
				return null;
			List<object> res = new List<object>();
			while( offset < data.Length ) {
				byte type = ReadByte();
				if( type == 0 ) {
					res.Add( ReadByte() );
				} else if( type == 1 ) {
					res.Add( ReadBool() );
				} else if( type == 2 ) {
					res.Add( ReadInt() );
				} else if( type == 3 ) {
					res.Add( ReadUint() );
				} else if( type == 4 ) {
					res.Add( ReadLong() );
				} else if( type == 5 ) {
					res.Add( ReadULongLong() );
				} else if( type == 6 ) {
					res.Add( ReadFloat() );
				} else if( type == 7 ) {
					res.Add( ReadDouble() );
				} else if( type == 8 ) {
					res.Add( ReadVector3() );
				} else if( type == 9 ) {
					res.Add( ReadVector2() );
				} else if( type == 10 ) {
					res.Add( ReadString() );
				} else {
					Debug.LogError("ReadObjects: unknown type");
					break;
				}
			}
			return res.ToArray();
		}
		public byte ReadByte() {
			if( offset+sizeof(byte) > data.Length ) {
				Debug.LogError("ReadByte: out of range");
				return 0;
			}
			byte res;
			res = data[offset];
			offset += 1;
			return res;
		}
		public bool ReadBool() {
			if( offset+sizeof(byte) > data.Length ) {
				Debug.LogError("ReadByte: out of range");
				return false;
			}
			bool res;
			res = (bool)( data[offset] == 1 ? true : false );
			offset += 1;
			return res;
		}
		public int ReadInt() {
			if( offset+2 > data.Length ) {
				Debug.LogError("ReadInt: out of range");
				return 0;
			}
			int res;
			res = (int)data[offset+0] << 8 | (int)(data[offset+1] & 0xFF);
			offset += 2;
			return res;
		}
		public uint ReadUint() {
			if( offset+2 > data.Length ) {
				Debug.LogError("ReadInt: out of range");
				return 0;
			}
			uint res;
			res = (uint)data[offset+0] << 8 | (uint)(data[offset+1] & 0xFF);
			offset += 2;
			return res;
		}
		public long ReadLong() {
			if( offset+4 > data.Length ) {
				Debug.LogError("ReadLong: out of range");
				return 0;
			}
			long res;
			//res = System.BitConverter.ToInt64(data, offset);
			res = (long)data[offset+0] << 24 | (long)data[offset+1] << 16 | (long)data[offset+2] << 8 | (long)(data[offset+3] & 0xFF);
			offset += 4;
			return res;
		}
		public long ReadLongLong() {
			if( offset+8 > data.Length ) {
				Debug.LogError("ReadLong: out of range");
				return 0;
			}
			long res;
			//res = System.BitConverter.ToInt64(data, offset);
			res =   (long)data[offset+0] << 56 | (long)data[offset+1] << 48 | (long)data[offset+2] << 40 | (long)data[offset+3] << 32 |
					(long)data[offset+4] << 24 | (long)data[offset+5] << 16 | (long)data[offset+6] << 8 | (long)(data[offset+7] & 0xFF);
			offset += 8;
			return res;
		}
		public ulong ReadULongLong() {
			if( offset+8 > data.Length ) {
				Debug.LogError("ReadLong: out of range");
				return 0;
			}
			ulong res;
			//res = System.BitConverter.ToInt64(data, offset);
			res =   (ulong)data[offset+0] << 56 | (ulong)data[offset+1] << 48 | (ulong)data[offset+2] << 40 | (ulong)data[offset+3] << 32 |
					(uint)data[offset+4] << 24 | (uint)data[offset+5] << 16 | (uint)data[offset+6] << 8 | (uint)(data[offset+7] & 0xFF);
			offset += 8;
			return res;
		}
		public double ReadDouble() {
			if( offset+8 > data.Length ) {
				Debug.LogError("ReadFloat: out of range");
				return 0;
			}
			double res;
			res = System.BitConverter.ToDouble(data, offset);
			offset += 8;

			return res;
		}
		public float ReadFloat() {
			if( offset+4 > data.Length ) {
				Debug.LogError("ReadFloat: out of range");
				return 0;
			}
			float res;
			res = System.BitConverter.ToSingle(data, offset);
			offset += 4;

			return res;
		}
		public float ReadShort(float max=1000f) {
			return ReadShortFloat(max);
		}
		public float ReadShortFloat(float max=1000f) {
			if( offset+2 > data.Length ) {
				Debug.LogError("ReadShortFloat: out of range");
				return 0;
			}
			short mid;
			mid = (short)(data[offset+0] << 8 | (data[offset+1] & 0xFF));
			float res = (float)mid / ((float)short.MaxValue / max);

			offset += 2;

			return res;
		}
		public string ReadString() {
			int len = ReadInt();
			int p;
			string s;
			
			if( offset+len > data.Length ) {
				Debug.LogError("ReadString: out of range");
				return "";
			}
			s = "";
			for(p=0; p<len; p++) {
				s += (char)data[offset+p];
			}
			offset += len;

			return s;
		}
		public byte[] ReadFixedBytes(int len) {
			byte[] res;
			
			if( offset+len > data.Length ) {
				Debug.LogError("ReadFixedBytes: out of range offset="+offset+", len="+len+", data.Length="+data.Length);
				return null;
			}
			res = new byte[len];
			System.Buffer.BlockCopy(data, offset, res, 0, len);
			offset += len;

			return res;
		}
		public byte[] ReadShortBytes() {
			uint len = ReadUint();
			byte[] res;
			
			if( offset+len > data.Length ) {
				Debug.LogError("ReadShortBytes: out of range");
				return null;
			}
			if( len == 0 ) {
				return new byte[0];
			}
			res = new byte[len];
			System.Buffer.BlockCopy(data, offset, res, 0, (int)len);
			offset += (int)len;

			return res;
		}

		public Vector3 ReadVector3() {
			Vector3 f = new Vector3();
			f.x = ReadFloat();
			f.y = ReadFloat();
			f.z = ReadFloat();
			return f;
		}
		public Vector2 ReadVector2() {
			Vector2 f = new Vector2();
			f.x = ReadFloat();
			f.y = ReadFloat();
			return f;
		}

		public Vector3 ReadShortVector3(float max=1000f) {
			Vector3 f = new Vector3();
			f.x = ReadShortFloat(max);
			f.y = ReadShortFloat(max);
			f.z = ReadShortFloat(max);
			return f;
		}
		public Vector2 ReadShortVector2(float max=1000f) {
			Vector2 f = new Vector2();
			f.x = ReadShortFloat(max);
			f.y = ReadShortFloat(max);
			return f;
		}
	}
}
