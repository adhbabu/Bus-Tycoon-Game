using System;
using System.IO;
using UnityEngine;

namespace BusTycoon.Save
{
    /// <summary>
    /// <see cref="CompanyState"/>'i JSON olarak persistentDataPath altına yazar.
    /// Yazma atomiktir (önce .tmp, sonra yer değiştirme): oyun yazarken kapanırsa eski kayıt bozulmaz.
    /// </summary>
    public static class SaveSystem
    {
        public static string GetPath(string slot) =>
            Path.Combine(Application.persistentDataPath, $"bus_tycoon_{slot}.json");

        public static bool Exists(string slot) => File.Exists(GetPath(slot));

        public static void Save(CompanyState state, string slot)
        {
            state.saveVersion = CompanyState.CurrentSaveVersion;
            string path = GetPath(slot);
            string tempPath = path + ".tmp";

            File.WriteAllText(tempPath, JsonUtility.ToJson(state));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tempPath, path);
        }

        public static bool TryLoad(string slot, out CompanyState state)
        {
            state = null;
            string path = GetPath(slot);
            if (!File.Exists(path)) return false;

            try
            {
                state = JsonUtility.FromJson<CompanyState>(File.ReadAllText(path));
                if (state == null) return false;
                Migrate(state);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] '{path}' okunamadı: {e.Message}");
                return false;
            }
        }

        /// <summary>Eski sürüm kayıtları güncel şemaya taşır. Yeni alan eklendikçe buraya adım ekleyin.</summary>
        static void Migrate(CompanyState state)
        {
            if (state.saveVersion < 1)
            {
                // v0 → v1 örneği: yeni eklenen listeler null gelebilir.
                state.announcedModelIds ??= new System.Collections.Generic.List<string>();
            }
            state.saveVersion = CompanyState.CurrentSaveVersion;
        }
    }
}
