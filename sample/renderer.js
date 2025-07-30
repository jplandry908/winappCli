window.electronAPI.onDeepLink((data) => {
        console.log('Received deep link:', data);
        const message = document.createElement('p');
        message.textContent = `Deep link received: ${data}`;
        document.body.appendChild(message);
      });

// Example of showing a notification
document.getElementById('show-notification').addEventListener('click', () => {
  window.electronAPI.showNotification('Hello', 'This is a WinRT notification from Electron!');
});