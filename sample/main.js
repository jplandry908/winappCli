const { app, BrowserWindow, ipcMain } = require('electron')
const path = require('path');

const addon = require('./native/build/Release/native.node');

app.setAppUserModelId('electron-msix-example');

// temp workaround for Sparse packaging issues
// do not use in production
app.commandLine.appendSwitch('--no-sandbox');

// Handle deep links
let PROTOCOL = 'sparse-example';

if (process.defaultApp) {
    PROTOCOL += '-dev';
  app.setAsDefaultProtocolClient(
    PROTOCOL,
    process.execPath,
    [path.resolve(process.argv[1])]
  );
} else {
  // in an installed .exe this is enough
  app.setAsDefaultProtocolClient(PROTOCOL);
}

let mainWindow = null;

const createWindow = () => {
  mainWindow = new BrowserWindow({
    width: 800,
    height: 600,
    webPreferences: {
        contextIsolation: true,
        nodeIntegration: false,
        preload: path.join(__dirname, 'preload.js')
  }
  })

  mainWindow.loadFile('index.html')

  mainWindow.webContents.openDevTools()
}

app.whenReady().then(() => {
  createWindow()

  setTimeout(() => {
    // This is a workaround to ensure the app can handle deep links
    // when launched from the command line or as a default protocol handler.
    const url = process.argv.find(a => a.startsWith(`${PROTOCOL}://`));
    if (url) handleDeepLink(url);
  }, 2000); 
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') app.quit()
});

// Ensure single instance mode
const gotLock = app.requestSingleInstanceLock();
if (!gotLock) {
  app.quit();
  process.exit(0);
}

app.on('second-instance', (_e, argv) => {
  // argv contains *all* cmd-line args; the deep-link is one of them
  const url = argv.find(a => a.startsWith(`${PROTOCOL}://`));
  if (url) handleDeepLink(url);
  if (mainWindow?.isMinimized()) mainWindow.restore();
  mainWindow?.focus();
});

function handleDeepLink(url) {
  // Handle the deep link URL
  console.log(`Handling deep link: ${url}`);
  
  // You can parse the URL and perform actions based on its content
  const parsedUrl = new URL(url);
  const queryParams = new URLSearchParams(parsedUrl.search);
  
  // Example: Log query parameters
  for (const [key, value] of queryParams.entries()) {
    console.log(`Query param: ${key} = ${value}`);
  }
  
  // You can also send this data to your renderer process if needed
  mainWindow.webContents.send('deep-link', url);
}

ipcMain.handle('show-notification', async (event, title, body) => {
  addon.showNotification(title, body);

  // var response = addon.callPhiSilica("hello", (part) => {
  //   console.log(part);
  // });

  // console.log("Response from addon:", response);

  // addon.setActionAvailability("actionName", true);
});