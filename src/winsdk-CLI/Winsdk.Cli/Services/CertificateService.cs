using Winsdk.Cli.Helpers;
using System.Diagnostics.Eventing.Reader;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Winsdk.Cli.Services;

internal partial class CertificateService(
    IBuildToolsService buildToolsService,
    IPowerShellService powerShellService,
    IGitignoreService gitignoreService,
    ILogger<CertificateService> logger) : ICertificateService
{
    public const string DefaultCertFileName = "devcert.pfx";

    private static Dictionary<string, string> GetCertificateEnvironmentVariables()
    {
        return new Dictionary<string, string>
        {
            ["PSModulePath"] = "C:\\Program Files\\WindowsPowerShell\\Modules;C:\\WINDOWS\\system32\\WindowsPowerShell\\v1.0\\Modules"
        };
    }

    public record CertificateResult(
        string CertificatePath,
        string Password,
        string Publisher,
        string SubjectName
    );

    public async Task<CertificateResult> GenerateDevCertificateAsync(
        string publisher,
        string outputPath,
        string password = "password",
        int validDays = 365,
        CancellationToken cancellationToken = default)
    {
        if (!Path.IsPathRooted(outputPath))
        {
            outputPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), outputPath));
        }
        // Ensure output directory exists
        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        // Clean up the publisher name to ensure proper CN format
        // Remove any existing CN= prefix and clean up quotes
        var cleanPublisher = publisher.Replace("CN=", "").Replace("\"", "").Replace("'", "");

        // Ensure we have a proper CN format
        var subjectName = $"CN={cleanPublisher}";

        var command = $"$dest='{outputPath}';$cert=New-SelfSignedCertificate -Type Custom -Subject '{subjectName}' -KeyUsage DigitalSignature -FriendlyName 'MSIX Dev Certificate' -CertStoreLocation 'Cert:\\CurrentUser\\My' -KeyProtection None -KeyExportPolicy Exportable -Provider 'Microsoft Software Key Storage Provider' -TextExtension @('2.5.29.37={{text}}1.3.6.1.5.5.7.3.3', '2.5.29.19={{text}}') -NotAfter (Get-Date).AddDays({validDays}); Export-PfxCertificate -Cert $cert -FilePath $dest -Password (ConvertTo-SecureString -String '{password}' -Force -AsPlainText) -Force";

        try
        {
            var (exitCode, output) = await powerShellService.RunCommandAsync(command, environmentVariables: GetCertificateEnvironmentVariables(), cancellationToken: cancellationToken);

            if (exitCode != 0)
            {
                var message = $"PowerShell command failed with exit code {exitCode}";
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    message += $": {output}";
                }
                throw new InvalidOperationException(message);
            }

            logger.LogDebug("Certificate generated: {OutputPath}", outputPath);

            return new CertificateResult(
                CertificatePath: outputPath,
                Password: password,
                Publisher: cleanPublisher,
                SubjectName: subjectName
            );
        }
        catch (Exception error)
        {
            throw new InvalidOperationException($"Failed to generate development certificate: {error.Message}", error);
        }
    }

    public async Task<bool> InstallCertificateAsync(string certPath, string password, bool force, CancellationToken cancellationToken = default)
    {
        if (!Path.IsPathRooted(certPath))
        {
            certPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), certPath));
        }

        if (!File.Exists(certPath))
        {
            throw new FileNotFoundException($"Certificate file not found: {certPath}");
        }

        logger.LogDebug("Installing development certificate: {CertPath}", certPath);

        try
        {
            // Check if certificate is already installed (unless force is true)
            if (!force)
            {
                var certName = Path.GetFileNameWithoutExtension(certPath);
                var checkCommand = $"Get-ChildItem -Path 'Cert:\\LocalMachine\\TrustedPeople' | Where-Object {{ $_.Subject -like '*{certName}*' }}";

                try
                {
                    var (_, result) = await powerShellService.RunCommandAsync(checkCommand, environmentVariables: GetCertificateEnvironmentVariables(), cancellationToken: cancellationToken);

                    if (!string.IsNullOrWhiteSpace(result))
                    {
                        logger.LogDebug("Certificate appears to already be installed");
                        return false;
                    }
                }
                catch
                {
                    // Continue with installation if check fails
                }
            }

            // Install to TrustedPeople store (required for MSIX sideloading)
            // Create the PowerShell command directly
            var absoluteCertPath = Path.GetFullPath(certPath);
            var installCommand = $"Import-PfxCertificate -FilePath '{absoluteCertPath}' -CertStoreLocation 'Cert:\\LocalMachine\\TrustedPeople' -Password (ConvertTo-SecureString -String '{password}' -Force -AsPlainText)";

            await powerShellService.RunCommandAsync(installCommand, elevated: true, cancellationToken: cancellationToken);

            logger.LogDebug("Certificate installed successfully to TrustedPeople store");

            return true;
        }
        catch (Exception error)
        {
            throw new InvalidOperationException($"Failed to install development certificate: {error.Message}", error);
        }
    }

    /// <summary>
    /// Signs a file with a certificate.
    /// This method can be used to sign any file, including but not limited to MSIX packages.
    /// </summary>
    /// <param name="filePath">Path to the file to sign</param>
    /// <param name="certificatePath">Path to the .pfx certificate file</param>
    /// <param name="password">Certificate password</param>
    /// <param name="timestampUrl">Timestamp server URL (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task SignFileAsync(string filePath, string certificatePath, string? password = "password", string? timestampUrl = null, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        if (!File.Exists(certificatePath))
        {
            throw new FileNotFoundException($"Certificate file not found: {certificatePath}");
        }

        var arguments = $@"sign /f ""{certificatePath}"" /p ""{password}"" /fd SHA256";

        if (!string.IsNullOrWhiteSpace(timestampUrl))
        {
            arguments += $@" /tr ""{timestampUrl}"" /td SHA256";
        }

        arguments += $@" ""{filePath}""";

        logger.LogDebug("Signing file: {FilePath}", filePath);

        try
        {
            await buildToolsService.RunBuildToolAsync("signtool.exe", arguments, cancellationToken: cancellationToken);

            logger.LogDebug("File signed successfully");
        }
        catch (BuildToolsService.InvalidBuildToolException ex)
            when (ex.Stdout.Contains("0x800"))
        {
            var query = new EventLogQuery(
                "Microsoft-Windows-AppxPackaging/Operational",
                PathType.LogName,
                $"*[System[Level=2 and Execution[@ProcessID={ex.ProcessId}]]]");

            EventRecord? record = null;
            var timeout = TimeSpan.FromSeconds(5);
            var pollingInterval = TimeSpan.FromMilliseconds(500);
            var startTime = DateTime.UtcNow;

            while (record == null && (DateTime.UtcNow - startTime) < timeout && !cancellationToken.IsCancellationRequested)
            {
                using var reader = new EventLogReader(query);
                record = reader.ReadEvent();

                if (record != null)
                {
                    throw new InvalidOperationException($"Failed to sign file: {record.FormatDescription()}", ex);
                }
                
                await Task.Delay(pollingInterval, cancellationToken);
            }

            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to sign file: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Generates a development certificate with automatic publisher inference, console output, and installation.
    /// This method combines publisher inference, certificate generation, gitignore management, console messaging, and optional installation.
    /// </summary>
    /// <param name="outputPath">Path where the certificate should be generated</param>
    /// <param name="explicitPublisher">Explicit publisher to use (optional)</param>
    /// <param name="manifestPath">Specific manifest path to extract publisher from (optional)</param>
    /// <param name="password">Certificate password</param>
    /// <param name="validDays">Certificate validity period</param>
    /// <param name="skipIfExists">Skip generation if certificate already exists</param>
    /// <param name="updateGitignore">Whether to update .gitignore</param>
    /// <param name="install">Whether to install the certificate after generation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Certificate generation result, or null if skipped</returns>
    public async Task<CertificateResult?> GenerateDevCertificateWithInferenceAsync(
        string outputPath,
        string? explicitPublisher = null,
        string? manifestPath = null,
        string password = "password",
        int validDays = 365,
        bool skipIfExists = true,
        bool updateGitignore = true,
        bool install = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Skip if certificate already exists and skipIfExists is true
            if (skipIfExists && File.Exists(outputPath))
            {
                logger.LogInformation("{UISymbol} Development certificate already exists: {OutputPath}", UiSymbols.Note, outputPath);
                return null;
            }

            // Start generation message
            logger.LogInformation("{UISymbol} Generating development certificate...", UiSymbols.Gear);

            // Get default publisher from system defaults
            var defaultPublisher = SystemDefaultsHelper.GetDefaultPublisherCN();

            // Infer publisher using the specified hierarchy
            string publisher = await InferPublisherAsync(explicitPublisher, manifestPath, defaultPublisher, cancellationToken);

            logger.LogInformation("Certificate publisher: {Publisher}", publisher);

            // Generate the certificate
            var result = await GenerateDevCertificateAsync(
                publisher,
                outputPath,
                password,
                validDays,
                cancellationToken);

            // Success message
            logger.LogInformation("{UISymbol} Development certificate generated → {CertificatePath}", UiSymbols.Check, result.CertificatePath);

            // Add certificate to .gitignore
            if (updateGitignore)
            {
                var baseDirectory = Path.GetDirectoryName(outputPath) ?? Directory.GetCurrentDirectory();
                var certFileName = Path.GetFileName(result.CertificatePath);
                gitignoreService.AddCertificateToGitignore(baseDirectory, certFileName);
            }

            // Display password information
            if (password == "password")
            {
                logger.LogInformation("{UISymbol} Using default password", UiSymbols.Note);
            }

            // Install certificate if requested
            if (install)
            {
                logger.LogDebug("Installing certificate...");

                var installResult = await InstallCertificateAsync(result.CertificatePath, password, false, cancellationToken);
                if (installResult)
                {
                    logger.LogInformation("✅ Certificate installed successfully!");
                }
                else
                {
                    logger.LogInformation("ℹ️ Certificate was already installed");
                }
            }
            else
            {
                logger.LogInformation("{UISymbol} Use 'winsdk cert install' to install the certificate for development", UiSymbols.Note);
            }

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError("❌ Failed to generate development certificate: {Message}", ex.Message);
            logger.LogDebug(ex, "Certificate generation failed with exception");
            throw; // Re-throw for callers that want to handle the error differently
        }
    }

    /// <summary>
    /// Extracts the publisher name from a certificate file
    /// </summary>
    /// <param name="certificatePath">Path to the certificate file (.pfx)</param>
    /// <param name="password">Certificate password</param>
    /// <returns>Publisher name (without CN= prefix)</returns>
    /// <exception cref="FileNotFoundException">Certificate file not found</exception>
    /// <exception cref="InvalidOperationException">Certificate cannot be loaded or has no subject</exception>
    public static string ExtractPublisherFromCertificate(string certificatePath, string password)
    {
        if (!File.Exists(certificatePath))
        {
            throw new FileNotFoundException($"Certificate file not found: {certificatePath}");
        }

        try
        {
            using var cert = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12FromFile(
                certificatePath, password, System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.Exportable);

            var subject = cert.Subject;
            if (string.IsNullOrWhiteSpace(subject))
            {
                throw new InvalidOperationException("Certificate has no subject information");
            }

            // Extract CN from the subject (format: "CN=Publisher, O=Organization, ...")
            var cnMatch = CnFieldRegex().Match(subject);
            if (!cnMatch.Success)
            {
                throw new InvalidOperationException($"Certificate subject does not contain CN field: {subject}");
            }

            var publisher = cnMatch.Groups[1].Value.Trim();

            // Remove any quotes that might be present
            publisher = publisher.Trim('"', '\'');

            return publisher;
        }
        catch (Exception ex) when (!(ex is FileNotFoundException || ex is InvalidOperationException))
        {
            throw new InvalidOperationException($"Failed to extract publisher from certificate: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validates that the publisher in the certificate matches the publisher in the AppX manifest
    /// </summary>
    /// <param name="certificatePath">Path to the certificate file</param>
    /// <param name="password">Certificate password</param>
    /// <param name="manifestPath">Path to the AppX manifest file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <exception cref="InvalidOperationException">Publishers don't match or validation failed</exception>
    public static async Task ValidatePublisherMatchAsync(string certificatePath, string password, string manifestPath, CancellationToken cancellationToken = default)
    {
        try
        {
            // Extract publisher from certificate
            var certPublisher = ExtractPublisherFromCertificate(certificatePath, password);

            // Extract publisher from manifest
            var manifestIdentity = await MsixService.ParseAppxManifestFromPathAsync(manifestPath, cancellationToken);
            var manifestPublisher = manifestIdentity.Publisher;

            // Normalize both publishers for comparison (remove CN= prefix and quotes)
            var normalizedCertPublisher = ManifestTemplateService.StripCnPrefix(certPublisher);
            var normalizedManifestPublisher = ManifestTemplateService.StripCnPrefix(manifestPublisher);

            // Compare publishers (case-insensitive)
            if (!string.Equals(normalizedCertPublisher, normalizedManifestPublisher, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Error: Publisher in {manifestPath} (CN={normalizedManifestPublisher}) does not match the publisher in the certificate {certificatePath} (CN={normalizedCertPublisher}).");
            }
        }
        catch (Exception ex) when (!(ex is InvalidOperationException))
        {
            throw new InvalidOperationException($"Failed to validate publisher match: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Infers the publisher name using the specified hierarchy:
    /// 1. If explicit publisher is provided, use that
    /// 2. If manifest path is provided, extract publisher from that manifest
    /// 3. If appxmanifest.xml is found in project (.winsdk directory), use that
    /// 4. Use the system default publisher (from SystemDefaultsService.GetDefaultPublisherCN())
    /// </summary>
    private async Task<string> InferPublisherAsync(
        string? explicitPublisher,
        string? manifestPath,
        string defaultPublisher,
        CancellationToken cancellationToken)
    {
        // 1. If explicit publisher is provided, use that
        if (!string.IsNullOrWhiteSpace(explicitPublisher))
        {
            return explicitPublisher;
        }

        // 2. If manifest path is provided, extract publisher from that manifest
        if (!string.IsNullOrWhiteSpace(manifestPath))
        {
            try
            {
                logger.LogInformation("Certificate publisher inferred from: {ManifestPath}", manifestPath);

                var identityInfo = await MsixService.ParseAppxManifestFromPathAsync(manifestPath, cancellationToken);
                return identityInfo.Publisher;
            }
            catch (Exception ex)
            {
                logger.LogDebug("Could not extract publisher from manifest: {Message}", ex.Message);
            }
        }

        // 3. If appxmanifest.xml is found in the current project, use that
        var projectManifestPath = MsixService.FindProjectManifest();
        if (projectManifestPath != null)
        {
            try
            {
                logger.LogInformation("Certificate publisher inferred from: {ProjectManifestPath}", projectManifestPath);

                var identityInfo = await MsixService.ParseAppxManifestFromPathAsync(projectManifestPath, cancellationToken);
                return identityInfo.Publisher;
            }
            catch (Exception ex)
            {
                logger.LogDebug("Could not extract publisher from project manifest: {Message}", ex.Message);
            }
        }

        // 4. Use default publisher
        logger.LogInformation("No manifest found, using default publisher: {DefaultPublisher}", defaultPublisher);
        return defaultPublisher;
    }

    [GeneratedRegexAttribute(@"CN=([^,]+)", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex CnFieldRegex();
}
