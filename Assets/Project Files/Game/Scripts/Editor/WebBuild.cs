using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Watermelon.SquadShooter
{
    // Web build for testers (a Vercel link): WebGL with the ShooterWeb page template (portrait layout, loader,
    // capped pixel ratio), Gzip with decompression fallback so it works whatever headers the host sends.
    // Same performance setup as Squad Command: IL2CPP Master, 128 MB starting heap, uncapped rAF frame rate
    // (ScreenSettings) and the mobile WebPerformance profile.
    public static class WebBuild
    {
        public const string OUTPUT = "Builds/Web";

        [MenuItem("Tools/Web/Apply WebGL Settings", priority = 1)]
        public static void ApplySettings()
        {
            // the game code relies on these modules on every platform (input, TMP, ads, haptics)
            string defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone);
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.WebGL, defines);

            PlayerSettings.WebGL.template = "PROJECT:ShooterWeb";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            PlayerSettings.WebGL.showDiagnostics = false;
            PlayerSettings.WebGL.initialMemorySize = 128;
            PlayerSettings.runInBackground = false;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Low);
            PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.WebGL, Il2CppCompilerConfiguration.Master);
            AssetDatabase.SaveAssets();
            Debug.Log("[Web] WebGL settings applied");
        }

        [MenuItem("Tools/Web/Build Web Test", priority = 2)]
        public static bool Build()
        {
            ApplySettings();

            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (Directory.Exists(OUTPUT)) Directory.Delete(OUTPUT, true);

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions()
            {
                scenes = scenes,
                locationPathName = OUTPUT,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("[Web] Build failed: " + report.summary.result + " errors " + report.summary.totalErrors);
                return false;
            }

            File.WriteAllText(Path.Combine(OUTPUT, "vercel.json"),
                "{\n  \"headers\": [\n    { \"source\": \"/Build/(.*)\", \"headers\": [ { \"key\": \"Cache-Control\", \"value\": \"public, max-age=3600\" } ] }\n  ]\n}\n");

            long size = 0;
            foreach (FileInfo file in new DirectoryInfo(OUTPUT).GetFiles("*", SearchOption.AllDirectories)) size += file.Length;
            Debug.Log($"[Web] Build ok: {size / 1048576f:0.0} MB in {OUTPUT}");
            return true;
        }
    }
}
