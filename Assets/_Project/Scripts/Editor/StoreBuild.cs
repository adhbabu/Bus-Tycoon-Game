using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BusTycoon.EditorTools
{
    /// <summary>
    /// Mağaza kimliği — İLK YÜKLEMEDEN ÖNCE gözden geçirin. Özellikle ApplicationId mağazaya bir kez
    /// yüklendikten sonra DEĞİŞTİRİLEMEZ (Google Play paket adı / Apple Bundle ID).
    /// </summary>
    public static class StoreSettings
    {
        public const string ProductName = "Otobüs İmparatoru";               // ana ekranda görünen ad
        public const string CompanyName = "adhbabu";                          // kayıt klasörü adında kullanılır
        public const string ApplicationId = "com.adhbabu.otobusimparatoru";   // ters alan adı biçimi, küçük harf
        public const string Version = "1.0.0";                                // kullanıcıya görünen sürüm
        public const int BuildNumber = 1;                                     // HER yüklemede 1 artırın

        public const string IconPath = "Assets/_Project/Art/AppIcon.png";
        public const string ScenePath = "Assets/_Project/Scenes/Main.unity";
        public const string AndroidOutput = "Builds/Android/OtobusImparatoru.aab";
        public const string IosOutput = "Builds/iOS";
    }

    /// <summary>
    /// "Bus Tycoon ▸ Mağaza" menüsü: proje ayarlarını mağaza kurallarına göre yapar ve derler.
    ///  1) Ayarları uygula  → kimlik, sürüm, yatay ekran, IL2CPP + ARM64, AAB, ikon, sahne
    ///  2) Android (.aab)    → Google Play Console'a yüklenecek dosya
    ///  3) iOS (Xcode)       → Mac'te Xcode ile açılıp App Store Connect'e gönderilecek proje
    /// Ayrıntılı yayın adımları: store/YAYIN-REHBERI.md
    /// </summary>
    public static class StoreBuild
    {
        [MenuItem("Bus Tycoon/Mağaza/1 - Mağaza ayarlarını uygula", priority = 100)]
        public static void ApplySettings()
        {
            PlayerSettings.companyName = StoreSettings.CompanyName;
            PlayerSettings.productName = StoreSettings.ProductName;
            PlayerSettings.bundleVersion = StoreSettings.Version;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, StoreSettings.ApplicationId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, StoreSettings.ApplicationId);

            // Oyun yalnızca yatay ekranda oynanır.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            // Android: Google Play 64-bit (ARM64) ve App Bundle (.aab) ister; hedef API en yeni kurulu sürüm.
            PlayerSettings.Android.bundleVersionCode = StoreSettings.BuildNumber;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            EditorUserBuildSettings.buildAppBundle = true;

            // iOS
            PlayerSettings.iOS.buildNumber = StoreSettings.BuildNumber.ToString();
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(StoreSettings.IconPath);
            if (icon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            else Debug.LogWarning($"[Mağaza] İkon bulunamadı: {StoreSettings.IconPath}");

            EnsureMainScene();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Mağaza] Ayarlar uygulandı: {StoreSettings.ProductName} {StoreSettings.Version} ({StoreSettings.BuildNumber}) · {StoreSettings.ApplicationId}");
        }

        [MenuItem("Bus Tycoon/Mağaza/2 - Android derle (Google Play .aab)", priority = 101)]
        public static void BuildAndroid()
        {
            ApplySettings();
            if (!PlayerSettings.Android.useCustomKeystore &&
                !EditorUtility.DisplayDialog("Yükleme anahtarı yok",
                    "Google Play'e yüklenecek dosya kendi anahtarınızla (keystore) imzalanmalıdır.\n\n" +
                    "Edit ▸ Project Settings ▸ Player ▸ Android ▸ Publishing Settings ▸ Keystore Manager ile bir anahtar " +
                    "oluşturun ve SAKLAYIN (kaybederseniz güncelleme yükleyemezsiniz).\n\n" +
                    "Anahtarsız derleme yalnızca kendi cihazınızda test içindir.", "Yine de derle (test)", "İptal"))
                return;

            Build(BuildTarget.Android, BuildTargetGroup.Android, StoreSettings.AndroidOutput);
        }

        [MenuItem("Bus Tycoon/Mağaza/3 - iOS Xcode projesi üret (App Store)", priority = 102)]
        public static void BuildIos()
        {
            ApplySettings();
            Build(BuildTarget.iOS, BuildTargetGroup.iOS, StoreSettings.IosOutput);
        }

        static void Build(BuildTarget target, BuildTargetGroup group, string output)
        {
            string directory = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { StoreSettings.ScenePath },
                locationPathName = output,
                target = target,
                targetGroup = group,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[Mağaza] Derleme hazır: {output} ({report.summary.totalSize / (1024 * 1024)} MB)");
                EditorUtility.RevealInFinder(output);
            }
            else
            {
                EditorUtility.DisplayDialog("Derleme başarısız",
                    $"{target} derlemesi tamamlanamadı ({report.summary.result}). Ayrıntılar Console penceresinde.\n\n" +
                    "Sık sebepler: ilgili platform modülü (Android/iOS Build Support) Unity Hub'dan kurulmamış, " +
                    "Android SDK/NDK eksik.", "Tamam");
            }
        }

        /// <summary>
        /// Oyun boş bir sahnede kendini kurar (GameBootstrap). Derleme için sahnede kamera ve ışık olan
        /// tek bir sahne yeterlidir; yoksa oluşturulup Build Settings'e eklenir.
        /// </summary>
        static void EnsureMainScene()
        {
            if (!File.Exists(StoreSettings.ScenePath))
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                Directory.CreateDirectory(Path.GetDirectoryName(StoreSettings.ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, StoreSettings.ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(StoreSettings.ScenePath, true) };
        }
    }
}
