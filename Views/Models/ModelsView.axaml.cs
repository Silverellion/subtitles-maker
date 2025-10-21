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
using System.Threading;

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

        // Unfocus the search box when clicking anywhere outside of it
        private void Root_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            var tb = this.FindControl<TextBox>("SearchBox");
            if (tb == null)
                return;

            var pos = e.GetPosition(tb);
            if (pos.X < 0 || pos.Y < 0 || pos.X > tb.Bounds.Width || pos.Y > tb.Bounds.Height)
            {
                var top = TopLevel.GetTopLevel(this);
                top?.FocusManager?.ClearFocus();
            }
        }

        private async void LoadModelsFromApi()
        {
            var grid = this.FindControl<Grid>("ModelsGrid");
            if (grid == null) return;

            grid.Children.Clear();
            grid.RowDefinitions.Clear();

            // Show loading indicator
            var loadingText = new TextBlock
            {
                Text = "Loading models from Hugging Face...",
                FontSize = 16,
                Foreground = Brushes.White,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Margin = new Avalonia.Thickness(0, 50, 0, 0)
            };
            Grid.SetColumnSpan(loadingText, 2);
            grid.Children.Add(loadingText);

            try
            {
                _availableModels = await FetchModelsFromHuggingFace();
                // Render all models initially
                RenderModels(_availableModels);
            }
            catch (Exception ex)
            {
                grid.Children.Clear();
                grid.RowDefinitions.Clear();

                var errorText = new TextBlock
                {
                    Text = $"Error loading models: {ex.Message}",
                    FontSize = 14,
                    Foreground = Brushes.Red,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    Margin = new Avalonia.Thickness(0, 50, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                };
                Grid.SetColumnSpan(errorText, 2);
                grid.Children.Add(errorText);
            }
        }

        private void RenderModels(IEnumerable<WhisperModel> models)
        {
            var grid = this.FindControl<Grid>("ModelsGrid");
            if (grid == null) return;

            grid.Children.Clear();
            grid.RowDefinitions.Clear();

            var list = models?.ToList() ?? new List<WhisperModel>();
            if (list.Count == 0)
            {
                var noModelsText = new TextBlock
                {
                    Text = "No models match your search.",
                    FontSize = 16,
                    Foreground = Brushes.White,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    Margin = new Avalonia.Thickness(0, 50, 0, 0)
                };
                Grid.SetColumnSpan(noModelsText, 2);
                grid.Children.Add(noModelsText);
                return;
            }

            int i = 0;
            foreach (var model in list.OrderBy(m => m.FileName))
            {
                var card = CreateModelCard(model);

                int row = i / 2;
                int col = i % 2;
                while (grid.RowDefinitions.Count <= row)
                    grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

                Grid.SetRow(card, row);
                Grid.SetColumn(card, col);

                card.Margin = new Avalonia.Thickness(col == 0 ? 0 : 10, 0, 0, 10);
                card.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;

                grid.Children.Add(card);
                i++;
            }
        }

        private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            var q = (sender as TextBox)?.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(q))
            {
                RenderModels(_availableModels);
                return;
            }

            var filtered = _availableModels.Where(m =>
                   (m.DisplayName?.IndexOf(q, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                || (m.FileName?.IndexOf(q, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                || (m.Size?.IndexOf(q, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0);

            RenderModels(filtered);
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
                Width = 45,
                Height = 45,
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
            downloadButton.Click += DownloadActionButton_Click;

            // Delete button (hidden until downloading or when downloaded)
            var deleteButton = new Button
            {
                Width = 45,
                Height = 45,
                Background = Brushes.Transparent,
                BorderThickness = new Avalonia.Thickness(0),
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                Content = new Image
                {
                    Source = new Bitmap(AssetLoader.Open(new Uri("avares://subtitles-maker/assets/icons/close-75.png"))),
                },
                IsVisible = isDownloaded
            };
            ToolTip.SetTip(deleteButton, "Delete model");
            deleteButton.Click += DeleteButton_Click;

            var openFolderButton = new Button
            {
                Width = 45,
                Height = 45,
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
                Width = 45,
                Height = 45,
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
                Children = { deleteButton, downloadButton, openFolderButton, openInNewButton }
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
                                FontWeight = FontWeight.Bold,
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
                DeleteButton = deleteButton,
                OpenFolderButton = openFolderButton,
                DownloadIcon = downloadIcon,
                FilePath = modelPath
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

        private async void DownloadActionButton_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not WhisperModel model)
                return;
            if (!_activeDownloads.TryGetValue(model.FileName, out var progress))
                return;
            if (progress.IsCompleted)
                return;

            if (progress.IsDownloading && !progress.IsPaused)
            {
                progress.IsPaused = true;
                progress.Cts?.Cancel();
                // Icon switches to play (resume)
                progress.DownloadIcon.Source = new Bitmap(AssetLoader.Open(new Uri("avares://subtitles-maker/assets/icons/play-75.png")));
                progress.DownloadIcon.Width = 40;
                progress.DownloadIcon.Height = 40;
                ToolTip.SetTip(progress.DownloadButton, $"Resume {model.DisplayName}");
                return;
            }

            // Resume if paused
            if (progress.IsPaused)
            {
                progress.Cts = new CancellationTokenSource();
                progress.IsPaused = false;
                progress.IsDownloading = true;
                progress.ProgressBar.IsVisible = true;
                progress.ProgressText.IsVisible = true;
                progress.DeleteButton.IsVisible = true;
                progress.DownloadIcon.Source = new Bitmap(AssetLoader.Open(new Uri("avares://subtitles-maker/assets/icons/pause-75.png")));
                ToolTip.SetTip(progress.DownloadButton, $"Pause {model.DisplayName}");

                try
                {
                    await DownloadModel(model, progress, resume: true, progress.Cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Pause requested; state already handled
                }
                catch (Exception ex)
                {
                    progress.IsDownloading = false;
                    progress.ProgressBar.IsVisible = false;
                    progress.ProgressText.Text = $"Error: {ex.Message}";
                    progress.ProgressText.Foreground = Brushes.Red;
                    progress.ProgressText.IsVisible = true;
                    await Task.Delay(5000);
                    progress.ProgressText.IsVisible = false;
                    // Reset action button to download icon on failure
                    progress.DownloadIcon.Source = new Bitmap(AssetLoader.Open(new Uri("avares://subtitles-maker/assets/icons/download-75.png")));
                    progress.DownloadIcon.Width = double.NaN;
                    progress.DownloadIcon.Height = double.NaN;
                    ToolTip.SetTip(progress.DownloadButton, $"Download {model.DisplayName}");
                    progress.DeleteButton.IsVisible = false;
                }
                return;
            }

            // Start fresh download
            progress.Cts = new CancellationTokenSource();
            progress.IsDownloading = true;
            progress.IsPaused = false;
            progress.ProgressBar.IsVisible = true;
            progress.ProgressText.IsVisible = true;
            progress.DeleteButton.IsVisible = true;
            progress.DownloadIcon.Source = new Bitmap(AssetLoader.Open(new Uri("avares://subtitles-maker/assets/icons/pause-75.png")));
            progress.DownloadIcon.Width = double.NaN;
            progress.DownloadIcon.Height = double.NaN;
            ToolTip.SetTip(progress.DownloadButton, $"Pause {model.DisplayName}");

            try
            {
                await DownloadModel(model, progress, resume: false, progress.Cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Pause requested; state already handled
            }
            catch (Exception ex)
            {
                progress.IsDownloading = false;
                progress.ProgressBar.IsVisible = false;
                progress.ProgressText.Text = $"Error: {ex.Message}";
                progress.ProgressText.Foreground = Brushes.Red;
                progress.ProgressText.IsVisible = true;
                await Task.Delay(5000);
                progress.ProgressText.IsVisible = false;
                // Reset action button to download icon on failure
                progress.DownloadIcon.Source = new Bitmap(AssetLoader.Open(new Uri("avares://subtitles-maker/assets/icons/download-75.png")));
                progress.DownloadIcon.Width = double.NaN;
                progress.DownloadIcon.Height = double.NaN;
                ToolTip.SetTip(progress.DownloadButton, $"Download {model.DisplayName}");
                progress.DeleteButton.IsVisible = false;
            }
        }

        private async Task DownloadModel(WhisperModel model, DownloadProgress progress, bool resume, CancellationToken token)
        {
            string modelPath = progress.FilePath;

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromHours(2);

            long existing = 0;
            if ((resume || File.Exists(modelPath)) && File.Exists(modelPath))
            {
                existing = new FileInfo(modelPath).Length;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, model.DownloadUrl);
            if (existing > 0)
            {
                request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(existing, null);
            }

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();

            var contentLength = response.Content.Headers.ContentLength ?? 0;
            var totalBytes = existing + contentLength;
            var buffer = new byte[8192];
            var totalRead = 0L;

            using var contentStream = await response.Content.ReadAsStreamAsync(token);
            using var fileStream = new FileStream(modelPath, existing > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            int bytesRead;
            while ((bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), token)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), token);
                totalRead += bytesRead;

                var downloaded = existing + totalRead;
                if (totalBytes > 0)
                {
                    var percentage = (double)downloaded / totalBytes * 100;
                    var downloadedMB = downloaded / 1024.0 / 1024.0;
                    var totalMB = totalBytes / 1024.0 / 1024.0;

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        progress.ProgressBar.Value = percentage;
                        progress.ProgressText.Text = $"{downloadedMB:F2} MB / {totalMB:F2} MB ({percentage:F1}%)";
                    });
                }
            }

            // Completed
            progress.IsDownloading = false;
            progress.IsCompleted = true;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                progress.ProgressBar.IsVisible = false;
                progress.ProgressText.IsVisible = false;
                progress.DownloadIcon.Source = new Bitmap(AssetLoader.Open(new Uri("avares://subtitles-maker/assets/icons/check-75.png")));
                progress.DownloadIcon.Width = double.NaN;
                progress.DownloadIcon.Height = double.NaN;
                ToolTip.SetTip(progress.DownloadButton, $"Model downloaded");
                progress.DeleteButton.IsVisible = true; // keep delete option after completion
            });
        }

        private async void DeleteButton_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            // Find the model from sibling download button tag or infer from mapping
            var progress = _activeDownloads.Values.FirstOrDefault(p => p.DeleteButton == btn);
            if (progress == null) return;

            // Cancel any ongoing download
            progress.IsPaused = false;
            progress.Cts?.Cancel();

            try
            {
                if (!string.IsNullOrWhiteSpace(progress.FilePath) && File.Exists(progress.FilePath))
                {
                    // Ensure stream is closed before deleting (download loop closes it on cancel/complete)
                    await Task.Delay(50);
                    File.Delete(progress.FilePath);
                }
            }
            catch { }

            // Reset UI to initial state (download available)
            progress.IsDownloading = false;
            progress.IsCompleted = false;
            progress.ProgressBar.IsVisible = false;
            progress.ProgressText.IsVisible = false;
            progress.DownloadIcon.Source = new Bitmap(AssetLoader.Open(new Uri("avares://subtitles-maker/assets/icons/download-75.png")));
            progress.DownloadIcon.Width = double.NaN;
            progress.DownloadIcon.Height = double.NaN;
            ToolTip.SetTip(progress.DownloadButton, "Download");
            progress.DeleteButton.IsVisible = false;
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
            public Button DeleteButton { get; set; } = null!;
            public CancellationTokenSource? Cts { get; set; }
            public bool IsDownloading { get; set; }
            public bool IsPaused { get; set; }
            public bool IsCompleted { get; set; }
            public string FilePath { get; set; } = string.Empty;
        }
    }
}