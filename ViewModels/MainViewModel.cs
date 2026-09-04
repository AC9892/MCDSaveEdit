using DungeonTools.Save.File;
using MCDSaveEdit.Data;
using MCDSaveEdit.Logic;
using MCDSaveEdit.Save.Models.Profiles;
using MCDSaveEdit.Services;
using MCDSaveEdit.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
#nullable enable

namespace MCDSaveEdit.ViewModels
{
    public class MainViewModel
    {
        public ProfileViewModel profileModel = new ProfileViewModel();

        public bool isDirty { get; private set; }
        public Action<bool>? dirtyStateChanged;

        public Action<string>? showError;

        public string? detectedGameVersion;
        public string? detectedGameInstallationType;
        public string? selectedPakDirectory;

        private List<FileInfo> _recentFilesInfos = new List<FileInfo>();
        public IReadOnlyCollection<FileInfo> recentFilesInfos { get { return _recentFilesInfos; } }

        public MainViewModel()
        {
            profileModel.onChanged = () => setDirty(true);
            loadRecentFilesList();
        }

        private void setDirty(bool value)
        {
            if (isDirty == value) return;
            isDirty = value;
            dirtyStateChanged?.Invoke(value);
        }

        #region Recent Files List

        private void loadRecentFilesList()
        {
            // Reload items from the registry.
            for (int i = 0; i < Constants.MAX_RECENT_FILES; i++)
            {
                var file_name = RegistryTools.GetSetting(Constants.APPLICATION_NAME, "FilePath" + i.ToString(), string.Empty);
                if (!string.IsNullOrWhiteSpace(file_name))
                {
                    _recentFilesInfos.Add(new FileInfo(file_name));
                }
            }
        }

        // Save the current items in the Registry.
        private void saveRecentFilesList()
        {
            // Delete the saved entries.
            for (int i = 0; i < Constants.MAX_RECENT_FILES; i++)
            {
                RegistryTools.DeleteSetting(Constants.APPLICATION_NAME, "FilePath" + i.ToString());
            }

            // Save the current entries.
            int index = 0;
            foreach (FileInfo file_info in _recentFilesInfos)
            {
                RegistryTools.SaveSetting(Constants.APPLICATION_NAME,
                    "FilePath" + index.ToString(), file_info.FullName);
                index++;
            }
        }

        // Remove a file's info from the list.
        private void removeFileInfo(string file_name)
        {
            // Remove occurrences of the file's information from the list.
            for (int i = _recentFilesInfos.Count - 1; i >= 0; i--)
            {
                if (_recentFilesInfos[i].FullName == file_name) _recentFilesInfos.RemoveAt(i);
            }
        }

        // Add a file to the list, rearranging if necessary.
        private void addRecentFile(string file_name)
        {
            // Remove the file from the list.
            removeFileInfo(file_name);

            // Add the file to the beginning of the list.
            _recentFilesInfos.Insert(0, new FileInfo(file_name));

            // If we have too many items, remove the last one.
            if (_recentFilesInfos.Count > Constants.MAX_RECENT_FILES) _recentFilesInfos.RemoveAt(Constants.MAX_RECENT_FILES);

            // Update the Registry.
            saveRecentFilesList();
        }

        // Remove a file from the list, rearranging if necessary.
        private void removeRecentFile(string file_name)
        {
            // Remove the file from the list.
            removeFileInfo(file_name);

            // Update the Registry.
            saveRecentFilesList();
        }

        #endregion

        #region Open File

        public async Task handleFileOpenAsync(string filePath)
        {
            try
            {
                var profile = await handleGenericFileOpenAsync(filePath);
                if(profile != null)
                {
                    addRecentFile(filePath);
                    setProfile(filePath, profile!);
                }
            }
            catch (UnauthorizedAccessException exception)
            {
                showOpenError("The file could not be read because access was denied.", filePath, exception);
            }
            catch (IOException exception)
            {
                showOpenError("The file could not be read. It may be locked, unavailable, or stored on a disconnected device.", filePath, exception);
            }
            catch (Exception exception)
            {
                showOpenError("An unexpected error occurred while opening the file.", filePath, exception);
            }
        }

        public void setProfile(string filePath, ProfileSaveFile profile)
        {
            profileModel.filePath = filePath;
            profileModel.profile.setValue = profile;
            setDirty(false);
        }

        private Task<ProfileSaveFile?> handleGenericFileOpenAsync(string filePath)
        {
            Console.WriteLine("Reading file: {0}", filePath);
            if (string.Equals(Path.GetExtension(filePath), Constants.DECRYPTED_FILE_EXTENSION, StringComparison.OrdinalIgnoreCase))
            {
                return handleJsonFileOpen(filePath);
            }
            else
            {
                return handleDatFileOpen(filePath);
            }
        }

        private async Task<ProfileSaveFile?> handleJsonFileOpen(string filePath)
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            var detected = FileFormatDetector.Detect(stream);
            if (detected == DetectedFileFormat.Empty)
            {
                showError?.Invoke($"{Path.GetFileName(filePath)} is empty. No data was read and the original file was not modified.");
                return null;
            }
            if (detected != DetectedFileFormat.JsonText)
            {
                showError?.Invoke($"{Path.GetFileName(filePath)} has a JSON extension but contains {describeFormat(detected)}. Select a decrypted JSON character save. The original file was not modified.");
                return null;
            }
            return await tryParseFileStreamAsync(stream, "decrypted JSON");
        }

        private async Task<ProfileSaveFile?> handleDatFileOpen(string filePath)
        {
            var file = new FileInfo(filePath);
            using FileStream inputStream = file.OpenRead();
            var detected = FileFormatDetector.Detect(inputStream);
            if (detected == DetectedFileFormat.Empty)
            {
                showError?.Invoke($"{file.Name} is empty. It is not a usable Minecraft Dungeons save, and it was not modified.");
                return null;
            }
            bool encrypted = SaveFileHandler.IsFileEncrypted(inputStream);
            if (!encrypted)
            {
                if (detected == DetectedFileFormat.JsonText)
                    return await tryParseFileStreamAsync(inputStream, "decrypted JSON stored with a .dat extension");

                var guidance = detected == DetectedFileFormat.Binary
                    ? "It may be an unsupported console/container save or corrupted binary data. Extract the raw character save before opening it."
                    : "It appears to be unrelated text rather than a character save.";
                EventLogger.logError($"The file '{file.Name}' is not an encrypted character save ({detected}).");
                showError?.Invoke($"{file.Name} was detected as {describeFormat(detected)}, not an encrypted Minecraft Dungeons character save. {guidance}\n\nThe original file was not modified.");
                return null;
            }
            using Stream? processed = await FileProcessHelper.Decrypt(inputStream);
            if (processed == null)
            {
                EventLogger.logError($"Content of file \"{file.Name}\" could not be converted to a supported format.");
                showError?.Invoke(R.formatFILE_DECRYPT_ERROR_MESSAGE(file.Name));
                return null;
            }
            return await tryParseFileStreamAsync(processed!, "decrypted Minecraft Dungeons character data");
        }

        private async Task<ProfileSaveFile?> tryParseFileStreamAsync(Stream stream, string detectedFormat)
        {
            try
            {
                stream.Seek(0, SeekOrigin.Begin);
                var profile = await ProfileParser.Read(stream);
                if(profile == null || !profile.isValid())
                {
                    var version = profile?.Version > 0 ? $" Save version reported: {profile.Version}." : string.Empty;
                    showError?.Invoke($"The file was read as {detectedFormat}, but required character fields were missing.{version} It may use a newer unsupported structure or may not be a character save.\n\nThe original file was not modified.");
                    return null;
                }
                return profile;
            }
            catch (JsonException exception)
            {
                EventLogger.logError($"Malformed {detectedFormat}: {exception.Message}");
                showError?.Invoke($"The file was detected as {detectedFormat}, but its JSON structure is malformed near byte {exception.BytePositionInLine}. The file may be corrupted, incompletely decrypted, or from an unsupported container.\n\nTechnical details: {exception.Message}\n\nThe original file was not modified.");
            }
            catch (Exception e)
            {
                EventLogger.logError(e.ToString());
                showError?.Invoke($"The file was detected as {detectedFormat}, but it could not be converted into a supported character profile.\n\nTechnical details: {e.GetType().Name}: {e.Message}\n\nThe original file was not modified.");
            }
            return null;
        }

        private void showOpenError(string message, string filePath, Exception exception)
        {
            EventLogger.logError($"Open failed for {Path.GetExtension(filePath)}: {exception.GetType().Name}: {exception.Message}");
            showError?.Invoke($"{message}\n\nTechnical details: {exception.GetType().Name}: {exception.Message}\n\nThe original file was not modified.");
        }

        private static string describeFormat(DetectedFileFormat format)
        {
            return format switch
            {
                DetectedFileFormat.Empty => "an empty file",
                DetectedFileFormat.JsonText => "JSON text",
                DetectedFileFormat.PlainText => "plain text",
                _ => "unrecognized binary data"
            };
        }

        #endregion

        #region Save File

        public async Task handleFileSaveAsync(string? filePath, ProfileSaveFile profile)
        {
            if (filePath == null) { return; }
            profile.TotalGearPower = profile.computeCharacterPower();
            Console.WriteLine("Writing file: {0}", filePath!);
            if (string.Equals(Path.GetExtension(filePath!), Constants.DECRYPTED_FILE_EXTENSION, StringComparison.OrdinalIgnoreCase))
            {
                await handleJsonFileSave(filePath!, profile);
            }
            else
            {
                await handleDatFileSave(filePath!, profile);
            }
            addRecentFile(filePath);
            profileModel.filePath = filePath;
            setDirty(false);
        }

        private async Task handleJsonFileSave(string filePath, ProfileSaveFile profile)
        {
            using var stream = await ProfileParser.Write(profile);
            await SafeFileWriter.WriteAsync(stream, filePath,
                Math.Max(1, Settings.Default.BackupRetentionCount), Settings.Default.AutomaticBackupsEnabled);
        }

        private async Task handleDatFileSave(string filePath, ProfileSaveFile profile)
        {
            using var inputStream = await ProfileParser.Write(profile);
            inputStream.Seek(0, SeekOrigin.Begin);
            using Stream? processed = await FileProcessHelper.Encrypt(inputStream);
            if (processed == null)
            {
                EventLogger.logError($"Failed to encrypt the json data.");
                showError?.Invoke(R.FAILED_TO_ENCRYPT_ERROR_MESSAGE);
                return;
            }
            await SafeFileWriter.WriteAsync(processed, filePath,
                Math.Max(1, Settings.Default.BackupRetentionCount), Settings.Default.AutomaticBackupsEnabled);
        }

        #endregion
    }
}
