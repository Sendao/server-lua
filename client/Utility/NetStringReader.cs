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
		}
		public byte ReadByte() {
			if( offset+sizeof(byte) > data.Length ) {
				Debug.Log("ReadByte: out of range");
				return 0;
			}
			byte res;
			res = data[offset];
			offset += 1;
			return res;
		}
		public bool ReadBool() {
			if( offset+sizeof(byte) > data.Length ) {
				Debug.Log("ReadByte: out of range");
				return false;
			}
			bool res;
			res = (bool)( data[offset] == 1 ? true : false );
			offset += 1;
			return res;
		}
		public int ReadInt() {
			if( offset+2 > data.Length ) {
				Debug.Log("ReadInt: out of range");
				return 0;
			}
			int res;
			res = (int)data[offset+0] << 8 | (int)(data[offset+1] & 0xFF);
			offset += 2;
			return res;
		}
		public uint ReadUint() {
			if( offset+2 > data.Length ) {
				Debug.Log("ReadInt: out of range");
				return 0;
			}
			uint res;
			res = (uint)data[offset+0] << 8 | (uint)(data[offset+1] & 0xFF);
			offset += 2;
			return res;
		}
		public long ReadLong() {
			if( offset+4 > data.Length ) {
				Debug.Log("ReadLong: out of range");
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
				Debug.Log("ReadLong: out of range");
				return 0;
			}
			long res;
			//res = System.BitConverter.ToInt64(data, offset);
			res =   (long)data[offset+0] << 56 | (long)data[offset+1] << 48 | (long)data[offset+2] << 40 | (long)data[offset+3] << 32 |
					(long)data[offset+4] << 24 | (long)data[offset+5] << 16 | (long)data[offset+6] << 8 | (long)(data[offset+7] & 0xFF);
			offset += 8;
			return res;
		}
		public float ReadFloat() {
			if( offset+4 > data.Length ) {
				Debug.Log("ReadFloat: out of range");
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
				Debug.Log("ReadShortFloat: out of range");
				return 0;
			}
			short mid;
			mid = (short)(data[offset+0] << 8 | (data[offset+1] & 0xFF));
			float res = (float)mid / (short.MaxValue / max);
			offset += 2;

			return res;
		}
		public string ReadString() {
			int len = ReadInt();
			int p;
			string s;
			
			if( offset+len > data.Length ) {
				Debug.Log("ReadString: out of range");
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
				Debug.Log("ReadFixedBytes: out of range");
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
				Debug.Log("ReadShortBytes: out of range");
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
