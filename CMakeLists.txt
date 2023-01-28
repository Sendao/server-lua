cmake_minimum_required (VERSION 3.15)
project(whirlwind)

export(PACKAGE whirlwind)

file(GLOB main_code
	"src/*.cpp"
)

include_directories(/home/sendao/sol2/include)
add_executable(server ${main_code})
target_link_libraries(server lua-5.4)
