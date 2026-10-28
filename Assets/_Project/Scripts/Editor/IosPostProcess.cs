#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace BusTycoon.EditorTools
{
    /// <summary>
    /// iOS derlemesinden sonra Info.plist'e App Store'un sorduğu ihracat uyumu cevabını yazar:
    /// oyun ağ bağlantısı veya şifreleme kullanmaz → ITSAppUsesNonExemptEncryption = false.
    /// Böylece her TestFlight yüklemesinde "Export Compliance" sorusu çıkmaz.
    /// </summary>
    public static class IosPostProcess
    {
        [PostProcessBuild]
        public static void OnPostProcessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;

            string plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            plist.WriteToFile(plistPath);
        }
    }
}
#endif
