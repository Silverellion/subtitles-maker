using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Diagnostics;

namespace subtitles_maker.services
{
    public class ArchiveExtractionService
    {
        public event Action<string>? OnLogMessage;

        private static readonly Dictionary<string, string> ExtractionTools = new()
        {
            { ".zip", "built-in" },
            { ".rar", "7z" },
            { ".7z", "7z" }
        };

        public bool CanExtract(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            return ExtractionTools.ContainsKey(extension);
        }

        public async Task<string?> ExtractArchive(string archivePath)
        {
            try
            {
                string extension = Path.GetExtension(archivePath).ToLowerInvariant();
                string extractPath = Path.Combine(Path.GetTempPath(), "subtitles_maker_extract", Path.GetFileNameWithoutExtension(archivePath));
                
                if (Directory.Exists(extractPath))
                {
                    Directory.Delete(extractPath, true);
                    OnLogMessage?.Invoke($"Cleaned existing extraction directory: {extractPath}");
                }
                
                Directory.CreateDirectory(extractPath);
                OnLogMessage?.Invoke($"Created extraction directory: {extractPath}");

                bool success = extension switch
                {
                    ".zip" => await ExtractZip(archivePath, extractPath),
                    ".rar" => await ExtractWithSevenZip(archivePath, extractPath),
                    ".7z" => await ExtractWithSevenZip(archivePath, extractPath),
                    _ => false
                };

                if (success)
                {
                    OnLogMessage?.Invoke($"✓ Successfully extracted {extension.ToUpper()} archive");
                    return extractPath;
                }
                else
                {
                    OnLogMessage?.Invoke($"✗ Failed to extract {extension.ToUpper()} archive");
                    CleanupExtractedFiles(extractPath);
                    return null;
                }
            }
            catch (Exception ex)
            {
                OnLogMessage?.Invoke($"✗ Error extracting archive {Path.GetFileName(archivePath)}: {ex.Message}");
                return null;
            }
        }

        private async Task<bool> ExtractZip(string archivePath, string extractPath)
        {
            try
            {
                await Task.Run(() => ZipFile.ExtractToDirectory(archivePath, extractPath));
                OnLogMessage?.Invoke($"Extracted ZIP using built-in .NET extractor");
                return true;
            }
            catch (Exception ex)
            {
                OnLogMessage?.Invoke($"✗ ZIP extraction failed: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ExtractWithSevenZip(string archivePath, string extractPath)
        {
            string sevenZipPath = FindSevenZipExecutable();
            
            if (string.IsNullOrEmpty(sevenZipPath))
            {
                OnLogMessage?.Invoke($"✗ 7-Zip not found. Please install 7-Zip or place 7z.exe in your PATH");
                OnLogMessage?.Invoke($"  Download from: https://www.7-zip.org/");
                return false;
            }

            try
            {
                OnLogMessage?.Invoke($"Using 7-Zip for extraction: {sevenZipPath}");
                // 7z x "archive.rar" -o"output_directory" -y
                string arguments = $"x \"{archivePath}\" -o\"{extractPath}\" -y";

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
                
                string output = "";
                string error = "";
                
                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        output += e.Data + Environment.NewLine;
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        error += e.Data + Environment.NewLine;
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    OnLogMessage?.Invoke($"7-Zip extraction completed successfully");
                    return true;
                }
                else
                {
                    OnLogMessage?.Invoke($"✗ 7-Zip extraction failed (Exit code: {process.ExitCode})");
                    if (!string.IsNullOrEmpty(error))
                        OnLogMessage?.Invoke($"  Error: {error.Trim()}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                OnLogMessage?.Invoke($"✗ Error running 7-Zip: {ex.Message}");
                return false;
            }
        }

        private string FindSevenZipExecutable()
        {
            // Check common locations for 7-Zip
            string[] possiblePaths = {
                "7z.exe",                                                              // In PATH
                "7zip\\7z.exe",                                                        // In project 7zip folder
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "7z.exe"),         // In application directory
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "7zip", "7z.exe"), // In app 7zip folder
                @"C:\Program Files\7-Zip\7z.exe",                                      // Standard installation path
                @"C:\Program Files (x86)\7-Zip\7z.exe",                                // 32-bit installation path
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip", "7z.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "7-Zip", "7z.exe")
            };

            foreach (string path in possiblePaths)
            {
                try
                {
                    if (File.Exists(path))
                        return path;
                    
                    if (path == "7z.exe")
                    {
                        using var process = new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = "7z",
                                Arguments = "",
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                CreateNoWindow = true
                            }
                        };
                        
                        process.Start();
                        process.WaitForExit(3000); // 3 second timeout
                        
                        if (process.ExitCode == 0 || process.ExitCode == 1) // 7z returns 1 for help
                            return "7z";
                    }
                }
                catch (Exception ex)
                {
                    OnLogMessage?.Invoke($"Warning: Could not check path {path}: {ex.Message}");
                }
            }

            return string.Empty;
        }

        public void CleanupExtractedFiles(string? extractPath)
        {
            if (string.IsNullOrEmpty(extractPath) || !Directory.Exists(extractPath))
                return;

            try
            {
                Directory.Delete(extractPath, true);
                OnLogMessage?.Invoke($"Cleaned up extracted files: {extractPath}");
            }
            catch (Exception ex)
            {
                OnLogMessage?.Invoke($"Warning: Could not clean up extracted files {extractPath}: {ex.Message}");
            }
        }

        public void CleanupAllTempExtractions()
        {
            try
            {
                string tempExtractionDir = Path.Combine(Path.GetTempPath(), "subtitles_maker_extract");
                if (Directory.Exists(tempExtractionDir))
                {
                    Directory.Delete(tempExtractionDir, true);
                    OnLogMessage?.Invoke("Cleaned up all temporary extraction files");
                }
            }
            catch (Exception ex)
            {
                OnLogMessage?.Invoke($"Warning: Could not clean up temp extraction directory: {ex.Message}");
            }
        }
    }
}