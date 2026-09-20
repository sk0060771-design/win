cmake_minimum_required(VERSION 3.20)
project(ReelForgeNative LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

add_executable(ReelForgeNative WIN32 main.cpp)
target_compile_definitions(ReelForgeNative PRIVATE UNICODE _UNICODE NOMINMAX)
target_link_libraries(ReelForgeNative PRIVATE comctl32 gdiplus shlwapi winmm)

if(MSVC)
  target_compile_options(ReelForgeNative PRIVATE /W4 /EHsc)
endif()
