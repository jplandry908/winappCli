/**
 * Get the path to the global .winsdk directory
 * @returns {string} The full path to the global .winsdk directory
 * @throws {Error} If the global .winsdk directory is not found
 */
function getGlobalWinsdkPath() {
  const { execSync } = require('child_process');
  const { getWinsdkCliPath } = require('./winsdk-cli-utils');
  
  try {
    const winsdkCliPath = getWinsdkCliPath();
    const result = execSync(`"${winsdkCliPath}" get-global-winsdk`, {
      encoding: 'utf8',
      stdio: ['pipe', 'pipe', 'pipe']
    });
    return result.trim();
  } catch (error) {
    throw new Error(`Global .winsdk directory not found. Make sure to run 'winsdk setup' first.`);
  }
}

module.exports = {
  getGlobalWinsdkPath
};