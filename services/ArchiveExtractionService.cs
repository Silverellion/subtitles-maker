using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace subtitles_maker.services
{
    public class ArchiveExtractionService
    {
        private static readonly string[] SupportedExtensions = [".zip", ".rar", ".7z"];
        private readonly string _tempBasePath;

        public event Action<string>? OnLogMessage;

        public ArchiveExtractionService()
        {
            _tempBasePath = Path.Combine(Path.GetTempPath(), "subtitles_maker_extract");
            EnsureTempDirectoryExists();
        }

        private void EnsureTempDirectoryExists()
        {
            try
            {
                if (!Directory.Exists(_tempBasePath))
                    Directory.CreateDirectory(_tempBasePath);
            }
            catch (Exception ex)
            {
                OnLogMessage?.Invoke($"Warning: Could not create temp directory: {ex.Message}");
            }
        }

        public bool CanExtract(string archivePath)
        {
            string extension = Path.GetExtension(archivePath).ToLowerInvariant();
            return SupportedExtensions.Contains(extension);
        }

        public async Task<string?> ExtractArchive(string archivePath)
        {
            if (!File.Exists(archivePath))
            {
                OnLogMessage?.Invoke($"✗ Archive file not found: {Path.GetFileName(archivePath)}");
                return null;
            }

            string extension = Path.GetExtension(archivePath).ToLowerInvariant();
            string extractPath = CreateExtractionDirectory(archivePath);

            OnLogMessage?.Invoke($"Created extraction directory: {extractPath}");

            bool success = extension switch
            {
                ".zip" => await ExtractZip(archivePath, extractPath),
                ".rar" => await ExtractRar(archivePath, extractPath),
                ".7z" => await Extract7z(archivePath, extractPath),
                _ => false
            };

            if (success)
            {
                OnLogMessage?.Invoke($"✓ Successfully extracted archive: {Path.GetFileName(archivePath)}");
                return extractPath;
            }
            else
            {
                CleanupExtractedFiles(extractPath);
                return null;
            }
        }

        private string CreateExtractionDirectory(string archivePath)
        {
            string archiveName = Path.GetFileNameWithoutExtension(archivePath);
            string extractPath = Path.Combine(_tempBasePath, archiveName);
            
            if (Directory.Exists(extractPath))
                Directory.Delete(extractPath, true);
            
            Directory.CreateDirectory(extractPath);
            return extractPath;
        }

        private async Task<bool> ExtractZip(string archivePath, string extractPath)
        {
            try
            {
                if (await Try7Zip(archivePath, extractPath, "x"))
                    return true;

                if (await TryWinRar(archivePath, extractPath))
                    return true;

                System.IO.Compression.ZipFile.ExtractToDirectory(archivePath, extractPath);
                OnLogMessage?.Invoke($"✓ Extracted ZIP using built-in .NET library");
                return true;
            }
            catch (Exception ex)
            {
                OnLogMessage?.Invoke($"✗ Failed to extract ZIP archive: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ExtractRar(string archivePath, string extractPath)
        {
            if (await TryWinRar(archivePath, extractPath))
                return true;

            if (await Try7Zip(archivePath, extractPath, "x"))
                return true;

            OnLogMessage?.Invoke($"✗ No extraction programs found for RAR files");
            OnLogMessage?.Invoke($"  Please install WinRAR or 7-Zip to extract RAR archives");
            return false;
        }

        private async Task<bool> Extract7z(string archivePath, string extractPath)
        {
            if (await Try7Zip(archivePath, extractPath, "x"))
                return true;

            if (await TryWinRar(archivePath, extractPath))
                return true;

            OnLogMessage?.Invoke($"✗ No extraction programs found for 7Z files");
            OnLogMessage?.Invoke($"  Please install 7-Zip or WinRAR to extract 7Z archives");
            return false;
        }

        private async Task<bool> Try7Zip(string archivePath, string extractPath, string operation)
        {
            try
            {
                string[] possiblePaths = [
                    "7z.exe",
                    @"C:\Program Files\7-Zip\7z.exe",
                    @"C:\Program Files (x86)\7-Zip\7z.exe"
                ];

                string? sevenZipPath = null;
                foreach (string path in possiblePaths)
                {
                    if (await IsExecutableAvailable(path))
                    {
                        sevenZipPath = path;
                        break;
                    }
                }

                if (sevenZipPath == null)
                    return false;

                string arguments = $"{operation} \"{archivePath}\" -o\"{extractPath}\" -y";
                
                var processInfo = new ProcessStartInfo
                {
                    FileName = sevenZipPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = processInfo };
                process.Start();
                
                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();
                
                await process.WaitForExitAsync();
                
                if (process.ExitCode == 0)
                {
                    OnLogMessage?.Invoke($"✓ Extracted using 7-Zip");
                    return true;
                }
                else
                {
                    OnLogMessage?.Invoke($"✗ 7-Zip extraction failed: {error}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                OnLogMessage?.Invoke($"✗ Error running 7-Zip: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> TryWinRar(string archivePath, string extractPath)
        {
            try
            {
                string[] possiblePaths = [
                    "unrar.exe", 
                    "winrar.exe",
                    @"C:\Program Files\WinRAR\unrar.exe",
                    @"C:\Program Files (x86)\WinRAR\unrar.exe",
                    @"C:\Program Files\WinRAR\winrar.exe",
                    @"C:\Program Files (x86)\WinRAR\winrar.exe"
                ];

                string? winrarPath = null;
                string command = "";
                
                foreach (string path in possiblePaths)
                {
                    if (await IsExecutableAvailable(path))
                    {
                        winrarPath = path;
                        command = path.Contains("unrar") ? "x" : "x";
                        break;
                    }
                }

                if (winrarPath == null)
                    return false;

                string arguments = $"{command} \"{archivePath}\" \"{extractPath}\\\" -y";
                
                var processInfo = new ProcessStartInfo
                {
                    FileName = winrarPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = processInfo };
                process.Start();
                
                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();
                
                await process.WaitForExitAsync();
                
                if (process.ExitCode == 0)
                {
                    OnLogMessage?.Invoke($"✓ Extracted using WinRAR");
                    return true;
                }
                else
                {
                    OnLogMessage?.Invoke($"✗ WinRAR extraction failed: {error}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                OnLogMessage?.Invoke($"✗ Error running WinRAR: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> IsExecutableAvailable(string executablePath)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = processInfo };
                process.Start();
                await process.WaitForExitAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void CleanupExtractedFiles(string extractPath)
        {
            try
            {
                if (Directory.Exists(extractPath))
                {
                    Directory.Delete(extractPath, true);
                    OnLogMessage?.Invoke($"Cleaned up extracted files: {extractPath}");
                }
            }
            catch (Exception ex)
            {
                OnLogMessage?.Invoke($"Warning: Could not cleanup directory {extractPath}: {ex.Message}");
            }
        }

        public void CleanupAllTempExtractions()
        {
            try
            {
                if (Directory.Exists(_tempBasePath))
                {
                    var directories = Directory.GetDirectories(_tempBasePath);
                    foreach (string dir in directories)
                    {
                        try
                        {
                            Directory.Delete(dir, true);
                        }
                        catch (Exception ex)
                        {
                            OnLogMessage?.Invoke($"Warning: Could not cleanup directory {dir}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OnLogMessage?.Invoke($"Warning: Error during temp cleanup: {ex.Message}");
            }
        }
    }
}