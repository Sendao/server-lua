using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;

namespace CNet {
    public class NetStringBuilder
    {
        public byte[] ptr;
        public int alloced;
        public int used;

        public NetStringBuilder(int size=32) {
            alloced = size;
            used = 0;
            ptr = new byte[size];
        }

        public void AllocMore() {
            int old = alloced;
            alloced *= 2;

            byte[] newmem = new byte[alloced];
            System.Buffer.BlockCopy(ptr, 0, newmem, 0, old);
            ptr = newmem;
        }

        public void AddVector3(Vector3 value) {
            AddFloat(value.x);
            AddFloat(value.y);
            AddFloat(value.z);
        }

        public void AddVector2(Vector2 value) {
            AddFloat(value.x);
            AddFloat(value.y);
        }

        public void AddShortVector3(Vector3 value, float max=1000f) {
            AddShortFloat(value.x, max);
            AddShortFloat(value.y, max);
            AddShortFloat(value.z, max);
        }

        public void AddShortVector2(Vector2 value, float max=1000f) {
            AddShortFloat(value.x, max);
            AddShortFloat(value.y, max);
        }

        public void AddLongLong(long value) {
            while( used+8 > alloced )
                AllocMore();
            
            ptr[used+0] = (byte)((value>>56) & 0xff);
            ptr[used+1] = (byte)((value>>48) & 0xff);
            ptr[used+2] = (byte)((value>>40) & 0xff);
            ptr[used+3] = (byte)((value>>32) & 0xff);
            ptr[used+4] = (byte)((value>>24) & 0xff);
            ptr[used+5] = (byte)((value>>16) & 0xff);
            ptr[used+6] = (byte)((value>>8) & 0xff);
            ptr[used+7] = (byte)(value&0xFF);
            used += 8;
        }
        public void AddULongLong(ulong value) {
            while( used+8 > alloced )
                AllocMore();
            
            ptr[used+0] = (byte)((value>>56) & 0xff);
            ptr[used+1] = (byte)((value>>48) & 0xff);
            ptr[used+2] = (byte)((value>>40) & 0xff);
            ptr[used+3] = (byte)((value>>32) & 0xff);
            ptr[used+4] = (byte)((value>>24) & 0xff);
            ptr[used+5] = (byte)((value>>16) & 0xff);
            ptr[used+6] = (byte)((value>>8) & 0xff);
            ptr[used+7] = (byte)(value&0xFF);
            used += 8;
        }

        public void AddLong(long value) {
            while( used+4 > alloced )
                AllocMore();
            ptr[used+0] = (byte)((value>>24) & 0xff);
            ptr[used+1] = (byte)((value>>16) & 0xff);
            ptr[used+2] = (byte)((value>>8) & 0xff);
            ptr[used+3] = (byte)(value&0xFF);
            used += 4;
        }
        public void AddUlong(ulong value) {
            while( used+4 > alloced )
                AllocMore();
            ptr[used+0] = (byte)((value>>24) & 0xff);
            ptr[used+1] = (byte)((value>>16) & 0xff);
            ptr[used+2] = (byte)((value>>8) & 0xff);
            ptr[used+3] = (byte)(value&0xFF);
            used += 4;
        }
        public void AddInt(int value) {
            while( used+2 > alloced )
                AllocMore();
            ptr[used+0] = (byte)((value>>8) & 0xff);
            ptr[used+1] = (byte)(value&0xFF);
            used += 2;
        }
        public void AddUint(uint value) {
            while( used+2 > alloced )
                AllocMore();
            ptr[used+0] = (byte)((value>>8) & 0xff);
            ptr[used+1] = (byte)(value&0xFF);
            used += 2;
        }
        public void AddBool(bool value) {
            while( used+1 > alloced )
                AllocMore();
            ptr[used] = (byte)(value?1:0);
            used += 1;
        }
        public void AddByte(byte value) {
            while( used+1 > alloced )
                AllocMore();
            ptr[used] = value;
            used += 1;
        }
        public void AddFloat(float value) {
            while( used+4 > alloced ) {
                AllocMore();
            }
            byte[] x = System.BitConverter.GetBytes(value);
            x.CopyTo(ptr, used);
            used += 4;
        }
        public void AddShort(float value, float max=1000f) {
            AddShortFloat(value,max);
        }
        public void AddShortFloat(float value, float max=1000f) {
            while( used+2 > alloced ) {
                AllocMore();
            }
            if( value > max || value < -max ) {
                Debug.LogError("NetStringBuilder: value out of range: "+value);
            }
            short x = (short)(value*(short.MaxValue/max));
            ptr[used+0] = (byte)((x>>8) & 0xff);
            ptr[used+1] = (byte)(x&0xFF);
            used += 2;
        }

        public void AddString(string str) {
            int len = str.Length;
            while( used+2+len > alloced )
                AllocMore();
            ptr[used+0] = (byte)((len>>8) & 0xff);
            ptr[used+1] = (byte)(len&0xFF);
            System.Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(str), 0, ptr, used+2, len);
            used += len+2;
        }

        public void AddBytes(byte[] data) {
            int len = data.Length;
            while( used+len > alloced )
                AllocMore();
            System.Buffer.BlockCopy(data, 0, ptr, used, len);
            used += len;
        }

        public void AddShortBytes(byte[] data) {
            int len = data.Length;
            while( used+len+2 > alloced )
                AllocMore();
            ptr[used+0] = (byte)((len>>8) & 0xff);
            ptr[used+1] = (byte)(len&0xFF);
            System.Buffer.BlockCopy(data, 0, ptr, used+2, len);
            used += len+2;
        }

        public void Reduce() {
            byte[] newmem = new byte[used];
            System.Buffer.BlockCopy(ptr, 0, newmem, 0, used);
            ptr = newmem;
            alloced = used;
        }
    }
}
