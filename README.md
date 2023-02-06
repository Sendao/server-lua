# server-lua

Contains no actual LUA.


# Next up:
- rigid body movement

# Completed:

- tcp sockets
- stream compression
- file listing
- basic lua interface
- file i/o
- basic packeting
- authoritative client

# MinGW Instructions:

- clone to c:\serv
- install msys
- pacman install mingw
- start a mingw shell
- pacman -R cmake
- pacman -S mingw-w64-x86_64-cmake
- start a new mingw shell
- build sol2 from source. use a build directory in the sol2 root dir. Use the following from there:
- cmake -G"MSYS Makefiles" ..
- make (should download and install lua)
 (this builds target liblua-5.4.4 in build/x64/lib and build/x64/bin)
- locate liblua-5.4.4.dll.a and liblua-5.4.4.dll and copy to /c/serv/
- go to c/serv and create a 'debug' directory'
- copy the .dll file to the debug dir
- build the project in 'debug' directory using cmake -G"MSYS Makefiles" ..
- locate libwinpthread-1.dll in /c/msys64/mingw64/bin and copy to the debug dir
- you're set to run server.exe




