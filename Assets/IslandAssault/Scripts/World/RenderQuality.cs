using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace IslandAssault
{
    /// <summary>
    /// Makes the game look good out of the box with URP: long soft shadows, anti-aliasing,
    /// and a post-processing look (bright tropical colors, soft bloom, subtle vignette).
    /// </summary>
    public static class RenderQuality
    {
        public static void Apply(Camera cam)
        {
            // --- Pipeline asset (the active quality level's URP asset) ---
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null) urp = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            if (urp != null)
            {
                urp.shadowDistance = 160f;       // default is ~50, which cut off all shadows at our camera distance
                urp.shadowCascadeCount = 2;
                urp.msaaSampleCount = 4;
                urp.renderScale = 1f;
            }

            // --- Camera ---
            if (cam != null)
            {
                cam.allowHDR = true;
                cam.allowMSAA = true;
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null)
                {
                    data.renderPostProcessing = true;
                    data.renderShadows = true;
                    data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                    data.antialiasingQuality = AntialiasingQuality.High;
                }
            }

            // --- Post-processing ---
            var go = new GameObject("PostProcessing");
            Object.DontDestroyOnLoad(go);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100f;   // wins over the template's Global Volume
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.value = TonemappingMode.Neutral;

            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.value = 0.1f;
            color.contrast.value = 8f;
            color.saturation.value = 4f;

            var wb = profile.Add<WhiteBalance>(true);
            wb.temperature.value = 4f;

            // the URP template's volume may blur the top/bottom of the screen (depth of field): turn it off
            var dof = profile.Add<DepthOfField>(true);
            dof.mode.value = DepthOfFieldMode.Off;

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.value = 1.0f;
            bloom.intensity.value = 0.45f;
            bloom.scatter.value = 0.65f;

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.value = 0.22f;
            vignette.smoothness.value = 0.5f;

            volume.sharedProfile = profile;
        }
    }
}
