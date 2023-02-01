cmake_minimum_required (VERSION 3.15)
project(whirlwind)

export(PACKAGE whirlwind)

file(GLOB main_code
	"src/*.cpp"
)

include_directories(/c/sol/include /c/sol/build/vendor/lua-5.4.4/src)
link_directories(/c/serv/)
#set(CMAKE_EXE_LINKER_FLAGS " -static")
add_executable(server ${main_code})
target_link_libraries(server lua-5.4.4 ws2_32 winmm pthread z -static-libgcc -static-libstdc++)
