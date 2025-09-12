using System;
using System.IO;

namespace subtitles_maker.utility
{
    public static class InitializeWhisper
    {
        private static readonly string ProjectRoot = GetProjectRoot();
        private static readonly string SourceWhisperPath = Path.Combine(ProjectRoot, "whisper");
        private static readonly string TargetWhisperPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "whisper");

        public static bool EnsureWhisperExists()
        {
            try
            {
                if (!Directory.Exists(SourceWhisperPath))
                    return false;

                string sourceExe = Path.Combine(SourceWhisperPath, "whisper-cli.exe");
                if (!File.Exists(sourceExe))
                    return false;

                if (!Directory.Exists(TargetWhisperPath))
                    Directory.CreateDirectory(TargetWhisperPath);

                string targetExe = Path.Combine(TargetWhisperPath, "whisper-cli.exe");

                if (!File.Exists(targetExe) || File.GetLastWriteTime(sourceExe) > File.GetLastWriteTime(targetExe))
                    CopyWhisperFiles();

                return File.Exists(targetExe);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void CopyWhisperFiles()
        {
            try
            {
                // Copy all files from source whisper directory to target
                foreach (string sourceFile in Directory.GetFiles(SourceWhisperPath, "*", SearchOption.AllDirectories))
                {
                    string relativePath = Path.GetRelativePath(SourceWhisperPath, sourceFile);
                    string targetFile = Path.Combine(TargetWhisperPath, relativePath);

                    string targetDir = Path.GetDirectoryName(targetFile);
                    if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                        Directory.CreateDirectory(targetDir);

                    File.Copy(sourceFile, targetFile, true);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to copy whisper files: {ex.Message}", ex);
            }
        }

        private static string GetProjectRoot()
        {
            string currentDir = AppDomain.CurrentDomain.BaseDirectory;
            
            DirectoryInfo dir = new DirectoryInfo(currentDir);
            
            while (dir != null && dir.Name != "subtitles-maker")
                dir = dir.Parent;

            if (dir == null)
            {
                // Fallback: look for .csproj file
                dir = new DirectoryInfo(currentDir);
                while (dir != null)
                {
                    if (Directory.GetFiles(dir.FullName, "*.csproj").Length > 0)
                        break;
                    dir = dir.Parent;
                }
            }

            return dir?.FullName ?? currentDir;
        }

        public static string GetWhisperExecutablePath()
        {
            return Path.Combine(TargetWhisperPath, "whisper-cli.exe");
        }

        public static bool IsWhisperAvailable()
        {
            string whisperExe = GetWhisperExecutablePath();
            return File.Exists(whisperExe);
        }
    }
}