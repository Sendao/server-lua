using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;

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

    public void AddLongLong(long value) {
        if( used+8 > alloced )
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
        if( used+4 > alloced )
            AllocMore();
        ptr[used+0] = (byte)((value>>24) & 0xff);
        ptr[used+1] = (byte)((value>>16) & 0xff);
        ptr[used+2] = (byte)((value>>8) & 0xff);
        ptr[used+3] = (byte)(value&0xFF);
        used += 4;
    }
    public void AddInt(int value) {
        if( used+2 > alloced )
            AllocMore();
        ptr[used+0] = (byte)((value>>8) & 0xff);
        ptr[used+1] = (byte)(value&0xFF);
        used += 2;
    }
    public void AddByte(byte value) {
        if( used+1 > alloced )
            AllocMore();
        ptr[used] = value;
        used += 1;
    }
    public void AddFloat(float value) {
        if( used+4 > alloced ) {
            AllocMore();
        }
        byte[] x = System.BitConverter.GetBytes(value);
        x.CopyTo(ptr, used);
        used += 4;
    }

    public void AddString(string str) {
        int len = str.Length;
        if( used+2+len > alloced )
            AllocMore();
        ptr[used+0] = (byte)((len>>8) & 0xff);
        ptr[used+1] = (byte)(len&0xFF);
        System.Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(str), 0, ptr, used+2, len);
        used += len+2;
    }
}
