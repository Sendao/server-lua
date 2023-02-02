using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System.Runtime.InteropServices;
using UnityEngine;

public class NetStringBuilder
{
    unsafe public byte *memory;
    unsafe byte *ptr;
    public int alloced;
    public int used;

    public NetStringBuilder(int size) {
        alloced = size;
        used = 0;
        unsafe
        {
            memory = (byte*)UnsafeUtility.Malloc(size, 4, Allocator.Persistent);
            ptr = memory;
        }
    }

    public void Dispose() {
        unsafe
        {
            UnsafeUtility.Free(memory, Allocator.Persistent);
        }
    }

    public void AllocMore() {
        int old = alloced;
        alloced *= 2;
        unsafe
        {
            byte *newmem = (byte*)UnsafeUtility.Malloc(alloced, 4, Allocator.Persistent);
            UnsafeUtility.MemCpy(newmem, memory, old);
            UnsafeUtility.Free(memory, Allocator.Persistent);
            memory = newmem;
            ptr = memory + used;
        }
    }

    public void AddLong(long value) {
        if( used+sizeof(long) > alloced ) {
            AllocMore();
        }
        unsafe
        {
            *(long*)ptr = value;
            ptr += sizeof(long);
        }
        used += sizeof(long);
    }
    public void AddInt(int value) {
        if( used+sizeof(int) > alloced ) {
            AllocMore();
        }
        unsafe
        {
            *ptr = (byte)((value>>8) & 0xff);
            ptr++;
            *ptr = (byte)(value&0xFF);
            ptr++;
        }
        used += 2;
    }
    public void AddFloat(float value) {
        if( used+sizeof(float) > alloced ) {
            AllocMore();
        }
        unsafe
        {
            *(float*)ptr = value;
            ptr += sizeof(float);
        }
        used += sizeof(float);
    }

    public void AddString(string str) {
        int len = str.Length;
        if( used+sizeof(int)+len > alloced ) {
            AllocMore();
        }
        unsafe
        {
            *ptr = (byte)((len>>8) & 0xff);
            *(ptr+1) = (byte)(len&0xFF);
            ptr += 2;
            fixed (char* p = str)
            {
                UnsafeUtility.MemCpy(ptr, p, len * 2);
                ptr += len * 2;
            }
        }
        used += len + sizeof(int);
    }
}
