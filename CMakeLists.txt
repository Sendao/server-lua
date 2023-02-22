cmake_minimum_required (VERSION 3.15)
project(whirlwind)

export(PACKAGE whirlwind)

file(GLOB main_code
	"src/*.cpp"
)

set(CMAKE_C_FLAGS "${CMAKE_C_FLAGS} -ggdb")
set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -ggdb")

link_directories(c:\\serv\\)
#set(CMAKE_EXE_LINKER_FLAGS " -static")
add_executable(server ${main_code})
target_include_directories(server PRIVATE c:\\serv\\sol\\include c:\\serv\\sol\\build\\vendor\\lua-5.4.4\\src)
#target_link_libraries(server lua-5.4 z -static-libgcc -static-libstdc++)
target_link_libraries(server lua-5.4.4 ws2_32 winmm pthread z -static-libgcc -static-libstdc++)
