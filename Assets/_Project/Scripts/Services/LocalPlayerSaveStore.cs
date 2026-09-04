using System;
using System.IO;
using UnityEngine;

namespace ARWalking.UI
{
    public enum SaveLoadStatus { Missing, Loaded, Corrupt }

    public sealed class SaveLoadResult
    {
        public SaveLoadStatus status;
        public PlayerSaveData save;
        public string backupPath;
        public string error;
    }

    public sealed class LocalPlayerSaveStore
    {
        public const string FileName = "player-save.json";
        public string SavePath { get; }

        public LocalPlayerSaveStore(string savePath = null)
        {
            SavePath = string.IsNullOrWhiteSpace(savePath)
                ? Path.Combine(Application.persistentDataPath, FileName)
                : savePath;
        }

        public SaveLoadResult Load()
        {
            if (!File.Exists(SavePath)) return new SaveLoadResult { status = SaveLoadStatus.Missing };
            try
            {
                var json = File.ReadAllText(SavePath);
                var save = JsonUtility.FromJson<PlayerSaveData>(json);
                if (save == null || save.schemaVersion <= 0 || save.schemaVersion > PlayerSaveData.CurrentSchemaVersion ||
                    !save.setupComplete || !PlayerSaveData.IsValidDisplayName(save.displayName))
                    throw new InvalidDataException("The local profile is incomplete or uses an unsupported schema.");
                save.displayName = PlayerSaveData.NormalizeDisplayName(save.displayName);
                save.RepairCollections();
                save.schemaVersion = PlayerSaveData.CurrentSchemaVersion;
                return new SaveLoadResult { status = SaveLoadStatus.Loaded, save = save };
            }
            catch (Exception exception)
            {
                var backup = PreserveCorruptFile();
                return new SaveLoadResult { status = SaveLoadStatus.Corrupt, backupPath = backup, error = exception.Message };
            }
        }

        public void Save(PlayerSaveData save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            save.schemaVersion = PlayerSaveData.CurrentSchemaVersion;
            save.RepairCollections();
            var directory = Path.GetDirectoryName(SavePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var temporaryPath = SavePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(save, true));
            if (File.Exists(SavePath)) File.Delete(SavePath);
            File.Move(temporaryPath, SavePath);
        }

        public void Reset()
        {
            if (File.Exists(SavePath)) File.Delete(SavePath);
            var temporaryPath = SavePath + ".tmp";
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }

        string PreserveCorruptFile()
        {
            try
            {
                var backup = SavePath + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff") + ".bak";
                File.Move(SavePath, backup);
                return backup;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not preserve corrupt local profile: " + exception.Message);
                return null;
            }
        }
    }
}
