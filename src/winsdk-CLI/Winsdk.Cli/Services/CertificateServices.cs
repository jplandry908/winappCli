namespace Winsdk.Cli.Services;

internal class CertificateServices
{
    private readonly BuildToolsService _buildToolsService;
    private readonly PowerShellService _powerShellService;

    public CertificateServices(BuildToolsService buildToolsService)
    {
        _buildToolsService = buildToolsService;
        _powerShellService = new PowerShellService();
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
        bool verbose = true,
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

        var command = $"New-SelfSignedCertificate -Type Custom -Subject '{subjectName}' -KeyUsage DigitalSignature -FriendlyName 'MSIX Dev Certificate' -CertStoreLocation 'Cert:\\CurrentUser\\My' -TextExtension @('2.5.29.37={{text}}1.3.6.1.5.5.7.3.3', '2.5.29.19={{text}}') -NotAfter (Get-Date).AddDays({validDays}) | Export-PfxCertificate -FilePath '{outputPath}' -Password (ConvertTo-SecureString -String '{password}' -Force -AsPlainText)";

        if (verbose)
        {
            Console.WriteLine($"Generating development certificate for publisher: {cleanPublisher}");
            Console.WriteLine($"Certificate subject: {subjectName}");
        }

        try
        {
            var (exitCode, _) = await _powerShellService.RunCommandAsync(command, verbose: verbose, cancellationToken: cancellationToken);

            if (exitCode != 0)
            {
                throw new InvalidOperationException($"PowerShell command failed with exit code {exitCode}");
            }

            if (verbose)
            {
                Console.WriteLine($"Certificate generated: {outputPath}");
            }

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

    internal async Task<bool> InstallCertificateAsync(string certPath, string password, bool force, bool verbose, CancellationToken cancellationToken = default)
    {
        if (!Path.IsPathRooted(certPath))
        {
            certPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), certPath));
        }

        if (!File.Exists(certPath))
        {
            throw new FileNotFoundException($"Certificate file not found: {certPath}");
        }

        if (verbose)
        {
            Console.WriteLine($"Installing development certificate: {certPath}");
        }

        try
        {
            // Check if certificate is already installed (unless force is true)
            if (!force)
            {
                var certName = Path.GetFileNameWithoutExtension(certPath);
                var checkCommand = $"Get-ChildItem -Path 'Cert:\\LocalMachine\\TrustedPeople' | Where-Object {{ $_.Subject -like '*{certName}*' }}";

                try
                {
                    var (_, result) = await _powerShellService.RunCommandAsync(checkCommand, verbose: false, cancellationToken: cancellationToken);

                    if (!string.IsNullOrWhiteSpace(result))
                    {
                        if (verbose)
                        {
                            Console.WriteLine("Certificate appears to already be installed");
                        }
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

            await _powerShellService.RunCommandAsync(installCommand, elevated: true, verbose: verbose, cancellationToken: cancellationToken);

            if (verbose)
            {
                Console.WriteLine("Certificate installed successfully to TrustedPeople store");
            }

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
    /// <param name="verbose">Enable verbose logging</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task SignFileAsync(string filePath, string certificatePath, string? password = "password", string? timestampUrl = null, bool verbose = true, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        if (!File.Exists(certificatePath))
            throw new FileNotFoundException($"Certificate file not found: {certificatePath}");

        var arguments = $@"sign /f ""{certificatePath}"" /p ""{password}"" /fd SHA256";

        if (!string.IsNullOrWhiteSpace(timestampUrl))
        {
            arguments += $@" /tr ""{timestampUrl}"" /td SHA256";
        }

        arguments += $@" ""{filePath}""";

        if (verbose)
        {
            Console.WriteLine($"Signing file: {filePath}");
        }

        try
        {
            await _buildToolsService.RunBuildToolAsync("signtool.exe", arguments, verbose, cancellationToken: cancellationToken);

            if (verbose)
            {
                Console.WriteLine("File signed successfully");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to sign file: {ex.Message}", ex);
        }
    }
}
