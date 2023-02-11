# server-lua

Contains no actual LUA.

# Next up:
- RPCs
- kinematic controller movement

# In progress:
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
- delete CMakeLists.txt and copy CMakeWin.txt to CMakeLists.txt
- install msys from https://www.msys2.org/
- start a UCRT64 shell.
- pacman -S mingw-w64-x86_64-cmake mingw-w64-x86_64-c++ mingw-w64-x86_64-make
- optionally, pacman -S mingw-w64-x86_64-gdb
- switch to the MINGW64 shell.
- download sol2 to c:\serv\sol
- go to c:\serv\sol and create a 'build' directory.
- from the build directory:
- `cmake ..`
- `mingw32-make` (should download and install lua)
 (this builds target liblua-5.4.4 in build/x64/lib and build/x64/bin)
- locate liblua-5.4.4.dll.a and liblua-5.4.4.dll and copy to c:\serv
- go to c/serv and create a 'debug' directory'
- copy the .dll file to the debug dir
- build the project in 'debug' directory using `cmake ..`
- `mingw32-make` (will build server.exe)
- you're set to run server.exe

