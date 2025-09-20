{
  "targets": [
    {
      "target_name": "{addon-name}",
      "sources": ["{addon-name}.cc"],
      "include_dirs": [
        "<!@(node -p \"require('node-addon-api').include\")",
        "<!(node -e \"require('nan')\")",
        "<!@(node -p \"require('windows-sdk').getGlobalWinsdkPath().replace(/\\\\/g, '/') + '/include'\")",
      ],
      "defines": [ "NAPI_DISABLE_CPP_EXCEPTIONS" ],
      "library_dirs": [
        "<!@(node -p \"require('windows-sdk').getGlobalWinsdkPath().replace(/\\\\/g, '/') + '/lib/<(target_arch)'\")",
        "../build/<(target_arch)/Release"
      ],
      "libraries": [
        "WindowsApp.lib",
        "Microsoft.WindowsAppRuntime.Bootstrap.lib"
      ]
    }
  ]
}