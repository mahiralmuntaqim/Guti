using System.IO;
using FishNet.Component.Transforming;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Object;
using FishNet.Transporting.Tugboat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BrineTime.EditorTools
{
    /// <summary>
    /// Generates the few assets Brine Time needs (everything else is built in code at runtime):
    /// the NetPlayer prefab, the FishNet spawnable-prefab list, the sky material, and Main.unity
    /// with a NetworkManager + Bootstrap. Runs automatically when Main.unity is missing; also on the
    /// "Brine Time" menu and from the command line via -executeMethod.
    /// </summary>
    [InitializeOnLoad]
    public static class BrineSetup
    {
        const string Root = "Assets/BrineTime/Generated";
        const string ScenePath = "Assets/BrineTime/Scenes/Main.unity";
        const string PrefabPath = Root + "/NetPlayer.prefab";
        const string PrefabListPath = Root + "/BrinePrefabs.asset";
        const string SkyPath = "Assets/BrineTime/Resources/SkyMaterial.mat";

        static BrineSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(ScenePath) && !Application.isBatchMode) Rebuild();
            };
        }

        [MenuItem("Brine Time/Rebuild Generated Assets + Scene")]
        public static void Rebuild()
        {
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            Directory.CreateDirectory(Path.GetDirectoryName(SkyPath));

            // 1. networked presence prefab
            var temp = new GameObject("NetPlayer");
            temp.AddComponent<NetworkObject>();
            var nt = temp.AddComponent<NetworkTransform>();
            nt.SetSynchronizeScale(false);
            temp.AddComponent<NetPlayer>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath);
            Object.DestroyImmediate(temp);

            // 2. FishNet spawnable prefab list containing just that prefab
            var list = AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>(PrefabListPath);
            if (list == null)
            {
                list = ScriptableObject.CreateInstance<SinglePrefabObjects>();
                AssetDatabase.CreateAsset(list, PrefabListPath);
            }
            list.Clear();
            list.AddObject(prefab.GetComponent<NetworkObject>(), true, false);
            EditorUtility.SetDirty(list);

            // 3. sky material (referenced from Resources so it ships in builds)
            if (AssetDatabase.LoadAssetAtPath<Material>(SkyPath) == null)
            {
                var sky = new Material(Shader.Find("Skybox/Procedural"));
                sky.SetFloat("_AtmosphereThickness", 0.8f);
                sky.SetColor("_SkyTint", new Color(0.45f, 0.65f, 0.9f));
                sky.SetColor("_GroundColor", new Color(0.25f, 0.4f, 0.5f));
                sky.SetFloat("_Exposure", 1.25f);
                AssetDatabase.CreateAsset(sky, SkyPath);
            }

            // 4. the scene: NetworkManager (+ Tugboat + NetSession) and the Bootstrap
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var nmGo = new GameObject("NetworkManager");
            var nm = nmGo.AddComponent<NetworkManager>();
            nmGo.AddComponent<Tugboat>();
            nm.SpawnablePrefabs = list;
            var session = nmGo.AddComponent<NetSession>();
            session.PlayerPrefab = prefab.GetComponent<NetworkObject>();
            new GameObject("Bootstrap").AddComponent<Bootstrap>();
            RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.productName = "Brine Time";
            PlayerSettings.companyName = "Brine Time Team";
            PlayerSettings.runInBackground = true;   // keep simulating while alt-tabbed (needed for hosting)
            PlayerSettings.resizableWindow = true;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[BrineSetup] Generated prefab, prefab list, sky and " + ScenePath);
        }

        /// <summary>For CI / command line: Unity -batchmode -executeMethod BrineTime.EditorTools.BrineSetup.RebuildBatch -quit</summary>
        public static void RebuildBatch() => Rebuild();

        /// <summary>Unity -batchmode -executeMethod BrineTime.EditorTools.BrineSetup.BuildWindows -quit</summary>
        [MenuItem("Brine Time/Build Windows Player")]
        public static void BuildWindows()
        {
            if (!File.Exists(ScenePath)) Rebuild();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Windows/BrineTime.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            Debug.Log($"[BrineSetup] Build result: {report.summary.result}, {report.summary.totalErrors} errors, size {report.summary.totalSize / (1024 * 1024)} MB");
            if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }
    }
}
