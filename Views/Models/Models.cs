using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace subtitles_maker.Views.Models
{
    public partial class ModelsView : UserControl
    {
        private const string HuggingFaceApiUrl = "https://huggingface.co/api/models/ggerganov/whisper.cpp/tree/main";
        private const string ModelsDirectory = @"C:\subtitles-maker\models";
        private readonly Dictionary<string, DownloadProgress> _activeDownloads = new();
        private List<WhisperModel> _availableModels = new();

        public ModelsView()
        {
            InitializeComponent();
            Directory.CreateDirectory(ModelsDirectory);
            LoadModelsFromApi();
        }

        private async void LoadModelsFromApi()
        {
            var stackPanel = this.FindControl<StackPanel>("ModelsStackPanel");
            if (stackPanel == null) return;

            stackPanel.Children.Clear();

            // Show loading indicator
            var loadingText = new TextBlock
            {
                Text = "Loading models from Hugging Face...",
                FontSize = 16,
                Foreground = Brushes.White,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Margin = new Avalonia.Thickness(0, 50, 0, 0)
            };
            stackPanel.Children.Add(loadingText);

            try
            {
                _availableModels = await FetchModelsFromHuggingFace();
                
                stackPanel.Children.Clear();

                if (_availableModels.Count == 0)
                {
                    var noModelsText = new TextBlock
                    {
                        Text = "No .bin models found in the repository.",
                        FontSize = 16,
                        Foreground = Brushes.White,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        Margin = new Avalonia.Thickness(0, 50, 0, 0)
                    };
                    stackPanel.Children.Add(noModelsText);
                    return;
                }

                foreach (var model in _availableModels.OrderBy(m => m.FileName))
                {
                    var modelCard = CreateModelCard(model);
                    stackPanel.Children.Add(modelCard);
                }
            }
            catch (Exception ex)
            {
                stackPanel.Children.Clear();
                var errorText = new TextBlock
                {
                    Text = $"Error loading models: {ex.Message}",
                    FontSize = 14,
                    Foreground = Brushes.Red,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    Margin = new Avalonia.Thickness(0, 50, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                };
                stackPanel.Children.Add(errorText);
            }
        }

        private async Task<List<WhisperModel>> FetchModelsFromHuggingFace()
        {
            var models = new List<WhisperModel>();

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "SubtitlesMaker/1.0");

            var response = await client.GetAsync(HuggingFaceApiUrl);
            response.EnsureSuccessStatusCode();

            var jsonString = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(jsonString);

            foreach (var item in jsonDoc.RootElement.EnumerateArray())
            {
                if (item.TryGetProperty("path", out var pathElement) &&
                    item.TryGetProperty("type", out var typeElement))
                {
                    string path = pathElement.GetString() ?? "";
                    string type = typeElement.GetString() ?? "";

                    // Only include .bin files
                    if (type == "file" && path.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
                    {
                        string fileName = Path.GetFileName(path);
                        long size = 0;

                        if (item.TryGetProperty("size", out var sizeElement))
                            size = sizeElement.GetInt64();

                        string displayName = GetDisplayName(fileName);
                        string sizeString = FormatFileSize(size);
                        string downloadUrl = $"https://huggingface.co/ggerganov/whisper.cpp/resolve/main/{fileName}";

                        models.Add(new WhisperModel(fileName, displayName, sizeString, downloadUrl));
                    }
                }
            }

            return models;
        }

        private string GetDisplayName(string fileName)
        {
            // Remove "ggml-" prefix and ".bin" extension
            string name = fileName.Replace("ggml-", "", StringComparison.OrdinalIgnoreCase)
                                 .Replace(".bin", "", StringComparison.OrdinalIgnoreCase);

            if (string.IsNullOrEmpty(name))
                return fileName;

            // Replace hyphens with spaces and capitalize
            name = name.Replace("-", " ");
            var words = name.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                    words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1);
            }

            return string.Join(" ", words);
        }

        private string FormatFileSize(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            else if (bytes < 1024 * 1024)
                return $"{bytes / 1024.0:F2} KB";
            else if (bytes < 1024 * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024.0):F2} MB";
            else
                return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }

        private Border CreateModelCard(WhisperModel model)
        {
            string modelPath = Path.Combine(ModelsDirectory, model.FileName);
            bool isDownloaded = File.Exists(modelPath);
            var downloadButton = new Button
            {
                Width = 50,
                Height = 50,
                Background = Brushes.Transparent,
                BorderThickness = new Avalonia.Thickness(0),
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                Tag = model,
            };
            ToolTip.SetTip(downloadButton, isDownloaded ? "Model already downloaded" : $"Download {model.DisplayName}");

            var downloadIcon = new Image
            {
                Source = new Bitmap(AssetLoader.Open(new Uri(
                    isDownloaded 
                        ? "avares://subtitles-maker/assets/icons/check-75.png"
                        : "avares://subtitles-maker/assets/icons/download-75.png")))
            };
            downloadButton.Content = downloadIcon;
            downloadButton.Click += DownloadButton_Click;

            var openFolderButton = new Button
            {
                Width = 50,
                Height = 50,
                Background = Brushes.Transparent,
                BorderThickness = new Avalonia.Thickness(0),
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                Content = new Image
                {
                    Source = new Bitmap(AssetLoader.Open(new Uri("avares://subtitles-maker/assets/icons/folder-open-75.png"))),
                },
            };
            ToolTip.SetTip(openFolderButton, "Open models folder");
            openFolderButton.Click += (s, e) => OpenModelFolder();

            var openInNewButton = new Button
            {
                Width = 50,
                Height = 50,
                Background = Brushes.Transparent,
                BorderThickness = new Avalonia.Thickness(0),
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                Content = new Image
                {
                    Source = new Bitmap(AssetLoader.Open(new Uri("avares://subtitles-maker/assets/icons/open-in-new-75.png"))),
                },
            };
            ToolTip.SetTip(openInNewButton, "View on Hugging Face");

            openInNewButton.Click += (s, e) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = $"https://huggingface.co/ggerganov/whisper.cpp/blob/main/{model.FileName}",
                        UseShellExecute = true
                    });
                }
                catch { }
            };

            var progressBar = new ProgressBar
            {
                Height = 4,
                Margin = new Avalonia.Thickness(0, 10, 0, 0),
                IsVisible = false,
                Foreground = Brushes.Green,
                Maximum = 100,
                Value = 0
            };

            var progressText = new TextBlock
            {
                Foreground = Brushes.White,
                FontSize = 12,
                Margin = new Avalonia.Thickness(0, 5, 0, 0),
                IsVisible = false
            };

            var buttonsPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Children = { downloadButton, openFolderButton, openInNewButton }
            };

            var contentGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                Children =
                {
                    new StackPanel
                    {
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = model.DisplayName,
                                FontSize = 16,
                                FontWeight = Avalonia.Media.FontWeight.Bold,
                                Foreground = Brushes.White
                            },
                            new TextBlock
                            {
                                Text = model.FileName,
                                FontSize = 12,
                                Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                                Margin = new Avalonia.Thickness(0, 2, 0, 0)
                            },
                            new TextBlock
                            {
                                Text = $"Size: {model.Size}",
                                FontSize = 12,
                                Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                                Margin = new Avalonia.Thickness(0, 2, 0, 0)
                            }
                        }
                    },
                    buttonsPanel
                }
            };

            Grid.SetColumn(buttonsPanel, 1);

            var mainPanel = new StackPanel
            {
                Children = { contentGrid, progressBar, progressText }
            };

            _activeDownloads[model.FileName] = new DownloadProgress
            {
                ProgressBar = progressBar,
                ProgressText = progressText,
                DownloadButton = downloadButton,
                OpenFolderButton = openFolderButton,
                DownloadIcon = downloadIcon
            };

            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                BorderThickness = new Avalonia.Thickness(1),
                CornerRadius = new Avalonia.CornerRadius(8),
                Padding = new Avalonia.Thickness(20, 15, 20, 15),
                Margin = new Avalonia.Thickness(0, 0, 0, 10),
                Child = mainPanel
            };
        }

        private async void DownloadButton_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not WhisperModel model)
                return;

            if (!_activeDownloads.TryGetValue(model.FileName, out var progress))
                return;

            progress.ProgressBar.IsVisible = true;
            progress.ProgressText.IsVisible = true;

            try
            {
                await DownloadModel(model, progress);
                
                // Change icon to check mark
                progress.DownloadIcon.Source = new Bitmap(AssetLoader.Open(
                    new Uri("avares://subtitles-maker/assets/icons/check-75.png")));
                
                progress.ProgressBar.IsVisible = false;
                progress.ProgressText.IsVisible = false;
            }
            catch (Exception ex)
            {
                progress.ProgressBar.IsVisible = false;
                progress.ProgressText.Text = $"Error: {ex.Message}";
                progress.ProgressText.Foreground = Brushes.Red;
                progress.ProgressText.IsVisible = true;
                
                await Task.Delay(5000);
                progress.ProgressText.IsVisible = false;
            }
        }

        private async Task DownloadModel(WhisperModel model, DownloadProgress progress)
        {
            string modelPath = Path.Combine(ModelsDirectory, model.FileName);
            
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromHours(2);

            using var response = await client.GetAsync(model.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            var buffer = new byte[8192];
            var totalRead = 0L;

            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(modelPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            int bytesRead;
            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                totalRead += bytesRead;

                if (totalBytes > 0)
                {
                    var percentage = (double)totalRead / totalBytes * 100;
                    var downloadedMB = totalRead / 1024.0 / 1024.0;
                    var totalMB = totalBytes / 1024.0 / 1024.0;

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        progress.ProgressBar.Value = percentage;
                        progress.ProgressText.Text = $"{downloadedMB:F2} MB / {totalMB:F2} MB ({percentage:F1}%)";
                    });
                }
            }
        }

        private void OpenModelFolder()
        {
            if (Directory.Exists(ModelsDirectory))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = ModelsDirectory,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
        }

        private class WhisperModel
        {
            public string FileName { get; }
            public string DisplayName { get; }
            public string Size { get; }
            public string DownloadUrl { get; }

            public WhisperModel(string fileName, string displayName, string size, string downloadUrl)
            {
                FileName = fileName;
                DisplayName = displayName;
                Size = size;
                DownloadUrl = downloadUrl;
            }
        }

        private class DownloadProgress
        {
            public ProgressBar ProgressBar { get; set; } = null!;
            public TextBlock ProgressText { get; set; } = null!;
            public Button DownloadButton { get; set; } = null!;
            public Button OpenFolderButton { get; set; } = null!;
            public Image DownloadIcon { get; set; } = null!;
        }
    }
}