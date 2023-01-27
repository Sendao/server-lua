del CMakeLists.txt
copy CMakeDebug.txt CMakeLists.txt
mkdir debug
cd debug
cmake -DCMAKE_BUILD_TYPE=debug .. -G"MinGW Makefiles"
