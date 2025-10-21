using Avalonia.Controls;
using System;
using System.Diagnostics;
using System.IO;
using Avalonia.Platform.Storage;
using Avalonia.Input;
using System.Linq;
using Avalonia.Threading;
using subtitles_maker.services;
using subtitles_maker.Services;
using subtitles_maker.Views.Models;

namespace subtitles_maker.Views.Home
{
    public partial class HomeView : UserControl
    {
        private readonly WhisperService _whisperService;
        private readonly FileProcessingService _fileProcessingService;

        public HomeView()
        {
            InitializeComponent();
            
            _whisperService = new WhisperService();
            _fileProcessingService = new FileProcessingService();
            
            _whisperService.OnLogMessage += message => Dispatcher.UIThread.InvokeAsync(() => LogToTerminal(message));
            _fileProcessingService.OnLogMessage += message => Dispatcher.UIThread.InvokeAsync(() => LogToTerminal(message));
            
            InitializeApplication();
            SetupEventHandlers();
            SetupDropZone();
            LoadConfigAndPopulateUI();
        }

        private void InitializeApplication()
        {
            _whisperService.EnsureWhisperInitialized();
        }

        private void SetupEventHandlers()
        {
            if (ChooseNewOutputFolderButton != null)
                ChooseNewOutputFolderButton.Click += ChooseNewOutputFolderButton_Click;

            if (OpenOutputFolderButton != null)
                OpenOutputFolderButton.Click += OpenOutputFolderButton_Click;

            if (DropZoneButton != null)
                DropZoneButton.Click += DropZoneButton_Click;

            if (OutputPathTextBox != null)
                OutputPathTextBox.AddHandler(TextBox.TextChangedEvent, (EventHandler<TextChangedEventArgs>)OutputPathTextBox_TextChanged);

            var modelCombo = this.FindControl<ComboBox>("ModelPathComboBox");
            if (modelCombo != null)
                modelCombo.SelectionChanged += ModelCombo_SelectionChanged;

            DetachedFromVisualTree += (s, e) =>
            {
                PersistConfig();
            };
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
                            string modelPath = this.FindControl<ComboBox>("ModelPathComboBox")?.SelectedItem as string ?? string.Empty;
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
        
        private async void DropZoneButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
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
                    Title = "Select Audio Files",
                    AllowMultiple = true,
                    FileTypeFilter =
                    [
                        new FilePickerFileType("Audio Files")
                        {
                            Patterns = [ "*.mp3", "*.mp4", "*.wav", "*.m4a", "*.aac", "*.flac", "*.ogg", "*.wma", "*.avi", "*.mkv", "*.mov", "*.webm" ]
                        },
                        new FilePickerFileType("Archive Files")
                        {
                            Patterns = [ "*.zip", "*.rar", "*.7z" ]
                        },
                        new FilePickerFileType("All Files")
                        {
                            Patterns = [ "*.*" ]
                        }
                    ]
                });

                if (files.Count > 0)
                {
                    var filePaths = files.Select(f => f.Path.LocalPath).ToList();
                    LogToTerminal($"Selected {filePaths.Count} file(s) for processing");
                    
                    var audioFiles = await _fileProcessingService.ProcessDroppedFiles(filePaths);

                    if (audioFiles.Count > 0)
                    {
                        string modelPath = this.FindControl<ComboBox>("ModelPathComboBox")?.SelectedItem as string ?? string.Empty;
                        string outputPath = OutputPathTextBox?.Text ?? "";
                        string selectedLanguage = GetSelectedLanguage();

                        await _whisperService.TranscribeAudioFiles(audioFiles, modelPath, outputPath, selectedLanguage);
                    }
                }
            }
            catch (Exception ex)
            {
                LogToTerminal($"Error selecting files: {ex.Message}");
            }
        }

        private string GetSelectedLanguage()
        {
            var languageComboBox = this.FindControl<ComboBox>("LanguageComboBox");
            if (languageComboBox?.SelectedItem is ComboBoxItem selectedItem)
                return selectedItem.Content?.ToString() ?? "English";
            return "English";
        }

        // Removed model picker buttons and handlers

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
                        PersistConfig();
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

        private void LoadConfigAndPopulateUI()
        {
            // Populate model list from downloaded models directory
            try
            {
                var modelsDir = @"C:\subtitles-maker\models";
                var modelCombo = this.FindControl<ComboBox>("ModelPathComboBox");
                if (modelCombo != null)
                {
                    var list = new System.Collections.Generic.List<string>();
                    const string Placeholder = "Select model";
                    const string DownloadSentinel = "Download model";
                    list.Add(Placeholder);
                    if (Directory.Exists(modelsDir))
                    {
                        var modelFiles = Directory.GetFiles(modelsDir, "*.bin", SearchOption.TopDirectoryOnly).ToList();
                        modelFiles.Sort(StringComparer.OrdinalIgnoreCase);
                        list.AddRange(modelFiles);
                    }
                    // Always keep 'Download model' as last item
                    list.Add(DownloadSentinel);
                    modelCombo.ItemsSource = list;
                    modelCombo.SelectedIndex = 0;
                }
            }
            catch { }

            var cfg = ConfigService.Load();
            var modelCombo2 = this.FindControl<ComboBox>("ModelPathComboBox");
            var outputTb = this.FindControl<TextBox>("OutputPathTextBox");

            if (modelCombo2 != null)
            {
                if (!string.IsNullOrWhiteSpace(cfg.ModelPath))
                {
                    var existing = (modelCombo2.Items as System.Collections.IEnumerable)?.Cast<object>().Select(x => x?.ToString() ?? string.Empty).ToList() ?? new();
                    if (!existing.Contains(cfg.ModelPath))
                    {
                        existing.Add(cfg.ModelPath);
                        modelCombo2.ItemsSource = existing;
                    }
                    modelCombo2.SelectedItem = cfg.ModelPath;
                }
                else
                {
                    modelCombo2.SelectedIndex = (modelCombo2.Items as System.Collections.IList)?.Count > 0 ? 0 : -1;
                }
            }

            if (outputTb != null)
            {
                outputTb.Text = string.IsNullOrWhiteSpace(cfg.OutputPath) ? string.Empty : cfg.OutputPath;
            }
        }

        private void PersistConfig()
        {
            var modelCombo = this.FindControl<ComboBox>("ModelPathComboBox");
            var outputTb = this.FindControl<TextBox>("OutputPathTextBox");

            var cfg = ConfigService.Load();
            var selected = modelCombo?.SelectedItem as string;
            if (string.Equals(selected, "Select model", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(selected, "Download model", StringComparison.OrdinalIgnoreCase))
            {
                cfg.ModelPath = null;
            }
            else
            {
                cfg.ModelPath = selected;
            }
            cfg.OutputPath = outputTb?.Text;
            ConfigService.Save(cfg);
        }

        private void ModelCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var combo = sender as ComboBox;
            var value = combo?.SelectedItem as string;
            if (string.Equals(value, "Download model", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    if (VisualRoot is MainWindow mw)
                    {
                        mw.NavigateToModels();
                    }
                }
                catch { }
                return;
            }
            PersistConfig();
        }

        private void OutputPathTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            PersistConfig();
        }
    }
}