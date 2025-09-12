using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace subtitles_maker.services
{
    public class FileProcessingService
    {
        private static readonly string[] SupportedAudioExtensions = 
        [
            ".mp3", ".mp4", ".wav",
            ".m4a", ".aac", ".flac",
            ".ogg", ".wma", ".avi",
            ".mkv", ".mov", ".webm"
        ];

        private static readonly string[] SupportedArchiveExtensions = 
        [
            ".zip", ".rar", ".7z"
        ];

        private readonly ArchiveExtractionService _extractionService;
        private readonly List<string> _extractedDirectories = new();

        public event Action<string>? OnLogMessage;

        public FileProcessingService()
        {
            _extractionService = new ArchiveExtractionService();
            _extractionService.OnLogMessage += message => OnLogMessage?.Invoke(message);
        }

        public bool IsValidFile(string fileName)
        {
            string extension = Path.GetExtension(fileName).ToLowerInvariant();
            return SupportedAudioExtensions.Contains(extension) || SupportedArchiveExtensions.Contains(extension);
        }

        public async Task<List<string>> ProcessDroppedFiles(IEnumerable<string> filePaths)
        {
            var audioFiles = new List<string>();

            try
            {
                foreach (var filePath in filePaths)
                {
                    try
                    {
                        var fileName = Path.GetFileName(filePath);
                        OnLogMessage?.Invoke($"Processing: {fileName}");

                        if (Directory.Exists(filePath))
                        {
                            var folderAudioFiles = ProcessFolder(filePath);
                            audioFiles.AddRange(folderAudioFiles);
                        }
                        else if (SupportedAudioExtensions.Contains(Path.GetExtension(fileName).ToLowerInvariant()))
                        {
                            audioFiles.Add(filePath);
                            OnLogMessage?.Invoke($"Added audio file: {fileName}");
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
                    catch (UnauthorizedAccessException ex)
                    {
                        OnLogMessage?.Invoke($"✗ Access denied processing '{Path.GetFileName(filePath)}': {ex.Message}");
                    }
                    catch (DirectoryNotFoundException ex)
                    {
                        OnLogMessage?.Invoke($"✗ Directory not found '{Path.GetFileName(filePath)}': {ex.Message}");
                    }
                    catch (FileNotFoundException ex)
                    {
                        OnLogMessage?.Invoke($"✗ File not found '{Path.GetFileName(filePath)}': {ex.Message}");
                    }
                    catch (IOException ex)
                    {
                        OnLogMessage?.Invoke($"✗ IO error processing '{Path.GetFileName(filePath)}': {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        OnLogMessage?.Invoke($"✗ Unexpected error processing '{Path.GetFileName(filePath)}': {ex.Message}");
                    }
                }

                if (audioFiles.Count == 0)
                    OnLogMessage?.Invoke("No supported audio files found");
                else
                    OnLogMessage?.Invoke($"Found {audioFiles.Count} audio files ready for transcription");

                return audioFiles;
            }
            catch (Exception ex)
            {
                OnLogMessage?.Invoke($"✗ Critical error in ProcessDroppedFiles: {ex.Message}");
                return new List<string>();
            }
        }

        private List<string> ProcessFolder(string folderPath)
        {
            var audioFiles = new List<string>();
            
            try
            {
                var files = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);
                
                foreach (var file in files)
                {
                    try
                    {
                        string extension = Path.GetExtension(file).ToLowerInvariant();
                        if (SupportedAudioExtensions.Contains(extension))
                            audioFiles.Add(file);
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        OnLogMessage?.Invoke($"✗ Access denied to file '{Path.GetFileName(file)}': {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        OnLogMessage?.Invoke($"✗ Error processing file '{Path.GetFileName(file)}': {ex.Message}");
                    }
                }
                
                OnLogMessage?.Invoke($"Found {audioFiles.Count} audio files in folder: {Path.GetFileName(folderPath)}");
            }
            catch (UnauthorizedAccessException ex)
            {
                OnLogMessage?.Invoke($"✗ Access denied to folder '{Path.GetFileName(folderPath)}': {ex.Message}");
            }
            catch (DirectoryNotFoundException ex)
            {
                OnLogMessage?.Invoke($"✗ Folder not found '{Path.GetFileName(folderPath)}': {ex.Message}");
            }
            catch (Exception ex)
            {
                OnLogMessage?.Invoke($"✗ Error processing folder '{Path.GetFileName(folderPath)}': {ex.Message}");
            }

            return audioFiles;
        }

        private async Task<List<string>> ProcessArchive(string archivePath)
        {
            var audioFiles = new List<string>();
            
            try
            {
                if (!_extractionService.CanExtract(archivePath))
                {
                    string extension = Path.GetExtension(archivePath).ToLowerInvariant();
                    OnLogMessage?.Invoke($"✗ Unsupported archive format: {extension}");
                    return audioFiles;
                }

                string? extractPath = await _extractionService.ExtractArchive(archivePath);
                
                if (!string.IsNullOrEmpty(extractPath))
                {
                    _extractedDirectories.Add(extractPath);
                    var extractedAudioFiles = ProcessFolder(extractPath);
                    audioFiles.AddRange(extractedAudioFiles);
                    
                    OnLogMessage?.Invoke($"Extracted {audioFiles.Count} audio files from archive: {Path.GetFileName(archivePath)}");
                }
                else
                {
                    OnLogMessage?.Invoke($"✗ Failed to extract archive: {Path.GetFileName(archivePath)}");
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                OnLogMessage?.Invoke($"✗ Access denied extracting archive '{Path.GetFileName(archivePath)}': {ex.Message}");
            }
            catch (FileNotFoundException ex)
            {
                OnLogMessage?.Invoke($"✗ Archive file not found '{Path.GetFileName(archivePath)}': {ex.Message}");
            }
            catch (InvalidDataException ex)
            {
                OnLogMessage?.Invoke($"✗ Corrupted or invalid archive '{Path.GetFileName(archivePath)}': {ex.Message}");
            }
            catch (Exception ex)
            {
                OnLogMessage?.Invoke($"✗ Error extracting archive '{Path.GetFileName(archivePath)}': {ex.Message}");
            }

            return audioFiles;
        }

        public void CleanupExtractedFiles()
        {
            try
            {
                foreach (string extractedDir in _extractedDirectories)
                    _extractionService.CleanupExtractedFiles(extractedDir);
                _extractedDirectories.Clear();
                
                _extractionService.CleanupAllTempExtractions();
            }
            catch (Exception ex)
            {
                OnLogMessage?.Invoke($"Warning: Error during cleanup: {ex.Message}");
            }
        }
    }
}