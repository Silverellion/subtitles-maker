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

namespace subtitles_maker.Views.Models
{
    public partial class ModelsView : UserControl
    {
        private const string HuggingFaceApiUrl = "https://huggingface.co/api/models/ggerganov/whisper.cpp/tree/main";
        private readonly string _modelsDirectory;
        private readonly Dictionary<string, DownloadProgress> _activeDownloads = new();
        private List<WhisperModel> _availableModels = new();

        public ModelsView()
        {
            InitializeComponent();
            
            _modelsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "subtitles-maker", "models");
            Directory.CreateDirectory(_modelsDirectory);
            
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
                Foreground = Avalonia.Media.Brushes.White,
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
                        Foreground = Avalonia.Media.Brushes.White,
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
                    Foreground = Avalonia.Media.Brushes.Red,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    Margin = new Avalonia.Thickness(0, 50, 0, 0),
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
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

            // Capitalize first letter and format nicely
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
            string modelPath = Path.Combine(_modelsDirectory, model.FileName);
            bool isDownloaded = File.Exists(modelPath);

            var downloadButton = new Button
            {
                Content = isDownloaded ? "Downloaded" : "Download",
                Width = 120,
                Height = 40,
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                IsEnabled = !isDownloaded,
                Tag = model
            };

            downloadButton.Click += DownloadButton_Click;

            var openFolderButton = new Button
            {
                Width = 40,
                Height = 40,
                Background = Avalonia.Media.Brushes.Transparent,
                BorderThickness = new Avalonia.Thickness(0),
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                Content = new TextBlock
                {
                    Text = "📁",
                    FontSize = 20,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                },
                IsVisible = isDownloaded
            };

            openFolderButton.Click += (s, e) => OpenModelFolder();

            var progressBar = new ProgressBar
            {
                Height = 4,
                Margin = new Avalonia.Thickness(0, 10, 0, 0),
                IsVisible = false,
                Foreground = Avalonia.Media.Brushes.Green
            };

            var progressText = new TextBlock
            {
                Foreground = Avalonia.Media.Brushes.White,
                FontSize = 12,
                Margin = new Avalonia.Thickness(0, 5, 0, 0),
                IsVisible = false
            };

            var buttonsPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 10,
                Children = { downloadButton, openFolderButton }
            };

            var contentPanel = new StackPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = model.DisplayName,
                        FontSize = 16,
                        FontWeight = Avalonia.Media.FontWeight.Bold,
                        Foreground = Avalonia.Media.Brushes.White
                    },
                    new TextBlock
                    {
                        Text = model.FileName,
                        FontSize = 12,
                        Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(200, 200, 200)),
                        Margin = new Avalonia.Thickness(0, 5, 0, 0)
                    },
                    new TextBlock
                    {
                        Text = $"Size: {model.Size}",
                        FontSize = 12,
                        Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(150, 150, 150)),
                        Margin = new Avalonia.Thickness(0, 5, 0, 10)
                    },
                    buttonsPanel,
                    progressBar,
                    progressText
                }
            };

            _activeDownloads[model.FileName] = new DownloadProgress
            {
                ProgressBar = progressBar,
                ProgressText = progressText,
                DownloadButton = downloadButton,
                OpenFolderButton = openFolderButton
            };

            return new Border
            {
                Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(40, 40, 40)),
                BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(80, 80, 80)),
                BorderThickness = new Avalonia.Thickness(1),
                CornerRadius = new Avalonia.CornerRadius(8),
                Padding = new Avalonia.Thickness(20),
                Margin = new Avalonia.Thickness(0, 0, 0, 15),
                Child = contentPanel
            };
        }

        private async void DownloadButton_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not WhisperModel model)
                return;

            if (!_activeDownloads.TryGetValue(model.FileName, out var progress))
                return;

            button.IsEnabled = false;
            button.Content = "Downloading...";
            progress.ProgressBar.IsVisible = true;
            progress.ProgressText.IsVisible = true;

            try
            {
                await DownloadModel(model, progress);
                
                button.Content = "Downloaded";
                progress.OpenFolderButton.IsVisible = true;
                progress.ProgressBar.IsVisible = false;
                progress.ProgressText.IsVisible = false;
            }
            catch (Exception ex)
            {
                button.IsEnabled = true;
                button.Content = "Download";
                progress.ProgressBar.IsVisible = false;
                progress.ProgressText.IsVisible = false;
                progress.ProgressText.Text = $"Error: {ex.Message}";
                progress.ProgressText.IsVisible = true;
            }
        }

        private async Task DownloadModel(WhisperModel model, DownloadProgress progress)
        {
            string modelPath = Path.Combine(_modelsDirectory, model.FileName);
            
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
            if (Directory.Exists(_modelsDirectory))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _modelsDirectory,
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
        }
    }
}