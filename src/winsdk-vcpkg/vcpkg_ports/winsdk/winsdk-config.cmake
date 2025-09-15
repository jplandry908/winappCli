get_filename_component(_packages_dir "${CMAKE_CURRENT_LIST_DIR}" PATH)
get_filename_component(_packages_dir  "${_packages_dir}" PATH)

if(NOT TARGET WindowsAppSdk::DWriteCore)
   add_library(WindowsAppSdk::DWriteCore STATIC IMPORTED)
   set_property(
      TARGET WindowsAppSdk::DWriteCore 
      PROPERTY IMPORTED_LOCATION "${_packages_dir}/lib/DWriteCore.lib"
    )
endif()

if(NOT TARGET WindowsAppSdk::Bootstrap)
   add_library(WindowsAppSdk::Bootstrap SHARED IMPORTED)
   set_target_properties(WindowsAppSdk::Bootstrap 
      PROPERTIES
        INTERFACE_INCLUDE_DIRECTORIES "${_packages_dir}/include"
        IMPORTED_IMPLIB "${_packages_dir}/lib/Microsoft.WindowsAppRuntime.Bootstrap.lib"
        IMPORTED_LOCATION "${_packages_dir}/bin/Microsoft.WindowsAppRuntime.Bootstrap.dll"
        RUNTIME_DLLS "${_packages_dir}/bin/Microsoft.WindowsAppRuntime.Bootstrap.dll"
    )
endif()

if(NOT TARGET WindowsAppSdk::Runtime)
   add_library(WindowsAppSdk::Runtime STATIC IMPORTED)
   set_property(
      TARGET WindowsAppSdk::Runtime 
      PROPERTY IMPORTED_LOCATION "${_packages_dir}/lib/Microsoft.WindowsAppRuntime.lib"
    )
endif()

if(NOT TARGET Microsoft::WindowsAppSdk)
   add_library(Microsoft::WindowsAppSdk INTERFACE IMPORTED)
 #  add_custom_target( DEPENDS "${wasdk_stamp}")
   target_link_libraries(Microsoft::WindowsAppSdk INTERFACE
      WindowsAppSdk::DWriteCore
      WindowsAppSdk::Bootstrap
      WindowsAppSdk::Runtime
   )
endif()

# Function to copy AppX package files to build directory
function(winsdk_copy_appx_files)
    set(options)
    set(oneValueArgs MANIFEST_FILE ASSETS_DIR DESTINATION)
    set(multiValueArgs)
    cmake_parse_arguments(PARSE_ARGV 0 ARG "${options}" "${oneValueArgs}" "${multiValueArgs}")
    
    # Set default values
    if(NOT ARG_MANIFEST_FILE)
        set(ARG_MANIFEST_FILE "AppxManifest.xml")
    endif()
    
    if(NOT ARG_ASSETS_DIR)
        set(ARG_ASSETS_DIR "Images")
    endif()
    
    if(NOT ARG_DESTINATION)
        set(ARG_DESTINATION "${CMAKE_BINARY_DIR}")
    endif()
    
    # Copy the AppxManifest content to the binary directory
    file(
        COPY 
            "${ARG_ASSETS_DIR}"
            "${ARG_MANIFEST_FILE}"
        DESTINATION 
            "${ARG_DESTINATION}"
    )
endfunction()

# Function to register AppX package for debugging
function(winsdk_register_appx_package TARGET_NAME)
    set(options)
    set(oneValueArgs MANIFEST_FILE PACKAGE_LOCATION)
    set(multiValueArgs)
    cmake_parse_arguments(PARSE_ARGV 1 ARG "${options}" "${oneValueArgs}" "${multiValueArgs}")
    
    # Set default values
    if(NOT ARG_MANIFEST_FILE)
        set(ARG_MANIFEST_FILE "AppxManifest.xml")
    endif()
    
    if(NOT ARG_PACKAGE_LOCATION)
        set(ARG_PACKAGE_LOCATION "${CMAKE_BINARY_DIR}")
    endif()
    
    # Register the app package after build for in-place launch & debugging
    add_custom_command(TARGET ${TARGET_NAME} POST_BUILD
        COMMAND powershell -ExecutionPolicy Bypass -Command "Add-AppxPackage -Path \"${ARG_PACKAGE_LOCATION}/${ARG_MANIFEST_FILE}\" -ExternalLocation \"${ARG_PACKAGE_LOCATION}\" -Register -ForceUpdateFromAnyVersion"
        COMMENT "Registering the app package for ${TARGET_NAME}"
    )
endfunction()

unset(_packages_dir)
