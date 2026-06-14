using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FaseLucasGame;

namespace FaseLucasEditor
{
    /// <summary>
    /// Editor helpers to generate the FaseLucas scene. The level itself is constructed at
    /// runtime by <see cref="FaseLucasBootstrap"/>; these menu items just create/save a scene
    /// that contains the bootstrap object and register it in Build Settings.
    /// </summary>
    public static class FaseLucasBuilder
    {
        const string ScenePath = "Assets/Scenes/FaseLucas.unity";

        [MenuItem("Tools/Fase Lucas/Build && Save Scene")]
        public static void BuildAndSaveScene()
        {
            if (!EditorUtility.DisplayDialog("Build FaseLucas",
                    "This creates a fresh FaseLucas scene containing the level bootstrap and overwrites " +
                    ScenePath + ".\n\nContinue?", "Build", "Cancel"))
                return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var go = new GameObject("FaseLucas");
            go.AddComponent<FaseLucasBootstrap>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            EnsureIndustrialInBuildSettings();

            Debug.Log("FaseLucas scene created at " + ScenePath +
                      ". Press Play to generate and play the level.");
        }

        [MenuItem("Tools/Fase Lucas/Add Bootstrap To Current Scene")]
        public static void AddBootstrapToCurrentScene()
        {
            if (Object.FindFirstObjectByType<FaseLucasBootstrap>() != null)
            {
                Debug.Log("FaseLucasBootstrap already present in the scene.");
                return;
            }
            var go = new GameObject("FaseLucas");
            go.AddComponent<FaseLucasBootstrap>();
            Undo.RegisterCreatedObjectUndo(go, "Add FaseLucas Bootstrap");
            Selection.activeGameObject = go;
            Debug.Log("Added FaseLucas bootstrap. Press Play to build the level.");
        }

        static void AddSceneToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == path)) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void EnsureIndustrialInBuildSettings()
        {
            const string industrial = "Assets/Industrial_Zone_Modular_Pack/Scenes/Industrial_Zone.unity";
            if (System.IO.File.Exists(industrial))
                AddSceneToBuildSettings(industrial);
        }
    }
}
