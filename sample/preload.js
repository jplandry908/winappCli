const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('electronAPI', {
  onDeepLink: (callback) => ipcRenderer.on('deep-link', (event, data) => callback(data)),
  showNotification: (title, body) => ipcRenderer.invoke('show-notification', title, body),
});