using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System;
using System.IO;

namespace CNet
{
    public class NetFiles
    {
        struct FileData {
            public string filename;
            public long filesize;
            public long filetime;
            public string contents;
        };

        private Dictionary<String, FileData> localAssets = new Dictionary<String, FileData>(); // file data for local files
        private bool readingFiles = false;
        private FileData readingFile; // file currently being read from server
        private BinaryWriter fileWriter;
        private Queue<FileData> fileQ = new Queue<FileData>(); // files to be read

        private NetSocket net;

        public NetFiles(NetSocket ctrl)
        {
            net = ctrl;
            readingFiles = false;
            ReadLocalFiles();
        }

        public void ReadLocalFiles()
        {
            // read directory
            string[] files = Directory.GetFiles("ServerFiles");
            foreach( string file in files ) {
                string[] paths = file.Split('\\');
                FileInfo fx = new System.IO.FileInfo(file);

                FileData fi = new FileData();
                fi.filename = paths[paths.Length-1];
                fi.filesize = fx.Length;   
                TimeSpan ts = fx.LastWriteTime - DateTime.UnixEpoch;
                fi.filetime = (long)ts.TotalMilliseconds;
                fi.contents = null;

                localAssets[fi.filename] = fi;
                Debug.Log("Local asset found: " + fi.filename);
            }
        }


        public void GotEndOfFileList(NetStringReader stream)
        {
            Debug.Log("EOF list");
            if( !readingFiles ) {
                net.GetObjects();
            }
        }

        public void GotNextFile(NetStringReader stream)
        {
            if( fileQ.Count > 0 ) {
                if( fileWriter != null ) {
                    fileWriter.Close();
                    fileWriter = null;
                }
                readingFile = fileQ.Dequeue();
                Debug.Log("Next: file " + readingFile.filename);
                // open the streamwriter
                if( File.Exists("ServerFiles\\" + readingFile.filename) )
                    File.Delete("ServerFiles\\" + readingFile.filename);
                fileWriter = new BinaryWriter(File.Create("ServerFiles\\" + readingFile.filename));
            } else {
                if( fileWriter != null ) {
                    fileWriter.Close();
                    fileWriter = null;
                }
                readingFiles = false;
                fileWriter = null;
                Debug.Log("End of files");
                net.GetObjects();
            }
        }

        public void GotFileData(NetStringReader stream)
        {
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

            filename = stream.ReadString();
            filesize = stream.ReadLongLong();
            filetime = stream.ReadLongLong();
            //Debug.Log("FileInfo " + filename + ": size=" + filesize + ", time=" + filetime);

            if( localAssets.ContainsKey(filename) ) {
                FileData fi = localAssets[filename];
                if( fi.filesize != filesize ) {
                    // file has changed, request it
                    Debug.Log("File " + filename + " has changed from filetime " + fi.filetime + ", requesting");
                    buf = new byte[filename.Length];
                    System.Text.Encoding.ASCII.GetBytes(filename, 0, filename.Length, buf, 0);
                    if( !readingFiles ) {
                        readingFile = fi;
                        File.Delete("ServerFiles\\" + readingFile.filename);
                        fileWriter = new BinaryWriter(File.Create("ServerFiles\\" + readingFile.filename));
                        readingFiles = true;
                    } else {
                        fileQ.Enqueue(fi);
                    }
                    net.SendMessage2( SCommand.GetFile, buf );
                    fi.contents = null;
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
                    fileWriter = new BinaryWriter(File.Create("ServerFiles\\" + readingFile.filename));
                    readingFiles = true;
                } else {
                    fileQ.Enqueue(fi);
                }
                net.SendMessage2( SCommand.GetFile, buf );
            }
        }
    }
}