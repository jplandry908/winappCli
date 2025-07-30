## Running the sample
1. First clone and run `npm install`.

1. Package the app with `npm run package`

    > The sample uses electron-builder for packaging the app as msi, but everything should work for other types. 

    > Note in the package.json, we are also including an *.exe.manifest file from the root of the project - this is a requirement for sparse packaging (see docs below)

1. Install the generated msi from the "dist" folder

1. Run the following command to register the appxmanifest.xml found in the msix folder:

    ```
    Add-AppxPackage -Path path-to-project\msix\appxmanifest.xml -ExternalLocation path-to-installed-location -Register -ForceUpdateFromAnyVersion
    ```

    > Notice that we are installing a package by using the appxmanifest.xml file directly - this is for testing purposes because it simplifies the process (you need to have developer mode enabled on your machine).

    > For production, you would need to generate a signed msix package from that .xml file and run the Add-AppxPackage command with that msix package instead. 

    > This process would need to be handled by your installer when installing the app on the users machine

1. You can then test the registered action by testing with the App Action Testing Playground App found here https://apps.microsoft.com/detail/9PLSWV2GR8B4?hl=en-us&gl=US&ocid=pdpshare

![image](https://github.com/user-attachments/assets/f6745b69-50a2-4ad8-a97b-5cb00f800a8e)


## Relevant docs

Sparse packaging documentation: https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/grant-identity-to-nonpackaged-apps
App Actions documentation: https://learn.microsoft.com/en-us/windows/ai/app-actions/

## Known issue

To get sparse packaging an Electron app to work, I've had to  disable sandboxing, creating a security concern (see `main.js:6`). This will need to be resolved to make this a viable solution for production apps.

As alternative, packaging the app fully with MSIX works and should be used instead.



# Package as full msix
To test the app packaged as a MSIX, I added a script to run the app as msix. Run these commands:

```bash
 # if not ran already
npm run install
```

```bash
# to build the app for prod, but not package
# the output will be in the dist/win-unpacked folder
npm run build 
```

```bash
# copy the msix/appxmanifest-full.xml to the dist folder above and rename to appxmanifest.xml
# also copy the msix/assets folder
npm run make-msix-prepare
```

You can run the `Add-AppxPackage` command to install the msix
```ps
Add-AppxPackage -Register .\dist\win-unpacked\appxmanifest.xml
```

Run the app from the start menu (it should be called `ElectronExample`)

> Alternatively, you can package the app as msix with `npm run make-msix` but you will need to sign it before installing - I didn't go as far to automate signing the package :D