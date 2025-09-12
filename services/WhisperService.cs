using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using subtitles_maker.utility;

namespace subtitles_maker.services
{
    public class WhisperService
    {
        private static readonly Dictionary<string, string> LanguageCodes = new()
        {
            { "English", "en" },
            { "French", "fr" },
            { "German", "de" },
            { "Japanese", "ja" },
            { "Spanish", "es" }
        };

        public event Action<string>? OnLogMessage;

        public static bool IsWhisperAvailable()
        {
            return InitializeWhisper.IsWhisperAvailable();
        }

        public bool EnsureWhisperInitialized()
        {
            try
            {
                bool whisperReady = InitializeWhisper.EnsureWhisperExists();
                if (whisperReady)
                    OnLogMessage?.Invoke("✓ Whisper initialized successfully");
                else
                    OnLogMessage?.Invoke("✗ Failed to initialize Whisper - check if whisper-cli.exe exists in project/whisper folder");
                return whisperReady;
            }
            catch (Exception ex)
            {
                OnLogMessage?.Invoke($"Error initializing Whisper: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> TranscribeAudioFiles(List<string> audioFiles, string modelPath, string outputPath, string language = "English")
        {
            if (!ValidateInputs(modelPath, outputPath))
                return false;

            string whisperExe = InitializeWhisper.GetWhisperExecutablePath();
            
            if (!IsWhisperAvailable())
            {
                OnLogMessage?.Invoke($"Error: Whisper executable not found at: {whisperExe}");
                OnLogMessage?.Invoke("Make sure whisper-cli.exe exists in the project/whisper folder");
                return false;
            }

            OnLogMessage?.Invoke($"Starting transcription of {audioFiles.Count} files...");

            bool allSuccessful = true;
            foreach (string audioFile in audioFiles)
            {
                bool success = await TranscribeAudioFile(audioFile, whisperExe, modelPath, outputPath, language);
                if (!success) allSuccessful = false;
            }

            OnLogMessage?.Invoke("Transcription completed!");
            return allSuccessful;
        }

        private bool ValidateInputs(string modelPath, string outputPath)
        {
            if (string.IsNullOrEmpty(modelPath) || !File.Exists(modelPath))
            {
                OnLogMessage?.Invoke("Error: Invalid model path. Please select a valid model file.");
                return false;
            }

            if (string.IsNullOrEmpty(outputPath) || !Directory.Exists(outputPath))
            {
                OnLogMessage?.Invoke("Error: Invalid output path. Please select a valid output folder.");
                return false;
            }

            return true;
        }

        private async Task<bool> TranscribeAudioFile(string audioFilePath, string whisperExe, string modelPath, string outputPath, string language)
        {
            try
            {
                string fileName = Path.GetFileNameWithoutExtension(audioFilePath);
                string outputBase = Path.Combine(outputPath, fileName);

                OnLogMessage?.Invoke($"Transcribing: {Path.GetFileName(audioFilePath)}");

                string languageCode = LanguageCodes.ContainsKey(language) ? LanguageCodes[language] : "en";
                string arguments = $"-m \"{modelPath}\" -f \"{audioFilePath}\" -of \"{outputBase}\" --language {languageCode} --output-txt --output-srt";

                OnLogMessage?.Invoke($"Using language: {language} ({languageCode})");

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
                        OnLogMessage?.Invoke($"Whisper: {e.Data}");
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        OnLogMessage?.Invoke($"Whisper Error: {e.Data}");
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    OnLogMessage?.Invoke($"✓ Successfully transcribed: {Path.GetFileName(audioFilePath)}");
                    
                    string txtFile = outputBase + ".txt";
                    string srtFile = outputBase + ".srt";
                    
                    if (File.Exists(txtFile))
                        OnLogMessage?.Invoke($"  Created: {Path.GetFileName(txtFile)}");
                    if (File.Exists(srtFile))
                        OnLogMessage?.Invoke($"  Created: {Path.GetFileName(srtFile)}");
                    
                    return true;
                }
                else
                {
                    OnLogMessage?.Invoke($"✗ Failed to transcribe: {Path.GetFileName(audioFilePath)} (Exit code: {process.ExitCode})");
                    return false;
                }
            }
            catch (Exception ex)
            {
                OnLogMessage?.Invoke($"Error transcribing {Path.GetFileName(audioFilePath)}: {ex.Message}");
                return false;
            }
        }
    }
}