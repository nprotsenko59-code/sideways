using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Sideways.Editor
{
    [InitializeOnLoad]
    public static class PrototypeStartup
    {
        private const string SandboxPath = "Assets/Sideways/Scenes/DrivingSandbox.unity";

        static PrototypeStartup()
        {
            EditorApplication.delayCall += OpenSandboxWhenEditorHasNoScene;
        }

        private static void OpenSandboxWhenEditorHasNoScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            var activeScene = SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(activeScene.path) || activeScene.isDirty) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SandboxPath) == null) return;

            EditorSceneManager.OpenScene(SandboxPath, OpenSceneMode.Single);
        }
    }
}
