using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using BusTycoon.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace BusTycoon.EditorTools
{
    /// <summary>
    /// Mağaza kimliği — İLK YÜKLEMEDEN ÖNCE gözden geçirin. Özellikle ApplicationId mağazaya bir kez
    /// yüklendikten sonra DEĞİŞTİRİLEMEZ (Google Play paket adı / Apple Bundle ID).
    /// Bulut derlemeleri (.github/workflows) ApplicationId'yi bu dosyadan okur.
    /// </summary>
    public static class StoreSettings
    {
        public const string ProductName = "Otobüs İmparatoru";               // ana ekranda görünen ad
        public const string CompanyName = "adhbabu";                          // kayıt klasörü adında kullanılır
        public const string ApplicationId = "com.adhbabu.otobusimparatoru";   // ters alan adı biçimi, küçük harf
        public const string Version = "1.0.0";                                // kullanıcıya görünen sürüm
        public const int BuildNumber = 1;                                     // HER yüklemede 1 artırın (bulutta otomatik)

        public const string IconPath = "Assets/_Project/Art/AppIcon.png";
        public const string ScenePath = "Assets/_Project/Scenes/Main.unity";
        public const string AndroidOutput = "Builds/Android/OtobusImparatoru.aab";
        public const string IosOutput = "Builds/iOS";
        public const string WebGLOutput = "Builds/WebGL";
        public const string WebGLTemplate = "BusTycoon";                      // Assets/WebGLTemplates/BusTycoon
    }

    /// <summary>
    /// "Bus Tycoon ▸ Mağaza" menüsü: proje ayarlarını mağaza kurallarına göre yapar ve derler.
    ///  1) Ayarları uygula  → kimlik, sürüm, yatay ekran, IL2CPP + ARM64, AAB, ikon, sahne
    ///  2) Android (.aab)    → Google Play Console'a yüklenecek dosya
    ///  3) iOS (Xcode)       → Mac'te Xcode ile açılıp App Store Connect'e gönderilecek proje
    ///  4) Web (WebGL)       → tarayıcıda oynanan sürüm (GitHub Pages'te yayınlanır)
    /// Mac'siz iOS ve otomatik Android derlemesi için <see cref="BuildFromCommandLine"/> (GitHub Actions).
    /// Ayrıntılı yayın adımları: store/YAYIN-REHBERI.md ve store/BULUT-DERLEME.md
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
            EnsureRuntimeMaterials();
            EnsureMapTextureImport();
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

            BuildFromMenu(BuildTarget.Android, BuildTargetGroup.Android, StoreSettings.AndroidOutput);
        }

        [MenuItem("Bus Tycoon/Mağaza/3 - iOS Xcode projesi üret (App Store)", priority = 102)]
        public static void BuildIos()
        {
            ApplySettings();
            BuildFromMenu(BuildTarget.iOS, BuildTargetGroup.iOS, StoreSettings.IosOutput);
        }

        [MenuItem("Bus Tycoon/Mağaza/4 - Web sürümü derle (WebGL)", priority = 103)]
        public static void BuildWeb()
        {
            ApplySettings();
            ApplyWebGLSettings();
            BuildFromMenu(BuildTarget.WebGL, BuildTargetGroup.WebGL, StoreSettings.WebGLOutput);
        }

        /// <summary>
        /// Bulut derlemesinin (GitHub Actions + GameCI) giriş noktası; Unity'yi pencere açmadan çalıştırır:
        /// <code>
        /// Unity -batchmode -quit -projectPath . -executeMethod BusTycoon.EditorTools.StoreBuild.BuildFromCommandLine
        ///       -customBuildTarget Android -customBuildPath build/Android/Oyun.aab -buildNumber 12
        ///       -androidKeystoreName user.keystore -androidKeystorePass … -androidKeyaliasName … -androidKeyaliasPass …
        /// </code>
        /// Yol .apk ile bitiyorsa telefonda denemelik APK üretilir (anahtar isteğe bağlı); .aab için anahtar zorunludur,
        /// çünkü Google Play anahtarsız (debug) imzalı paketi kabul etmez. Şifreler yalnızca bu oturumda kullanılır,
        /// depoya yazılmaz. Sonuç çıkış koduyla bildirilir (0 = başarılı).
        /// </summary>
        public static void BuildFromCommandLine()
        {
            bool success = false;
            try
            {
                success = BuildFromArguments(ParseArguments(Environment.GetCommandLineArgs()));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            EditorApplication.Exit(success ? 0 : 1);
        }

        static bool BuildFromArguments(Dictionary<string, string> args)
        {
            ApplySettings();

            if (args.TryGetValue("buildNumber", out string number) && int.TryParse(number, out int buildNumber) && buildNumber > 0)
            {
                PlayerSettings.Android.bundleVersionCode = buildNumber;
                PlayerSettings.iOS.buildNumber = buildNumber.ToString();
            }

            string targetName = args.TryGetValue("customBuildTarget", out string t) ? t : "";
            args.TryGetValue("customBuildPath", out string output);

            if (targetName.Equals("iOS", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(output)) output = StoreSettings.IosOutput;
                Debug.Log($"[Mağaza] iOS Xcode projesi derleniyor (build {PlayerSettings.iOS.buildNumber}) → {output}");
                return Build(BuildTarget.iOS, BuildTargetGroup.iOS, output);
            }

            if (targetName.Equals("WebGL", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(output)) output = StoreSettings.WebGLOutput;
                ApplyWebGLSettings();
                Debug.Log($"[Mağaza] Web sürümü (WebGL) derleniyor → {output}");
                return Build(BuildTarget.WebGL, BuildTargetGroup.WebGL, output);
            }

            if (!targetName.Equals("Android", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError($"[Mağaza] Bilinmeyen hedef: '{targetName}'. -customBuildTarget Android, iOS veya WebGL olmalı.");
                return false;
            }

            if (string.IsNullOrEmpty(output)) output = StoreSettings.AndroidOutput;
            bool appBundle = !output.EndsWith(".apk", StringComparison.OrdinalIgnoreCase);
            EditorUserBuildSettings.buildAppBundle = appBundle;

            if (args.TryGetValue("androidTargetSdkVersion", out string sdk) && TryParseSdkVersion(sdk, out var sdkVersion))
                PlayerSettings.Android.targetSdkVersion = sdkVersion;

            bool signed = ApplyKeystore(args);
            if (!signed && appBundle)
            {
                Debug.LogError("[Mağaza] Google Play paketi (.aab) imza anahtarı olmadan derlenemez. " +
                               "ANDROID_KEYSTORE_* sırlarını ekleyin (store/BULUT-DERLEME.md) ya da denemelik APK seçin.");
                return false;
            }
            if (!signed) Debug.LogWarning("[Mağaza] İmza anahtarı yok: APK yalnızca test için (debug) imzalanacak.");

            Debug.Log($"[Mağaza] Android {(appBundle ? "App Bundle" : "APK")} derleniyor " +
                      $"(versionCode {PlayerSettings.Android.bundleVersionCode}, hedef API {PlayerSettings.Android.targetSdkVersion}) → {output}");
            return Build(BuildTarget.Android, BuildTargetGroup.Android, output);
        }

        /// <summary>
        /// Tam pencere şablonu (yükleme çubuğu, telefonu yan çevir uyarısı, tarayıcıda kalıcı kayıt) ve GitHub Pages
        /// uyumlu sıkıştırma: Pages, sıkıştırılmış dosyalara Content-Encoding başlığı eklemez; açma işini yükleyici yapar.
        /// </summary>
        static void ApplyWebGLSettings()
        {
            PlayerSettings.WebGL.template = "PROJECT:" + StoreSettings.WebGLTemplate;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
        }

        /// <summary>Komut satırından gelen anahtarı oturum için ayarlar; anahtar dosyası yoksa false.</summary>
        static bool ApplyKeystore(Dictionary<string, string> args)
        {
            PlayerSettings.Android.useCustomKeystore = false;
            if (!args.TryGetValue("androidKeystoreName", out string keystore) || string.IsNullOrEmpty(keystore)) return false;
            if (!Path.IsPathRooted(keystore)) keystore = Path.Combine(Directory.GetParent(Application.dataPath).FullName, keystore);
            if (!File.Exists(keystore))
            {
                Debug.LogWarning($"[Mağaza] İmza anahtarı dosyası bulunamadı: {keystore}");
                return false;
            }

            args.TryGetValue("androidKeystorePass", out string keystorePass);
            args.TryGetValue("androidKeyaliasName", out string aliasName);
            args.TryGetValue("androidKeyaliasPass", out string aliasPass);
            if (string.IsNullOrEmpty(keystorePass) || string.IsNullOrEmpty(aliasName))
            {
                Debug.LogWarning("[Mağaza] İmza anahtarının şifresi veya takma adı (alias) eksik.");
                return false;
            }

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystore;
            PlayerSettings.Android.keystorePass = keystorePass;
            PlayerSettings.Android.keyaliasName = aliasName;
            // Unity'nin Keystore Manager'ı varsayılan olarak alias şifresini anahtar şifresiyle aynı yapar.
            PlayerSettings.Android.keyaliasPass = string.IsNullOrEmpty(aliasPass) ? keystorePass : aliasPass;
            return true;
        }

        /// <summary>"AndroidApiLevel36" ya da "36" biçimini kabul eder.</summary>
        static bool TryParseSdkVersion(string value, out AndroidSdkVersions version)
        {
            version = AndroidSdkVersions.AndroidApiLevelAuto;
            if (string.IsNullOrEmpty(value)) return false;
            if (Enum.TryParse(value, out version)) return true;
            const string prefix = "AndroidApiLevel";
            string digits = value.StartsWith(prefix, StringComparison.Ordinal) ? value.Substring(prefix.Length) : value;
            if (!int.TryParse(digits, out int level) || level <= 0) return false;
            version = (AndroidSdkVersions)level;
            return true;
        }

        /// <summary>"-anahtar değer" çiftlerini okur; değeri olmayan bayraklar boş değer alır.</summary>
        static Dictionary<string, string> ParseArguments(string[] commandLine)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < commandLine.Length; i++)
            {
                string arg = commandLine[i];
                if (arg.Length < 2 || arg[0] != '-') continue;
                bool hasValue = i + 1 < commandLine.Length && !commandLine[i + 1].StartsWith("-", StringComparison.Ordinal);
                result[arg.Substring(1)] = hasValue ? commandLine[++i] : "";
            }
            return result;
        }

        static void BuildFromMenu(BuildTarget target, BuildTargetGroup group, string output)
        {
            if (Build(target, group, output))
            {
                EditorUtility.RevealInFinder(output);
                return;
            }
            EditorUtility.DisplayDialog("Derleme başarısız",
                $"{target} derlemesi tamamlanamadı. Ayrıntılar Console penceresinde.\n\n" +
                "Sık sebepler: ilgili platform modülü (Android/iOS Build Support) Unity Hub'dan kurulmamış, " +
                "Android SDK/NDK eksik.", "Tamam");
        }

        static bool Build(BuildTarget target, BuildTargetGroup group, string output)
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
                return true;
            }
            Debug.LogError($"[Mağaza] {target} derlemesi tamamlanamadı: {report.summary.result} ({report.summary.totalErrors} hata)");
            return false;
        }

        /// <summary>
        /// Oyun malzemelerini kodla oluşturur (MaterialFactory). Unity hiçbir asset'in kullanmadığı shader'ları
        /// derlemeye koymaz; Resources'taki bu şablonlar Standard ve Unlit/Texture'ı derlemeye dahil eder.
        /// Olmazsa harita ve şehirler build'de pembe görünür. (URP'de pipeline'ın varsayılan malzemesi kullanılır.)
        /// </summary>
        static void EnsureRuntimeMaterials()
        {
            if (GraphicsSettings.currentRenderPipeline != null) return;
            EnsureMaterial(DefaultContent.LitMaterialResourcePath, "Standard");
            EnsureMaterial(DefaultContent.MapMaterialResourcePath, "Unlit/Texture");
        }

        /// <summary>
        /// Harita dokusu 4096 px'tir (Tools/build_map.py); Unity varsayılanı 2048'e küçültür. Tam boyut, mipmap,
        /// yüksek kaliteli sıkıştırma ve keskin eğik bakış (anizotropik) ayarlanır.
        /// </summary>
        static void EnsureMapTextureImport()
        {
            string path = $"Assets/_Project/Resources/{DefaultContent.MapTextureResourcePath}.jpg";
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
            {
                Debug.LogWarning($"[Mağaza] Harita dokusu bulunamadı: {path}");
                return;
            }

            bool upToDate = importer.maxTextureSize == 4096 && importer.npotScale == TextureImporterNPOTScale.None &&
                            importer.mipmapEnabled && importer.anisoLevel >= 4 && importer.wrapMode == TextureWrapMode.Clamp &&
                            importer.textureCompression == TextureImporterCompression.CompressedHQ;
            if (upToDate) return;

            importer.textureType = TextureImporterType.Default;
            importer.maxTextureSize = 4096;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 4;
            importer.filterMode = FilterMode.Trilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            Debug.Log($"[Mağaza] Harita dokusu içe aktarma ayarları güncellendi: {path}");
        }

        static void EnsureMaterial(string resourcePath, string shaderName)
        {
            string assetPath = $"Assets/_Project/Resources/{resourcePath}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(assetPath) != null) return;

            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning($"[Mağaza] Shader bulunamadı: {shaderName}");
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
            AssetDatabase.CreateAsset(new Material(shader), assetPath);
            Debug.Log($"[Mağaza] Şablon malzeme oluşturuldu: {assetPath} ({shaderName})");
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
