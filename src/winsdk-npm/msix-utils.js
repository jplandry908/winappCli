const fs = require('fs').promises;
const fsSync = require('fs');
const path = require('path');
const { execSync } = require('child_process');
const { callWinsdkCli } = require('./winsdk-cli-utils');

/**
 * Adds MSIX identity information from an appxmanifest.xml file to an executable's embedded manifest
 * @param {string} exePath - Path to the executable file
 * @param {string} appxManifestPath - Path to the appxmanifest.xml file containing MSIX identity data
 * @param {Object} options - Optional configuration
 * @param {boolean} options.verbose - Enable verbose logging (default: true)
 * @param {string} options.tempDir - Directory for temporary files (default: same as exe directory)
 */
async function addMsixIdentityToExe(exePath, appxManifestPath, options = {}) {
  const { verbose = true, tempDir } = options;
  
  if (verbose) {
    console.log('Adding MSIX identity to executable using native CLI...');
  }

  // Build arguments for native CLI
  const args = ['msix', 'add-identity-to-exe', exePath, appxManifestPath];
  
  // Add optional arguments
  if (tempDir) {
    args.push('--temp-dir', tempDir);
  }
  
  if (verbose) {
    args.push('--verbose');
  }
  
  // Call native CLI
  await callWinsdkCli(args, { verbose });
  
  // Extract identity information for return value (maintains API compatibility)
  try {
    const appxManifestContent = await fs.readFile(appxManifestPath, 'utf8');
    
    const nameMatch = appxManifestContent.match(/<Identity[^>]*Name\s*=\s*["']([^"']*)["']/i);
    const publisherMatch = appxManifestContent.match(/<Identity[^>]*Publisher\s*=\s*["']([^"']*)["']/i);
    const applicationMatch = appxManifestContent.match(/<Application[^>]*Id\s*=\s*["']([^"']*)["'][^>]*>/i);
    
    return {
      success: true,
      packageName: nameMatch ? nameMatch[1] : null,
      publisher: publisherMatch ? publisherMatch[1] : null,
      applicationId: applicationMatch ? applicationMatch[1] : null
    };
  } catch (error) {
    // If we can't parse the manifest for return values, still return success since CLI succeeded
    return {
      success: true,
      packageName: null,
      publisher: null,
      applicationId: null
    };
  }
}

/**
 * Adds MSIX identity to the Electron debug process
 * @param {Object} options - Configuration options
 * @param {boolean} options.verbose - Enable verbose logging (default: true)
 */
async function addElectronDebugIdentity(options = {}) {
  const { verbose = true } = options;
  
  if (verbose) {
    console.log('� Adding MSIX debug identity to Electron...');
  }
  
  
  try {
    // Step 1: Make a backup of electron.exe
    const electronExePath = path.join(process.cwd(), 'node_modules', 'electron', 'dist', 'electron.exe');
    const electronBackupPath = path.join(process.cwd(), 'node_modules', 'electron', 'dist', 'electron.backup.exe');

    if (!fsSync.existsSync(electronExePath)) {
      throw new Error(`Electron executable not found at: ${electronExePath}`);
    }
    
    if (verbose) {
      console.log('💾 Creating backup of electron.exe...');
    }
    
    // Create backup if it doesn't exist, or if the current exe is newer than the backup
    if (!fsSync.existsSync(electronBackupPath) || 
        fsSync.statSync(electronExePath).mtime > fsSync.statSync(electronBackupPath).mtime) {
      await fs.copyFile(electronExePath, electronBackupPath);
      
      if (verbose) {
        console.log(`✅ Backup created: ${electronBackupPath}`);
      }
    } else {
      if (verbose) {
        console.log('⏭️  Backup already exists and is up to date');
      }
    }
    
    // Step 2: Use the native CLI to create debug identity (handles manifest generation, identity addition, and package registration)
    if (verbose) {
      console.log('🔐 Creating debug identity using native CLI...');
    }
    
    const currentDir = process.cwd();
    await callWinsdkCli([
      'create-debug-identity',
      electronExePath,
      '--location', currentDir,
      verbose ? '-v' : ''
    ], { verbose });
    
    if (verbose) {
      console.log('✅ Debug identity created and package registered successfully');
    }
    
    // Determine the manifest path after CLI execution
    const msixDebugDir = path.resolve('.winsdk/debug');
    const manifestPath = path.join(msixDebugDir, 'appxmanifest.xml');
    
    // Read the manifest to extract package details for the result
    let packageName, publisher, applicationId;
    try {
      const manifestContent = await fs.readFile(manifestPath, 'utf8');
      const nameMatch = manifestContent.match(/<Identity[^>]*Name\s*=\s*["']([^"']*)["']/i);
      const publisherMatch = manifestContent.match(/<Identity[^>]*Publisher\s*=\s*["']([^"']*)["']/i);
      const applicationMatch = manifestContent.match(/<Application[^>]*Id\s*=\s*["']([^"']*)["']/i);
      
      packageName = nameMatch ? nameMatch[1] : 'Unknown';
      publisher = publisherMatch ? publisherMatch[1] : 'Unknown';
      applicationId = applicationMatch ? applicationMatch[1] : 'Unknown';
    } catch (error) {
      packageName = publisher = applicationId = 'Unknown';
    }
    
    const result = {
      success: true,
      electronExePath,
      backupPath: electronBackupPath,
      manifestPath,
      assetsDir: path.join(msixDebugDir, 'Assets'),
      packageName,
      publisher,
      applicationId
    };
    
    if (verbose) {
      console.log('🎉 Electron debug identity setup completed successfully!');
      console.log(`📦 Package: ${result.packageName}`);
      console.log(`👤 Publisher: ${result.publisher}`);
      console.log(`🆔 App ID: ${result.applicationId}`);
      console.log(`📁 Manifest: ${result.manifestPath}`);
    }
    
    return result;
    
  } catch (error) {
    throw new Error(`Failed to add Electron debug identity: ${error.message}`);
  }
}

module.exports = {
  addMsixIdentityToExe,
  addElectronDebugIdentity
};
