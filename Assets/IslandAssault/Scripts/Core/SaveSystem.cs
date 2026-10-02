using System;
using System.IO;
using UnityEngine;

namespace IslandAssault
{
    public static class SaveSystem
    {
        static string PathFile { get { return Path.Combine(Application.persistentDataPath, "islandassault_save.json"); } }

        public static SaveData Load()
        {
            try
            {
                if (!File.Exists(PathFile)) return null;
                string json = File.ReadAllText(PathFile);
                var data = JsonUtility.FromJson<SaveData>(json);
                if (data == null || data.buildings == null || data.buildings.Count == 0) return null;
                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[IslandAssault] Could not load save: " + e.Message);
                return null;
            }
        }

        public static void Save(SaveData data)
        {
            try
            {
                data.lastTime = GameData.Now();
                File.WriteAllText(PathFile, JsonUtility.ToJson(data, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[IslandAssault] Could not save: " + e.Message);
            }
        }

        public static void Delete()
        {
            try { if (File.Exists(PathFile)) File.Delete(PathFile); }
            catch (Exception e) { Debug.LogWarning("[IslandAssault] Could not delete save: " + e.Message); }
        }
    }
}
