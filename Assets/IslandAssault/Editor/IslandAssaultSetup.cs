using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace IslandAssault.EditorTools
{
    /// <summary>
    /// The game creates its materials in code. Unity strips shader variants that no asset uses from builds
    /// (e.g. transparent water, glowing emissive). This creates two small material assets in a Resources
    /// folder so those variants are always included in PC / Android / iOS builds.
    /// Runs automatically once; also available from the menu: Island Assault > Prepare for Build.
    /// </summary>
    [InitializeOnLoad]
    public static class IslandAssaultSetup
    {
        const string Dir = "Assets/IslandAssault/Resources";

        static IslandAssaultSetup()
        {
            EditorApplication.delayCall += () => Ensure(false);
        }

        [MenuItem("Island Assault/Prepare for Build")]
        static void EnsureMenu() { Ensure(true); }

        static void Ensure(bool log)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Material baseMat = DefaultMaterial();
            if (baseMat == null) return;
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/IslandAssault", "Resources");

            bool changed = false;
            if (AssetDatabase.LoadAssetAtPath<Material>(Dir + "/IA_KeepTransparent.mat") == null)
            {
                var m = new Material(baseMat);
                Art.MakeTransparent(m);
                AssetDatabase.CreateAsset(m, Dir + "/IA_KeepTransparent.mat");
                changed = true;
            }
            if (AssetDatabase.LoadAssetAtPath<Material>(Dir + "/IA_KeepEmissive.mat") == null)
            {
                var m = new Material(baseMat);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.white);
                AssetDatabase.CreateAsset(m, Dir + "/IA_KeepEmissive.mat");
                changed = true;
            }
            if (changed) AssetDatabase.SaveAssets();
            if (log) Debug.Log("[Island Assault] Ready to build. Shader variant materials are in " + Dir);
        }

        static Material DefaultMaterial()
        {
            var rp = GraphicsSettings.currentRenderPipeline;
            if (rp != null && rp.defaultMaterial != null) return rp.defaultMaterial;
            return AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
        }
    }
}
