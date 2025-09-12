using Avalonia.Controls;
using System;
using System.Diagnostics;
using System.IO;
using Avalonia.Platform.Storage;
using Avalonia.Input;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO.Compression;
using subtitles_maker.utility;
using Avalonia.Threading;

namespace subtitles_maker
{
    public partial class MainWindow : Window
    {
        private static readonly string[] SupportedAudioExtensions = 
        {
            ".mp3", ".mp4", ".wav", ".m4a", ".aac", ".flac", ".ogg", ".wma", ".avi", ".mkv", ".mov", ".webm"
        };

        private static readonly string[] SupportedArchiveExtensions = 
        {
            ".zip", ".rar", ".7z"
        };

        public MainWindow()
        {
            InitializeComponent();
            InitializeWhisperOnStartup();

            if (ChooseNewModelButton != null)
                ChooseNewModelButton.Click += ChooseNewModelButton_Click;

            if (OpenModelFolderButton != null)
                OpenModelFolderButton.Click += OpenModelFolderButton_Click;

            if (ChooseNewOutputFolderButton != null)
                ChooseNewOutputFolderButton.Click += ChooseNewOutputFolderButton_Click;

            if (OpenOutputFolderButton != null)
                OpenOutputFolderButton.Click += OpenOutputFolderButton_Click;

            SetupDropZone();
        }

        private void InitializeWhisperOnStartup()
        {
            try
            {
                bool whisperReady = InitializeWhisper.EnsureWhisperExists();
                if (whisperReady)
                {
                    LogToTerminal("✓ Whisper initialized successfully");
                }
                else
                {
                    LogToTerminal("✗ Failed to initialize Whisper - check if whisper-cli.exe exists in project/whisper folder");
                }
            }
            catch (Exception ex)
            {
                LogToTerminal($"Error initializing Whisper: {ex.Message}");
            }
        }

        private void SetupDropZone()
        {
            var dropZone = this.FindControl<Border>("DropZone");
            if (dropZone != null)
            {
                DragDrop.SetAllowDrop(dropZone, true);
                dropZone.AddHandler(DragDrop.DragOverEvent, DropZone_DragOver);
                dropZone.AddHandler(DragDrop.DropEvent, DropZone_Drop);
            }
        }

        private void DropZone_DragOver(object? sender, DragEventArgs e)
        {
            if (e.Data.Contains(DataFormats.Files))
            {
                var files = e.Data.GetFiles();
                if (files != null && files.Any(f => IsValidFile(f.Name) || Directory.Exists(f.Path.LocalPath)))
                    e.DragEffects = DragDropEffects.Copy;
                else
                    e.DragEffects = DragDropEffects.None;
            }
            else
                e.DragEffects = DragDropEffects.None;
        }

        private bool IsValidFile(string fileName)
        {
            string extension = Path.GetExtension(fileName).ToLowerInvariant();
            return SupportedAudioExtensions.Contains(extension) || SupportedArchiveExtensions.Contains(extension);
        }

        private async void DropZone_Drop(object? sender, DragEventArgs e)
        {
            try
            {
                if (e.Data.Contains(DataFormats.Files))
                {
                    var files = e.Data.GetFiles();
                    if (files != null)
                    {
                        var audioFiles = new List<string>();

                        foreach (var file in files)
                        {
                            var fileName = file.Name;
                            var filePath = file.Path.LocalPath;

                            LogToTerminal($"Processing: {fileName}");

                            if (Directory.Exists(filePath))
                            {
                                var folderAudioFiles = await ProcessFolder(filePath);
                                audioFiles.AddRange(folderAudioFiles);
                            }
                            else if (SupportedAudioExtensions.Contains(Path.GetExtension(fileName).ToLowerInvariant()))
                            {
                                audioFiles.Add(filePath);
                            }
                            else if (SupportedArchiveExtensions.Contains(Path.GetExtension(fileName).ToLowerInvariant()))
                            {
                                var extractedFiles = await ProcessArchive(filePath);
                                audioFiles.AddRange(extractedFiles);
                            }
                            else
                            {
                                LogToTerminal($"Skipped '{fileName}' - Unsupported file type");
                            }
                        }

                        if (audioFiles.Count > 0)
                        {
                            await ProcessAudioFiles(audioFiles);
                        }
                        else
                        {
                            LogToTerminal("No supported audio files found");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogToTerminal($"Error processing dropped files: {ex.Message}");
            }
        }

        private async Task<List<string>> ProcessFolder(string folderPath)
        {
            var audioFiles = new List<string>();
            
            try
            {
                var files = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);
                
                foreach (var file in files)
                {
                    string extension = Path.GetExtension(file).ToLowerInvariant();
                    if (SupportedAudioExtensions.Contains(extension))
                    {
                        audioFiles.Add(file);
                    }
                }
                
                LogToTerminal($"Found {audioFiles.Count} audio files in folder");
            }
            catch (Exception ex)
            {
                LogToTerminal($"Error processing folder: {ex.Message}");
            }

            return audioFiles;
        }

        private async Task<List<string>> ProcessArchive(string archivePath)
        {
            var audioFiles = new List<string>();
            
            try
            {
                string extractPath = Path.Combine(Path.GetTempPath(), "subtitles_maker_extract", Path.GetFileNameWithoutExtension(archivePath));
                
                if (Directory.Exists(extractPath))
                    Directory.Delete(extractPath, true);
                Directory.CreateDirectory(extractPath);

                string extension = Path.GetExtension(archivePath).ToLowerInvariant();
                
                if (extension == ".zip")
                {
                    ZipFile.ExtractToDirectory(archivePath, extractPath);
                    LogToTerminal($"Extracted ZIP archive to: {extractPath}");
                }
                else
                {
                    LogToTerminal($"Archive format {extension} requires additional extraction tools - skipping");
                    return audioFiles;
                }

                var extractedAudioFiles = await ProcessFolder(extractPath);
                audioFiles.AddRange(extractedAudioFiles);
            }
            catch (Exception ex)
            {
                LogToTerminal($"Error extracting archive: {ex.Message}");
            }

            return audioFiles;
        }

         private async Task ProcessAudioFiles(List<string> audioFiles)
        {
            string modelPath = ModelPathTextBox?.Text ?? "";
            string outputPath = OutputPathTextBox?.Text ?? "";

            if (string.IsNullOrEmpty(modelPath) || !File.Exists(modelPath))
            {
                LogToTerminal("Error: Invalid model path. Please select a valid model file.");
                return;
            }

            if (string.IsNullOrEmpty(outputPath) || !Directory.Exists(outputPath))
            {
                LogToTerminal("Error: Invalid output path. Please select a valid output folder.");
                return;
            }

            string whisperExe = InitializeWhisper.GetWhisperExecutablePath();
            
            if (!InitializeWhisper.IsWhisperAvailable())
            {
                LogToTerminal($"Error: Whisper executable not found at: {whisperExe}");
                LogToTerminal("Make sure whisper-cli.exe exists in the project/whisper folder");
                return;
            }

            LogToTerminal($"Starting transcription of {audioFiles.Count} files...");

            foreach (string audioFile in audioFiles)
            {
                await TranscribeAudioFile(audioFile, whisperExe, modelPath, outputPath);
            }

            LogToTerminal("Transcription completed!");
        }

        private async Task TranscribeAudioFile(string audioFilePath, string whisperExe, string modelPath, string outputPath)
        {
            try
            {
                string fileName = Path.GetFileNameWithoutExtension(audioFilePath);
                string outputBase = Path.Combine(outputPath, fileName);

                LogToTerminal($"Transcribing: {Path.GetFileName(audioFilePath)}");

                string arguments = $"-m \"{modelPath}\" -f \"{audioFilePath}\" -of \"{outputBase}\" --output-txt --output-srt";

                var processInfo = new ProcessStartInfo
                {
                    FileName = whisperExe,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(whisperExe)
                };

                using var process = new Process { StartInfo = processInfo };
                
                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        Dispatcher.UIThread.InvokeAsync(() => LogToTerminal($"Whisper: {e.Data}"));
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        Dispatcher.UIThread.InvokeAsync(() => LogToTerminal($"Whisper Error: {e.Data}"));
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    LogToTerminal($"✓ Successfully transcribed: {Path.GetFileName(audioFilePath)}");
                    
                    string txtFile = outputBase + ".txt";
                    string srtFile = outputBase + ".srt";
                    
                    if (File.Exists(txtFile))
                        LogToTerminal($"  Created: {Path.GetFileName(txtFile)}");
                    if (File.Exists(srtFile))
                        LogToTerminal($"  Created: {Path.GetFileName(srtFile)}");
                }
                else
                {
                    LogToTerminal($"✗ Failed to transcribe: {Path.GetFileName(audioFilePath)} (Exit code: {process.ExitCode})");
                }
            }
            catch (Exception ex)
            {
                LogToTerminal($"Error transcribing {Path.GetFileName(audioFilePath)}: {ex.Message}");
            }
        }

        private async void ChooseNewModelButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            try
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel == null)
                {
                    LogToTerminal("Error: Unable to get top level window");
                    return;
                }
                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select Model File",
                    AllowMultiple = false,
                    FileTypeFilter =
                    [
                        new FilePickerFileType("Model Files")
                        {
                            Patterns = [ "*.bin", "*.ggml" ]
                        },
                        new FilePickerFileType("All Files")
                        {
                            Patterns = [ "*.*" ]
                        }
                    ]
                });

                if (files.Count > 0)
                {
                    var filePath = files[0].Path.LocalPath;
                    ModelPathTextBox.Text = filePath;
                    LogToTerminal($"Model path set to: {filePath}");
                }
            }
            catch (Exception ex)
            {
                LogToTerminal($"Error opening file dialog: {ex.Message}");
            }
        }

        private void OpenModelFolderButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            try
            {
                string? modelPath = ModelPathTextBox.Text;
                if (!string.IsNullOrEmpty(modelPath) && File.Exists(modelPath))
                {
                    string directory = Path.GetDirectoryName(modelPath) ?? string.Empty;
                    if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                    {
                        OpenFolder(directory);
                        LogToTerminal($"Opened folder: {directory}");
                    }
                    else
                        LogToTerminal("Invalid model directory path.");
                }
                else
                    LogToTerminal("Model file path is not valid.");
            }
            catch (Exception ex)
            {
                LogToTerminal($"Error opening model folder: {ex.Message}");
            }
        }

        private async void ChooseNewOutputFolderButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            try
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel == null)
                {
                    LogToTerminal("Error: Unable to get top level window");
                    return;
                }
                
                var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select Output Folder",
                    AllowMultiple = false
                });

                if (folders.Count > 0)
                {
                    var folderPath = folders[0].Path.LocalPath;
                    OutputPathTextBox.Text = folderPath;
                    LogToTerminal($"Output path set to: {folderPath}");
                }
            }
            catch (Exception ex)
            {
                LogToTerminal($"Error opening folder dialog: {ex.Message}");
            }
        }

        private void OpenOutputFolderButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            try
            {
                string? outputPath = OutputPathTextBox.Text;
                if (!string.IsNullOrEmpty(outputPath) && Directory.Exists(outputPath))
                {
                    OpenFolder(outputPath);
                    LogToTerminal($"Opened folder: {outputPath}");
                }
                else
                    LogToTerminal("Output folder path is not valid.");
            }
            catch (Exception ex)
            {
                LogToTerminal($"Error opening output folder: {ex.Message}");
            }
        }

        private void OpenFolder(string folderPath)
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = folderPath,
                    UseShellExecute = true
                });
            }
            else if (OperatingSystem.IsLinux())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = folderPath,
                    UseShellExecute = true
                });
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "open",
                    Arguments = folderPath,
                    UseShellExecute = true
                });
            }
        }

        private void LogToTerminal(string message)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.InvokeAsync(() => LogToTerminal(message));
                return;
            }

            if (TerminalLogTextBox != null)
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss");
                string logEntry = $"[{timestamp}] {message}";
                
                if (string.IsNullOrEmpty(TerminalLogTextBox.Text))
                    TerminalLogTextBox.Text = logEntry;
                else
                    TerminalLogTextBox.Text += Environment.NewLine + logEntry;
                
                TerminalLogTextBox.CaretIndex = TerminalLogTextBox.Text.Length;
            }
        }
    }
}