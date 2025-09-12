using Avalonia.Controls;
using System;
using System.Diagnostics;
using System.IO;
using Avalonia.Platform.Storage;
using Avalonia.Input;
using System.Linq;
using Avalonia.Threading;
using subtitles_maker.services;

namespace subtitles_maker
{
    public partial class MainWindow : Window
    {
        private readonly WhisperService _whisperService;
        private readonly FileProcessingService _fileProcessingService;

        public MainWindow()
        {
            InitializeComponent();
            
            _whisperService = new WhisperService();
            _fileProcessingService = new FileProcessingService();
            
            _whisperService.OnLogMessage += message => Dispatcher.UIThread.InvokeAsync(() => LogToTerminal(message));
            _fileProcessingService.OnLogMessage += message => Dispatcher.UIThread.InvokeAsync(() => LogToTerminal(message));
            
            InitializeApplication();
            SetupEventHandlers();
            SetupDropZone();
        }

        private void InitializeApplication()
        {
            _whisperService.EnsureWhisperInitialized();
        }

        private void SetupEventHandlers()
        {
            if (ChooseNewModelButton != null)
                ChooseNewModelButton.Click += ChooseNewModelButton_Click;

            if (OpenModelFolderButton != null)
                OpenModelFolderButton.Click += OpenModelFolderButton_Click;

            if (ChooseNewOutputFolderButton != null)
                ChooseNewOutputFolderButton.Click += ChooseNewOutputFolderButton_Click;

            if (OpenOutputFolderButton != null)
                OpenOutputFolderButton.Click += OpenOutputFolderButton_Click;
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
                if (files != null && files.Any(f => _fileProcessingService.IsValidFile(f.Name) || Directory.Exists(f.Path.LocalPath)))
                    e.DragEffects = DragDropEffects.Copy;
                else
                    e.DragEffects = DragDropEffects.None;
            }
            else
                e.DragEffects = DragDropEffects.None;
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
                        var filePaths = files.Select(f => f.Path.LocalPath).ToList();
                        var audioFiles = await _fileProcessingService.ProcessDroppedFiles(filePaths);

                        if (audioFiles.Count > 0)
                        {
                            string modelPath = ModelPathTextBox?.Text ?? "";
                            string outputPath = OutputPathTextBox?.Text ?? "";
                            string selectedLanguage = GetSelectedLanguage();

                            await _whisperService.TranscribeAudioFiles(audioFiles, modelPath, outputPath, selectedLanguage);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogToTerminal($"Error processing dropped files: {ex.Message}");
            }
        }

        private string GetSelectedLanguage()
        {
            var languageComboBox = this.FindControl<ComboBox>("LanguageComboBox");
            if (languageComboBox?.SelectedItem is ComboBoxItem selectedItem)
            {
                return selectedItem.Content?.ToString() ?? "Japanese";
            }
            return "Japanese"; 
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
                    if (ModelPathTextBox != null)
                    {
                        ModelPathTextBox.Text = filePath;
                        LogToTerminal($"Model path set to: {filePath}");
                    }
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
                string? modelPath = ModelPathTextBox?.Text;
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
                    if (OutputPathTextBox != null)
                    {
                        OutputPathTextBox.Text = folderPath;
                        LogToTerminal($"Output path set to: {folderPath}");
                    }
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
                string? outputPath = OutputPathTextBox?.Text;
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