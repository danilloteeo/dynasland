cmake_minimum_required(VERSION 3.20)
project(DynaIslandNativeEngine VERSION 1.0.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin)

# Runtime do VC++ embutido na DLL: o instalador e o zip portátil não dependem do redistribuível instalado.
set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")

add_library(DynaIslandEngine SHARED
    CoreEngine.cpp
    Interop/exports.cpp
    WindowsAPI/HookThread.cpp)
target_include_directories(DynaIslandEngine PRIVATE ${CMAKE_CURRENT_SOURCE_DIR})
target_compile_definitions(DynaIslandEngine PRIVATE DI_BUILDING_DLL UNICODE _UNICODE)
target_compile_options(DynaIslandEngine PRIVATE /W4 /WX /permissive- /EHsc /utf-8)
target_link_libraries(DynaIslandEngine PRIVATE user32)

enable_testing()
add_executable(DynaIslandEngineTests tests/engine_tests.cpp)
target_include_directories(DynaIslandEngineTests PRIVATE ${CMAKE_CURRENT_SOURCE_DIR})
target_compile_options(DynaIslandEngineTests PRIVATE /W4 /WX /permissive- /EHsc /utf-8)
target_link_libraries(DynaIslandEngineTests PRIVATE DynaIslandEngine)
add_test(NAME engine COMMAND DynaIslandEngineTests)
