using FlyWireSwat.Sim;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FlyWireSwat.EditorTools
{
    public static class SceneSetup
    {
        [MenuItem("FlyWireSwat/Setup Scene")]
        public static void Setup()
        {
            var existing = Object.FindAnyObjectByType<SimulationDirector>();
            if (existing == null)
            {
                var go = new GameObject("SimulationDirector");
                go.AddComponent<SimulationDirector>();
                go.AddComponent<HudUI>();
                Undo.RegisterCreatedObjectUndo(go, "Create SimulationDirector");
            }
            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(0.32f, 0.36f, -0.5f);
                cam.transform.LookAt(new Vector3(0f, 0.12f, 0.1f));
                cam.nearClipPlane = 0.01f;
                cam.fieldOfView = 50f;
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[FlyWireSwat] Scene set up. Press Play.");
        }

        [MenuItem("FlyWireSwat/Create Weapon Assets")]
        public static void CreateWeaponAssets()
        {
            const string dir = "Assets/Data/Weapons";
            if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/Data", "Weapons");
            var list = FlyWireSwat.Weapons.WeaponLibrary.CreateDefaults();
            var director = Object.FindAnyObjectByType<SimulationDirector>();
            if (director != null) director.weapons.Clear();
            int i = 0;
            foreach (var w in list)
            {
                string path = $"{dir}/{i++:00}_{Sanitize(w.id)}.asset";
                AssetDatabase.CreateAsset(w, path);
                if (director != null) director.weapons.Add(w);
            }
            AssetDatabase.SaveAssets();
            if (director != null) EditorUtility.SetDirty(director);
            Debug.Log($"[FlyWireSwat] {list.Count} weapon assets written to {dir}");
        }

        [MenuItem("FlyWireSwat/Build Linux Player")]
        public static void BuildLinux()
        {
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/FlyWireSwat.unity" },
                locationPathName = EditorPrefs.GetString("FlyWireSwat.BuildDir", "Build/Linux") + "/FlyWireSwat.x86_64",
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.None,
            };
            PlayerSettings.productName = "FlyWireSwat";
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[FlyWireSwat] Build {report.summary.result}: {report.summary.totalSize / 1048576} MB, {report.summary.totalErrors} errors -> {opts.locationPathName}");
        }

        [MenuItem("FlyWireSwat/Game View 1920x1080")]
        public static void GameView1080()
        {
            try
            {
                var asm = typeof(EditorWindow).Assembly;
                var gvType = asm.GetType("UnityEditor.GameView");
                var gv = EditorWindow.GetWindow(gvType);
                var sizesType = asm.GetType("UnityEditor.GameViewSizes");
                var singleton = asm.GetType("UnityEditor.ScriptableSingleton`1").MakeGenericType(sizesType);
                var instance = singleton.GetProperty("instance").GetValue(null);
                var group = sizesType.GetMethod("GetGroup").Invoke(instance, new object[] { (int)sizesType.GetProperty("currentGroupType").GetValue(instance) });
                var groupType = group.GetType();
                int total = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
                int found = -1;
                for (int i = 0; i < total; i++)
                {
                    var size = groupType.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                    int w = (int)size.GetType().GetProperty("width").GetValue(size);
                    int h = (int)size.GetType().GetProperty("height").GetValue(size);
                    if (w == 1920 && h == 1080) { found = i; break; }
                }
                if (found < 0)
                {
                    var sizeType = asm.GetType("UnityEditor.GameViewSize");
                    var sizeTypeEnum = asm.GetType("UnityEditor.GameViewSizeType");
                    var newSize = System.Activator.CreateInstance(sizeType, System.Enum.Parse(sizeTypeEnum, "FixedResolution"), 1920, 1080, "FullHD 1080p");
                    groupType.GetMethod("AddCustomSize").Invoke(group, new[] { newSize });
                    found = total;
                }
                gvType.GetMethod("SizeSelectionCallback", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                      .Invoke(gv, new object[] { found, null });
                gv.Repaint();
                Debug.Log("[FlyWireSwat] Game view set to 1920x1080 (index " + found + ")");
            }
            catch (System.Exception e) { Debug.LogWarning("[FlyWireSwat] could not set game view size: " + e.Message); }
        }

        static string Sanitize(string s)
        {
            foreach (var c in System.IO.Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Replace(' ', '_').Replace('(', '_').Replace(')', '_');
        }
    }
}
