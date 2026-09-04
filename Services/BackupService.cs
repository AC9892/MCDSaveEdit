using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MCDSaveEdit.Services
{
    public static class BackupService
    {
        public const int DefaultRetentionCount = 20;
        public const string BackupDirectoryName = "Backups";

        public static string CreateBackup(string sourcePath, int retentionCount = DefaultRetentionCount)
        {
            return CreateBackup(sourcePath, DateTime.Now, retentionCount);
        }

        public static async Task<string?> RestoreBackupAsync(string backupPath, string targetPath,
            int retentionCount = DefaultRetentionCount)
        {
            if (string.IsNullOrWhiteSpace(backupPath)) throw new ArgumentException("A backup path is required.", nameof(backupPath));
            if (!File.Exists(backupPath)) throw new FileNotFoundException("The selected backup does not exist.", backupPath);
            if (string.Equals(Path.GetFullPath(backupPath), Path.GetFullPath(targetPath), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("A backup cannot be restored over itself.");

            using var backupStream = new FileStream(backupPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return await SafeFileWriter.WriteAsync(backupStream, targetPath, retentionCount);
        }

        public static IReadOnlyList<BackupEntry> GetBackups(string targetPath)
        {
            if (string.IsNullOrWhiteSpace(targetPath)) return new BackupEntry[0];
            var fullTargetPath = Path.GetFullPath(targetPath);
            var directory = Path.GetDirectoryName(fullTargetPath);
            if (string.IsNullOrWhiteSpace(directory)) return new BackupEntry[0];
            var backupDirectory = Path.Combine(directory, BackupDirectoryName);
            if (!Directory.Exists(backupDirectory)) return new BackupEntry[0];

            var targetName = Path.GetFileName(fullTargetPath);
            var baseName = Path.GetFileNameWithoutExtension(targetName);
            var extension = Path.GetExtension(targetName);
            var prefix = baseName + "_";
            return new DirectoryInfo(backupDirectory)
                .EnumerateFiles("*" + extension, SearchOption.TopDirectoryOnly)
                .Where(file => file.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(file => file.CreationTimeUtc)
                .ThenByDescending(file => file.Name)
                .Select(file => new BackupEntry(targetName, file))
                .ToArray();
        }

        internal static string CreateBackup(string sourcePath, DateTime timestamp, int retentionCount)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
                throw new ArgumentException("A source path is required.", nameof(sourcePath));
            if (retentionCount < 1)
                throw new ArgumentOutOfRangeException(nameof(retentionCount), "At least one backup must be retained.");
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("The save file to back up does not exist.", sourcePath);

            var source = new FileInfo(sourcePath);
            var backupDirectory = Path.Combine(source.DirectoryName!, BackupDirectoryName);
            Directory.CreateDirectory(backupDirectory);

            var baseName = Path.GetFileNameWithoutExtension(source.Name);
            var timestampPart = timestamp.ToString("yyyy-MM-dd_HHmmss");
            var backupPath = Path.Combine(backupDirectory, $"{baseName}_{timestampPart}{source.Extension}");
            var suffix = 1;
            while (File.Exists(backupPath))
            {
                backupPath = Path.Combine(backupDirectory, $"{baseName}_{timestampPart}_{suffix}{source.Extension}");
                suffix++;
            }

            File.Copy(source.FullName, backupPath, false);
            PruneBackups(backupDirectory, baseName, source.Extension, retentionCount);
            return backupPath;
        }

        private static void PruneBackups(string backupDirectory, string baseName, string extension, int retentionCount)
        {
            var prefix = baseName + "_";
            IEnumerable<FileInfo> backups = new DirectoryInfo(backupDirectory)
                .EnumerateFiles("*" + extension, SearchOption.TopDirectoryOnly)
                .Where(file => file.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(file => file.CreationTimeUtc)
                .ThenByDescending(file => file.Name);

            foreach (var expired in backups.Skip(retentionCount))
                expired.Delete();
        }
    }

    public sealed class BackupEntry
    {
        internal BackupEntry(string saveFileName, FileInfo file)
        {
            SaveFileName = saveFileName;
            BackupFileName = file.Name;
            Date = file.CreationTime.ToShortDateString();
            Time = file.CreationTime.ToLongTimeString();
            FileSize = file.Length;
            Path = file.FullName;
        }

        public string SaveFileName { get; }
        public string BackupFileName { get; }
        public string Date { get; }
        public string Time { get; }
        public long FileSize { get; }
        public string Path { get; }
    }
}
