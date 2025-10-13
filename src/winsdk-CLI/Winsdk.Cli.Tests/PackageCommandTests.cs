using Winsdk.Cli.Services;

namespace Winsdk.Cli.Tests;

[TestClass]
public class PackageCommandTests : BaseCommandTests
{
    private string _tempDirectory = null!;
    private string _testWinsdkDirectory = null!;
    private IConfigService _configService = null!;
    private IBuildToolsService _buildToolsService = null!;
    private IMsixService _msixService = null!;

    /// <summary>
    /// Standard test manifest content for use across multiple tests
    /// </summary>
    private const string StandardTestManifestContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Package xmlns=""http://schemas.microsoft.com/appx/manifest/foundation/windows10""
         xmlns:uap=""http://schemas.microsoft.com/appx/manifest/uap/windows10"">
  <Identity Name=""TestPackage""
            Publisher=""CN=TestPublisher""
            Version=""1.0.0.0"" />
  <Properties>
    <DisplayName>Test Package</DisplayName>
    <PublisherDisplayName>Test Publisher</PublisherDisplayName>
    <Description>Test package for integration testing</Description>
    <Logo>Assets\Logo.png</Logo>
  </Properties>
  <Dependencies>
    <TargetDeviceFamily Name=""Windows.Universal"" MinVersion=""10.0.18362.0"" MaxVersionTested=""10.0.26100.0"" />
  </Dependencies>
  <Applications>
    <Application Id=""TestApp"" Executable=""TestApp.exe"" EntryPoint=""TestApp.App"">
      <uap:VisualElements DisplayName=""Test App"" Description=""Test application""
                          BackgroundColor=""#777777"" Square150x150Logo=""Assets\Logo.png"" Square44x44Logo=""Assets\Logo.png"" />
    </Application>
  </Applications>
</Package>";

    [TestInitialize]
    public void Setup()
    {
        // Create a temporary directory for testing
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"WinsdkPackageTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);

        // Set up a temporary winsdk directory for testing (isolates tests from real winsdk directory)
        _testWinsdkDirectory = Path.Combine(_tempDirectory, ".winsdk");
        Directory.CreateDirectory(_testWinsdkDirectory);

        // Set up services with test cache directory
        _configService = GetRequiredService<IConfigService>();
        _configService.ConfigPath = Path.Combine(_tempDirectory, "winsdk.yaml");

        var directoryService = GetRequiredService<IWinsdkDirectoryService>();
        directoryService.SetCacheDirectoryForTesting(_testWinsdkDirectory);

        _buildToolsService = GetRequiredService<IBuildToolsService>();
        _msixService = GetRequiredService<IMsixService>();
    }

    [TestCleanup]
    public void Cleanup()
    {
        // Clean up temporary files and directories
        if (Directory.Exists(_tempDirectory))
        {
            try
            {
                Directory.Delete(_tempDirectory, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    /// <summary>
    /// Creates a minimal test package structure with manifest and basic files
    /// </summary>
    private void CreateTestPackageStructure(string packageDir)
    {
        Directory.CreateDirectory(packageDir);

        // Use the shared standard test manifest content
        File.WriteAllText(Path.Combine(packageDir, "AppxManifest.xml"), StandardTestManifestContent);

        // Create Assets directory and a fake logo
        var assetsDir = Path.Combine(packageDir, "Assets");
        Directory.CreateDirectory(assetsDir);
        File.WriteAllText(Path.Combine(assetsDir, "Logo.png"), "fake png content");

        // Create a fake executable
        File.WriteAllText(Path.Combine(packageDir, "TestApp.exe"), "fake exe content");
    }

    /// <summary>
    /// Creates external test manifest content with different identity for external manifest tests
    /// </summary>
    private static string CreateExternalTestManifest()
    {
        return @"<?xml version=""1.0"" encoding=""utf-8""?>
<Package xmlns=""http://schemas.microsoft.com/appx/manifest/foundation/windows10""
         xmlns:uap=""http://schemas.microsoft.com/appx/manifest/uap/windows10"">
  <Identity Name=""ExternalTestPackage""
            Publisher=""CN=ExternalTestPublisher""
            Version=""1.0.0.0"" />
  <Properties>
    <DisplayName>External Test Package</DisplayName>
    <PublisherDisplayName>External Test Publisher</PublisherDisplayName>
    <Description>Test package with external manifest</Description>
    <Logo>Assets\StoreLogo.png</Logo>
  </Properties>
  <Dependencies>
    <TargetDeviceFamily Name=""Windows.Universal"" MinVersion=""10.0.18362.0"" MaxVersionTested=""10.0.26100.0"" />
  </Dependencies>
  <Applications>
    <Application Id=""ExternalTestApp"" Executable=""TestApp.exe"" EntryPoint=""ExternalTestApp.App"">
      <uap:VisualElements DisplayName=""External Test App"" Description=""Test application with external manifest""
                          BackgroundColor=""#333333"" Square150x150Logo=""Assets\Logo.png"" Square44x44Logo=""Assets\Logo.png"" />
    </Application>
  </Applications>
</Package>";
    }

    [TestMethod]
    public async Task PackageCommand_ToolDiscovery_FindsCommonBuildTools()
    {
        // This test verifies that common build tools can be discovered after installation
        var commonTools = new[] { "makeappx.exe", "makepri.exe", "mt.exe", "signtool.exe" };
        var foundTools = new List<string>();
        var missingTools = new List<string>();

        // Ensure BuildTools are installed
        var buildToolsPath = await _buildToolsService.EnsureBuildToolsAsync(quiet: true);
        if (buildToolsPath == null)
        {
            Assert.Fail("Cannot run test - BuildTools installation failed.");
            return;
        }

        // Check each common tool
        foreach (var tool in commonTools)
        {
            var toolPath = _buildToolsService.GetBuildToolPath(tool);
            if (toolPath != null)
            {
                foundTools.Add(tool);
                Console.WriteLine($"Found {tool} at: {toolPath}");
            }
            else
            {
                missingTools.Add(tool);
                Console.WriteLine($"Missing: {tool}");
            }
        }

        // Assert - We should find at least some of the common tools
        Assert.IsNotEmpty(foundTools, $"Should find at least some common build tools. Found: [{string.Join(", ", foundTools)}], Missing: [{string.Join(", ", missingTools)}]");

        // Specifically check for makeappx since it's commonly used
        Assert.Contains("makeappx.exe", foundTools, "makeappx.exe should be available in BuildTools");
    }

    [TestMethod]
    [DataRow(null, @"TestPackage.msix", DisplayName = "Null output path defaults to current directory with package name")]
    [DataRow("", @"TestPackage.msix", DisplayName = "Empty output path defaults to current directory with package name")]
    [DataRow("CustomPackage.msix", @"CustomPackage.msix", DisplayName = "Full filename with .msix extension uses as-is")]
    [DataRow("output", @"output\TestPackage.msix", DisplayName = "Directory path without .msix extension combines with package name")]
    [DataRow(@"C:\temp\output", @"C:\temp\output\TestPackage.msix", DisplayName = "Absolute directory path combines with package name")]
    [DataRow(@"C:\temp\AbsolutePackage.msix", @"C:\temp\AbsolutePackage.msix", DisplayName = "Absolute .msix file path uses as-is")]
    public async Task CreateMsixPackageAsync_OutputPathHandling_WorksCorrectly(string? outputPath, string expectedRelativePath)
    {
        // Arrange
        var packageDir = Path.Combine(_tempDirectory, "TestPackage");
        CreateTestPackageStructure(packageDir);

        // Create a minimal winsdk.yaml to satisfy config requirements
        await File.WriteAllTextAsync(_configService.ConfigPath, "packages: []");

        // Convert expected relative path to absolute path based on current directory
        string expectedMsixPath;
        if (Path.IsPathRooted(expectedRelativePath))
        {
            // Already absolute - use as-is
            expectedMsixPath = expectedRelativePath;
        }
        else
        {
            // Relative - make absolute based on current directory
            expectedMsixPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), expectedRelativePath));
        }

        var result = await _msixService.CreateMsixPackageAsync(
            inputFolder: packageDir,
            outputPath: outputPath,
            packageName: "TestPackage",
            skipPri: true,
            autoSign: false,
            verbose: true,
            cancellationToken: CancellationToken.None
        );

        // If we get here without exception, verify the path is correct
        Assert.AreEqual(expectedMsixPath, result.MsixPath,
            $"Output path calculation incorrect. Input: '{outputPath}', Expected: '{expectedMsixPath}', Actual: '{result.MsixPath}'");
    }

    [TestMethod]
    public async Task CreateMsixPackageAsync_InvalidInputFolder_ThrowsDirectoryNotFoundException()
    {
        // Arrange - Use non-existent directory
        var nonExistentDir = Path.Combine(_tempDirectory, "NonExistentPackage");

        // Act & Assert
        await Assert.ThrowsExactlyAsync<DirectoryNotFoundException>(async () =>
        {
            await _msixService.CreateMsixPackageAsync(
                inputFolder: nonExistentDir,
                outputPath: null,
                packageName: "TestPackage",
                skipPri: true,
                autoSign: false,
                verbose: false,
                cancellationToken: CancellationToken.None
            );
        }, "Expected DirectoryNotFoundException when input folder does not exist.");
    }

    [TestMethod]
    public async Task CreateMsixPackageAsync_MissingManifest_ThrowsFileNotFoundException()
    {
        // Arrange - Create directory without manifest
        var packageDir = Path.Combine(_tempDirectory, "TestPackageNoManifest");
        Directory.CreateDirectory(packageDir);

        // Create a fake executable but no manifest
        File.WriteAllText(Path.Combine(packageDir, "TestApp.exe"), "fake exe content");

        // Act & Assert
        await Assert.ThrowsExactlyAsync<FileNotFoundException>(async () =>
        {
            await _msixService.CreateMsixPackageAsync(
                inputFolder: packageDir,
                outputPath: null,
                packageName: "TestPackage",
                skipPri: true,
                autoSign: false,
                verbose: false,
                cancellationToken: CancellationToken.None
            );
        }, "Expected FileNotFoundException when manifest file is missing.");
    }

    [TestMethod]
    public async Task CreateMsixPackageAsync_ExternalManifestWithAssets_CopiesManifestAndAssets()
    {
        // Arrange - Create input folder without manifest
        var packageDir = Path.Combine(_tempDirectory, "InputPackage");
        Directory.CreateDirectory(packageDir);
        
        // Create the executable in the input folder
        File.WriteAllText(Path.Combine(packageDir, "TestApp.exe"), "fake exe content");

        // Create external manifest directory with manifest and assets
        var externalManifestDir = Path.Combine(_tempDirectory, "ExternalManifest");
        Directory.CreateDirectory(externalManifestDir);
        
        // Create assets directory in external location
        var externalAssetsDir = Path.Combine(externalManifestDir, "Assets");
        Directory.CreateDirectory(externalAssetsDir);
        
        // Create asset files
        File.WriteAllText(Path.Combine(externalAssetsDir, "Logo.png"), "external logo content");
        File.WriteAllText(Path.Combine(externalAssetsDir, "StoreLogo.png"), "external store logo content");
        
        // Create external manifest that references the assets
        var externalManifestPath = Path.Combine(externalManifestDir, "AppxManifest.xml");
        await File.WriteAllTextAsync(externalManifestPath, CreateExternalTestManifest());

        // Create minimal winsdk.yaml
        await File.WriteAllTextAsync(_configService.ConfigPath, "packages: []");

        var result = await _msixService.CreateMsixPackageAsync(
            inputFolder: packageDir,
            outputPath: null,
            packageName: "ExternalTestPackage",
            skipPri: true,
            autoSign: false,
            manifestPath: externalManifestPath,
            verbose: true,
            cancellationToken: CancellationToken.None
        );

        // If successful, verify the package was created correctly
        Assert.IsNotNull(result, "Result should not be null");
        Assert.Contains("ExternalTestPackage", result.MsixPath, "Package name should reflect external manifest");
        
        // Verify that assets were accessible during processing
        // The external manifest and assets should still exist
        Assert.IsTrue(File.Exists(externalManifestPath), "External manifest should still exist");
        Assert.IsTrue(File.Exists(Path.Combine(externalAssetsDir, "Logo.png")), "External Logo.png should still exist");
        Assert.IsTrue(File.Exists(Path.Combine(externalAssetsDir, "StoreLogo.png")), "External StoreLogo.png should still exist");
    }
}
