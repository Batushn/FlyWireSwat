using System.IO;
using FlyWireSwat.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FlyWireSwat.EditorTools
{
    /// <summary>
    /// Builds the kitchen: room shell with Poly Haven PBR textures, dining table (top at world y=0 where
    /// the fly sits), chairs, cabinet, stove, food props, lamps, sun through the window, HDRI sky,
    /// post-processing volume. Everything is CC0 (see Assets/ThirdParty/*/CREDITS.md).
    /// </summary>
    public static class KitchenSceneBuilder
    {
        const string Prefabs = ThirdPartyImporter.PrefabDir + "/";
        const string Mats = "Assets/ThirdParty/PolyHaven/Materials/";
        const float TableTop = 0.877f;          // dining_table height -> table top sits at y = 0

        [MenuItem("FlyWireSwat/Build Kitchen Scene")]
        public static void Build()
        {
            var old = GameObject.Find("Kitchen");
            if (old != null) Undo.DestroyObjectImmediate(old);
            var root = new GameObject("Kitchen").transform;
            root.position = Vector3.zero;

            float floorY = -TableTop;
            // --- room shell (6 x 5 m, 2.7 m high), table roughly in the middle
            var floor = Quad("Floor", root, new Vector3(0f, floorY, 0.4f), Quaternion.Euler(90f, 0f, 0f), new Vector3(6f, 5f, 1f), Mats + "Tiled_wood_floor_worn.mat", new Vector2(3f, 2.5f));
            Quad("Ceiling", root, new Vector3(0f, floorY + 2.7f, 0.4f), Quaternion.Euler(-90f, 0f, 0f), new Vector3(6f, 5f, 1f), Mats + "Tiled_white_plaster_rough_02.mat", new Vector2(3f, 2.5f));
            // back wall (+Z) with a window hole: left piece, right piece, top piece, bottom piece
            string wall = Mats + "Tiled_plastered_wall_02.mat";
            Quad("WallBack_L", root, new Vector3(-2.15f, floorY + 1.35f, 2.9f), Quaternion.identity, new Vector3(1.7f, 2.7f, 1f), wall, new Vector2(0.85f, 1.35f));
            Quad("WallBack_R", root, new Vector3(2.15f, floorY + 1.35f, 2.9f), Quaternion.identity, new Vector3(1.7f, 2.7f, 1f), wall, new Vector2(0.85f, 1.35f));
            Quad("WallBack_Top", root, new Vector3(0f, floorY + 2.45f, 2.9f), Quaternion.identity, new Vector3(2.6f, 0.5f, 1f), wall, new Vector2(1.3f, 0.25f));
            Quad("WallBack_Bottom", root, new Vector3(0f, floorY + 0.45f, 2.9f), Quaternion.identity, new Vector3(2.6f, 0.9f, 1f), wall, new Vector2(1.3f, 0.45f));
            Quad("WallFront", root, new Vector3(0f, floorY + 1.35f, -2.1f), Quaternion.Euler(0f, 180f, 0f), new Vector3(6f, 2.7f, 1f), wall, new Vector2(3f, 1.35f));
            Quad("WallLeft", root, new Vector3(-3f, floorY + 1.35f, 0.4f), Quaternion.Euler(0f, -90f, 0f), new Vector3(5f, 2.7f, 1f), wall, new Vector2(2.5f, 1.35f));
            Quad("WallRight", root, new Vector3(3f, floorY + 1.35f, 0.4f), Quaternion.Euler(0f, 90f, 0f), new Vector3(5f, 2.7f, 1f), Mats + "Tiled_floor_tiles_06.mat", new Vector2(4f, 2.2f));
            // window frame (rollershutter model is 5.1 m wide with a weird pivot; use the glass + simple frame instead)
            var glass = Quad("WindowGlass", root, new Vector3(0f, floorY + 1.65f, 2.88f), Quaternion.identity, new Vector3(2.6f, 1.3f, 1f), null, Vector2.one);
            var glassMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            glassMat.SetColor("_BaseColor", new Color(0.7f, 0.85f, 1f, 0.12f));
            glassMat.SetFloat("_Surface", 1); glassMat.SetFloat("_Blend", 0); glassMat.SetFloat("_Smoothness", 0.95f);
            glassMat.SetOverrideTag("RenderType", "Transparent");
            glassMat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); glassMat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); glassMat.SetInt("_ZWrite", 0);
            glassMat.renderQueue = 3000; glassMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            var glassExisting = AssetDatabase.LoadAssetAtPath<Material>(Mats + "WindowGlass.mat");
            if (glassExisting == null) { AssetDatabase.CreateAsset(glassMat, Mats + "WindowGlass.mat"); glassExisting = glassMat; }
            glass.GetComponent<Renderer>().sharedMaterial = glassExisting;
            Object.DestroyImmediate(glass.GetComponent<Collider>());
            var frameMat = LitColor("WindowFrame", new Color(0.95f, 0.95f, 0.92f), 0.4f);
            Box("WindowFrame_T", root, new Vector3(0f, floorY + 2.32f, 2.87f), new Vector3(2.7f, 0.06f, 0.08f), frameMat);
            Box("WindowFrame_B", root, new Vector3(0f, floorY + 0.98f, 2.87f), new Vector3(2.7f, 0.08f, 0.14f), frameMat);
            Box("WindowFrame_L", root, new Vector3(-1.32f, floorY + 1.65f, 2.87f), new Vector3(0.06f, 1.4f, 0.08f), frameMat);
            Box("WindowFrame_R", root, new Vector3(1.32f, floorY + 1.65f, 2.87f), new Vector3(0.06f, 1.4f, 0.08f), frameMat);
            Box("WindowFrame_M", root, new Vector3(0f, floorY + 1.65f, 2.87f), new Vector3(0.04f, 1.4f, 0.06f), frameMat);

            // --- furniture
            var table = Place("dining_table", root, new Vector3(0f, floorY, 0f), 0f);
            var tb = ThirdPartyImporter.PrefabBounds(table);
            float topY = tb.max.y;
            if (Mathf.Abs(topY) > 0.01f) Debug.LogWarning($"table top at {topY:F3}, expected 0");
            table.AddComponent<LandingSurface>().Init(new Bounds(new Vector3(0f, topY, 0f), new Vector3(2.1f, 0.01f, 1.25f)));
            Place("dining_chair_02", root, new Vector3(-0.6f, floorY, -1.0f), 0f);
            Place("dining_chair_02", root, new Vector3(0.6f, floorY, -1.0f), 12f);
            Place("dining_chair_02", root, new Vector3(0.5f, floorY, 1.0f), 180f);
            Place("dining_chair_02", root, new Vector3(-0.7f, floorY, 1.05f), 170f);
            Place("modern_wooden_cabinet", root, new Vector3(-1.6f, floorY, 2.55f), 0f);
            Place("electric_stove", root, new Vector3(1.9f, floorY, 2.5f), 0f);
            Place("wooden_crate_01", root, new Vector3(-2.5f, floorY, -1.4f), 25f);
            Place("modern_ceiling_lamp_01", root, new Vector3(0f, floorY + 2.7f - 0.95f, 0.1f), 0f);
            Place("desk_lamp_arm_01", root, new Vector3(-1.5f, floorY + 0.68f, 2.55f), 200f);

            // --- props on the table (fly sits at the origin, keep a clear patch around it)
            Place("wooden_cutting_board", root, new Vector3(0.62f, 0f, 0.28f), 15f);
            Place("hamburger_buns", root, new Vector3(0.62f, 0.041f, 0.28f), 15f);
            Place("wooden_bowl_02", root, new Vector3(-0.55f, 0f, 0.35f), 0f);
            Place("food_apple_01", root, new Vector3(-0.8f, 0f, 0.1f), 30f);
            Place("lemon", root, new Vector3(-0.62f, 0.048f, 0.12f), 70f);
            Place("yellow_onion", root, new Vector3(-0.45f, 0f, 0.55f), 0f);
            Place("food_pears_asian_01", root, new Vector3(-0.85f, 0f, 0.45f), 0f);
            Place("bananas", root, new Vector3(0.55f, 0f, -0.35f), -35f);
            Place("croissant", root, new Vector3(-0.25f, 0f, -0.42f), 100f);
            Place("carrot_cake", root, new Vector3(0.85f, 0f, -0.15f), 0f);
            Place("tea_set_01", root, new Vector3(-0.45f, 0f, -0.25f), 190f);
            Place("metal_jug", root, new Vector3(0.9f, 0f, 0.5f), 0f);
            Place("wooden_spoon", root, new Vector3(0.3f, 0f, 0.5f), 120f);
            Place("wicker_basket_01", root, new Vector3(0.95f, floorY + 0.68f, 2.55f), 0f);
            Place("wine_bottles_01", root, new Vector3(-1.9f, floorY + 0.68f, 2.6f), 0f);
            Place("pot_enamel_01", root, new Vector3(1.85f, floorY + 0.86f, 2.5f), 0f);
            Place("all_purpose_cleaner", root, new Vector3(-0.95f, floorY + 0.68f, 2.6f), -20f);
            // a crumb next to the fly
            var crumb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crumb.name = "Crumb"; crumb.transform.SetParent(root, false);
            crumb.transform.localPosition = new Vector3(0.05f, 0.005f, 0.06f); crumb.transform.localScale = Vector3.one * 0.012f;
            crumb.GetComponent<Renderer>().sharedMaterial = LitColor("Crumb", new Color(0.72f, 0.48f, 0.2f), 0.2f);
            Object.DestroyImmediate(crumb.GetComponent<Collider>());

            // --- lighting: realtime direct + baked indirect (Mixed), soft warm sun through the window
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude)) if (l.type == LightType.Directional) Undo.DestroyObjectImmediate(l.gameObject);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(root, false);
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(32f, 150f, 0f);   // in through the +Z window, raking across the table
            sun.color = new Color(1f, 0.9f, 0.78f);
            sun.intensity = 1.1f;
            sun.lightmapBakeType = LightmapBakeType.Mixed;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sun.shadowBias = 0.02f; sun.shadowNormalBias = 0.3f;
            sun.gameObject.AddComponent<UniversalAdditionalLightData>().usePipelineSettings = true;

            var lamp = new GameObject("CeilingLampLight").AddComponent<Light>();
            lamp.transform.SetParent(root, false);
            lamp.transform.localPosition = new Vector3(0f, floorY + 2.7f - 0.92f, 0.1f);
            lamp.type = LightType.Point; lamp.range = 6f; lamp.intensity = 3.5f; lamp.color = new Color(1f, 0.9f, 0.78f);
            lamp.lightmapBakeType = LightmapBakeType.Mixed;
            lamp.shadows = LightShadows.Soft;
            lamp.gameObject.AddComponent<UniversalAdditionalLightData>();

            var sky = new GameObject("WindowSkyLight").AddComponent<Light>();
            sky.transform.SetParent(root, false);
            sky.transform.localPosition = new Vector3(0f, floorY + 1.8f, 2.7f);
            sky.transform.rotation = Quaternion.LookRotation(new Vector3(0f, -0.5f, -1f));
            sky.type = LightType.Spot; sky.spotAngle = 170f; sky.innerSpotAngle = 90f; sky.range = 8f; sky.intensity = 2.2f; sky.color = new Color(0.75f, 0.85f, 1f);
            sky.lightmapBakeType = LightmapBakeType.Baked;   // pure bounce/sky fill, baked only
            sky.shadows = LightShadows.None;
            sky.gameObject.AddComponent<UniversalAdditionalLightData>();

            var skyMat = AssetDatabase.LoadAssetAtPath<Material>(Mats + "Skybox_kloofendal_48d_partly_cloudy_puresky_2k.mat");
            RenderSettings.skybox = skyMat;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 0.55f;
            RenderSettings.sun = sun;
            RenderSettings.fog = false;
            var probe = new GameObject("ReflectionProbe").AddComponent<ReflectionProbe>();
            probe.transform.SetParent(root, false);
            probe.transform.localPosition = new Vector3(0f, 0.5f, 0.3f);
            probe.size = new Vector3(6f, 3f, 5f);
            probe.mode = ReflectionProbeMode.Baked;
            probe.resolution = 256;
            probe.boxProjection = true;

            // light probes for the dynamic fly / weapons
            var lpg = new GameObject("LightProbes").AddComponent<LightProbeGroup>();
            lpg.transform.SetParent(root, false);
            var pts = new System.Collections.Generic.List<Vector3>();
            for (float x = -2.5f; x <= 2.5f; x += 1.25f)
                for (float z = -1.8f; z <= 2.6f; z += 1.1f)
                    foreach (float y in new[] { floorY + 0.3f, 0.05f, 0.35f, 1.0f, floorY + 2.5f })
                        pts.Add(new Vector3(x, y, z));
            // dense cluster around the fly spot
            for (float x = -0.6f; x <= 0.6f; x += 0.3f) for (float z = -0.5f; z <= 0.5f; z += 0.25f) foreach (float y in new[] { 0.02f, 0.15f, 0.4f }) pts.Add(new Vector3(x, y, z));
            lpg.probePositions = pts.ToArray();

            ConfigureLightmapping();

            // --- post processing
            var vol = GameObject.Find("Global Volume");
            if (vol == null) { vol = new GameObject("Global Volume"); vol.AddComponent<Volume>().isGlobal = true; }
            vol.transform.SetParent(null);
            var v = vol.GetComponent<Volume>();
            v.sharedProfile = BuildProfile();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[FlyWireSwat] Kitchen built. Save the scene.");
        }

        static void ConfigureLightmapping()
        {
            var ls = new LightingSettings
            {
                lightmapper = LightingSettings.Lightmapper.ProgressiveGPU,
                lightmapResolution = 40f,
                lightmapPadding = 2,
                lightmapMaxSize = 2048,
                lightmapCompression = LightmapCompression.HighQuality,
                indirectSampleCount = 256,
                directSampleCount = 32,
                environmentSampleCount = 256,
                maxBounces = 3,
                ao = true,
                aoMaxDistance = 0.6f,
                aoExponentIndirect = 1f,
                filteringMode = LightingSettings.FilterMode.Auto,
                mixedBakeMode = MixedLightingMode.IndirectOnly,
                realtimeGI = false,
                bakedGI = true,
            };
            const string path = "Assets/Settings/FlyWireSwat_Lighting.lighting";
            var existing = AssetDatabase.LoadAssetAtPath<LightingSettings>(path);
            if (existing == null) AssetDatabase.CreateAsset(ls, path); else { EditorUtility.CopySerialized(ls, existing); ls = existing; }
            Lightmapping.lightingSettings = ls;
        }

        static VolumeProfile BuildProfile()
        {
            const string path = "Assets/Settings/FlyWireSwat_PostFX.asset";
            var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (p != null) return p;
            p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, path);
            var tone = p.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
            var bloom = p.Add<Bloom>(true); bloom.intensity.Override(0.25f); bloom.threshold.Override(1.0f); bloom.scatter.Override(0.7f);
            var ca = p.Add<ColorAdjustments>(true); ca.postExposure.Override(0.1f); ca.contrast.Override(8f); ca.saturation.Override(5f);
            var vig = p.Add<Vignette>(true); vig.intensity.Override(0.22f); vig.smoothness.Override(0.5f);
            var dof = p.Add<DepthOfField>(true); dof.mode.Override(DepthOfFieldMode.Bokeh); dof.focusDistance.Override(0.45f); dof.aperture.Override(11f); dof.focalLength.Override(40f);
            var grain = p.Add<FilmGrain>(true); grain.intensity.Override(0.06f); grain.type.Override(FilmGrainLookup.Thin1);
            var wb = p.Add<WhiteBalance>(true); wb.temperature.Override(6f);
            EditorUtility.SetDirty(p); AssetDatabase.SaveAssets();
            return p;
        }

        static Material LitColor(string name, Color c, float smooth)
        {
            string path = Mats + "Color_" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth);
                AssetDatabase.CreateAsset(m, path);
            }
            return m;
        }

        static GameObject Quad(string name, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale, string matPath, Vector2 tiling)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
            g.name = name; g.transform.SetParent(parent, false);
            g.transform.localPosition = pos; g.transform.localRotation = rot; g.transform.localScale = scale;
            GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
            if (matPath != null)
            {
                var src = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                var m = new Material(src);
                m.SetTextureScale("_BaseMap", tiling);
                string p = matPath.Replace(".mat", "_" + name + ".mat");
                var existing = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (existing == null) { AssetDatabase.CreateAsset(m, p); existing = m; }
                g.GetComponent<Renderer>().sharedMaterial = existing;
            }
            return g;
        }

        static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material m)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name; g.transform.SetParent(parent, false);
            g.transform.localPosition = pos; g.transform.localScale = size;
            GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.ContributeGI | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
            g.GetComponent<Renderer>().sharedMaterial = m;
            return g;
        }

        static GameObject Place(string prefab, Transform parent, Vector3 pos, float yaw)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + prefab + ".prefab");
            if (asset == null) { Debug.LogWarning("missing prefab " + prefab); return new GameObject(prefab); }
            var g = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            g.transform.localPosition = pos;
            // put the bottom of the rendered bounds exactly on the requested height (pivots differ per model)
            var b = ThirdPartyImporter.PrefabBounds(g);
            g.transform.localPosition = new Vector3(pos.x, pos.y - (b.min.y - g.transform.position.y), pos.z);
            foreach (var t in g.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.ContributeGI | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
            return g;
        }
    }
}
