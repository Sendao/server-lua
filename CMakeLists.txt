cmake_minimum_required (VERSION 2.8)
project(whirlwind)

include_directories( "./include" )

export(PACKAGE whirlwind)

set(CMAKE_CXX_FLAGS ${CMAKE_CXX_FLAGS} "-std=c++11 -rdynamic")
file(GLOB srcode src/*cpp)
