using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace subtitles_maker.services
{
    public class FileProcessingService
    {
        private static readonly string[] SupportedAudioExtensions = 
        [
            ".mp3", ".mp4", ".wav", ".m4a", ".aac", ".flac", ".ogg", ".wma", ".avi", ".mkv", ".mov", ".webm"
        ];

        private static readonly string[] SupportedArchiveExtensions = 
        [
            ".zip", ".rar", ".7z"
        ];

        public event Action<string>? OnLogMessage;

        public bool IsValidFile(string fileName)
        {
            string extension = Path.GetExtension(fileName).ToLowerInvariant();
            return SupportedAudioExtensions.Contains(extension) || SupportedArchiveExtensions.Contains(extension);
        }

        public async Task<List<string>> ProcessDroppedFiles(IEnumerable<string> filePaths)
        {
            var audioFiles = new List<string>();

            foreach (var filePath in filePaths)
            {
                var fileName = Path.GetFileName(filePath);
                OnLogMessage?.Invoke($"Processing: {fileName}");

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
                    OnLogMessage?.Invoke($"Skipped '{fileName}' - Unsupported file type");
                }
            }

            if (audioFiles.Count == 0)
            {
                OnLogMessage?.Invoke("No supported audio files found");
            }

            return audioFiles;
        }

        private Task<List<string>> ProcessFolder(string folderPath)
        {
            var audioFiles = new List<string>();
            
            try
            {
                var files = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);
                
                foreach (var file in files)
                {
                    string extension = Path.GetExtension(file).ToLowerInvariant();
                    if (SupportedAudioExtensions.Contains(extension))
                        audioFiles.Add(file);
                }
                
                OnLogMessage?.Invoke($"Found {audioFiles.Count} audio files in folder");
            }
            catch (Exception ex)
            {
                OnLogMessage?.Invoke($"Error processing folder: {ex.Message}");
            }

            return Task.FromResult(audioFiles);
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
                    OnLogMessage?.Invoke($"Extracted ZIP archive to: {extractPath}");
                }
                else
                {
                    OnLogMessage?.Invoke($"Archive format {extension} requires additional extraction tools - skipping");
                    return audioFiles;
                }

                var extractedAudioFiles = await ProcessFolder(extractPath);
                audioFiles.AddRange(extractedAudioFiles);
            }
            catch (Exception ex)
            {
                OnLogMessage?.Invoke($"Error extracting archive: {ex.Message}");
            }

            return audioFiles;
        }
    }
}