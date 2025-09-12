using System;
using System.IO;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.MediaFoundation;

namespace subtitles_maker.services
{
    public class AudioConversionService
    {
        public event Action<string>? OnLogMessage;
        private static bool _mediaFoundationInitialized = false;

        static AudioConversionService()
        {
            InitializeMediaFoundation();
        }

        private static void InitializeMediaFoundation()
        {
            if (!_mediaFoundationInitialized)
            {
                try
                {
                    MediaFoundationApi.Startup();
                    _mediaFoundationInitialized = true;
                }
                catch (Exception) { }
            }
        }

        public async Task<string> ConvertToWavIfNeeded(string inputPath)
        {
            string extension = Path.GetExtension(inputPath).ToLowerInvariant();
            
            if (extension == ".wav")
            {
                OnLogMessage?.Invoke($"File is already WAV format: {Path.GetFileName(inputPath)}");
                return inputPath;
            }

            return await ConvertToWav(inputPath);
        }

        private async Task<string> ConvertToWav(string inputPath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    string fileName = Path.GetFileNameWithoutExtension(inputPath);
                    string tempDir = Path.GetTempPath();
                    string outputPath = Path.Combine(tempDir, "subtitles_maker_conversion", $"{fileName}_{Guid.NewGuid():N}.wav");
                    
                    string? outputDir = Path.GetDirectoryName(outputPath);
                    if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                        Directory.CreateDirectory(outputDir);

                    OnLogMessage?.Invoke($"Converting {Path.GetFileName(inputPath)} to WAV format...");

                    string extension = Path.GetExtension(inputPath).ToLowerInvariant();

                    // Try different conversion methods in order of preference
                    if (TryConvertWithMediaFoundation(inputPath, outputPath, extension))
                    {
                        OnLogMessage?.Invoke($"✓ Converted with MediaFoundation: {Path.GetFileName(outputPath)}");
                        return outputPath;
                    }
                    else if (TryConvertWithAudioFileReader(inputPath, outputPath))
                    {
                        OnLogMessage?.Invoke($"✓ Converted with NAudio: {Path.GetFileName(outputPath)}");
                        return outputPath;
                    }
                    else if (TryConvertWithMp3FileReader(inputPath, outputPath, extension))
                    {
                        OnLogMessage?.Invoke($"✓ Converted with Mp3FileReader: {Path.GetFileName(outputPath)}");
                        return outputPath;
                    }
                    else
                    {
                        throw new InvalidOperationException($"Unable to convert {extension} file with available methods");
                    }
                }
                catch (Exception ex)
                {
                    OnLogMessage?.Invoke($"✗ Conversion failed for {Path.GetFileName(inputPath)}: {ex.Message}");
                    throw new InvalidOperationException($"Failed to convert audio file: {ex.Message}", ex);
                }
            });
        }

        private bool TryConvertWithMediaFoundation(string inputPath, string outputPath, string extension)
        {
            if (!_mediaFoundationInitialized)
                return false;

            try
            {
                if (extension == ".mp4" || extension == ".m4a" || extension == ".aac" || extension == ".wma")
                {
                    using var reader = new MediaFoundationReader(inputPath);
                    ConvertToOptimalWav(reader, outputPath);
                    return true;
                }
            }
            catch (Exception) {}
            return false;
        }

        private bool TryConvertWithAudioFileReader(string inputPath, string outputPath)
        {
            try
            {
                using var reader = new AudioFileReader(inputPath);
                ConvertToOptimalWav(reader, outputPath);
                return true;
            }
            catch (Exception) { }
            return false;
        }

        private bool TryConvertWithMp3FileReader(string inputPath, string outputPath, string extension)
        {
            if (extension != ".mp3")
                return false;

            try
            {
                using var reader = new Mp3FileReader(inputPath);
                ConvertToOptimalWav(reader, outputPath);
                return true;
            }
            catch (Exception) { }
            return false;
        }

        private void ConvertToOptimalWav(WaveStream reader, string outputPath)
        {
            // 16kHz, 16-bit, mono is optimal for whisper
            var targetFormat = new WaveFormat(16000, 16, 1);
            
            using var resampler = new MediaFoundationResampler(reader, targetFormat)
            {
                ResamplerQuality = 60 
            };
            
            WaveFileWriter.CreateWaveFile(outputPath, resampler);
        }

        public void CleanupTempFiles()
        {
            try
            {
                string tempConversionDir = Path.Combine(Path.GetTempPath(), "subtitles_maker_conversion");
                if (Directory.Exists(tempConversionDir))
                {
                    Directory.Delete(tempConversionDir, true);
                    OnLogMessage?.Invoke("Cleaned up temporary conversion files");
                }
            }
            catch (Exception ex)
            {
                OnLogMessage?.Invoke($"Warning: Could not clean up temp files: {ex.Message}");
            }
        }
    }
}