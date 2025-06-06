cmake_minimum_required(VERSION 3.16)

set(CMAKE_CXX_STANDARD 17)

project(RayMarching)

# Find packages
find_package(OpenGL REQUIRED)
find_package(glfw3 REQUIRED)

# Include directories
include_directories(${PROJECT_SOURCE_DIR}/includes)

# macOS-specific settings
if(APPLE)
    # On macOS, GLFW includes OpenGL, so we don't need to link it separately
    set(EXTRA_LIBS "")
    # Frameworks needed on macOS
    find_library(COCOA_LIBRARY Cocoa)
    find_library(IOKIT_LIBRARY IOKit)
    find_library(COREVIDEO_LIBRARY CoreVideo)
else()
    # On other systems, link these libraries
    set(EXTRA_LIBS dl GL)
endif()

add_executable(RayMarching 
    stb_image/stb_image.cpp 
    Shader.h 
    glad.c 
    RayMarching.cpp
)

# Link libraries
target_link_libraries(RayMarching
    ${OPENGL_LIBRARIES}
    glfw
    ${EXTRA_LIBS}
    ${COCOA_LIBRARY}
    ${IOKIT_LIBRARY}
    ${COREVIDEO_LIBRARY}
)