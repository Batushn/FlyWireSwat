using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FlyWireSwat.EditorTools
{
    /// <summary>
    /// Turns the raw Poly Haven / Kenney downloads (CC0) into URP materials and prefabs.
    /// Poly Haven "arm" textures pack AO (R), roughness (G), metallic (B); URP Lit wants
    /// metallic in R and smoothness in A, so we bake a mask texture.
    /// </summary>
    public static class ThirdPartyImporter
    {
        const string PhRoot = "Assets/ThirdParty/PolyHaven";
        const string KenneyRoot = "Assets/ThirdParty/Kenney/BlasterKit";
        public const string PrefabDir = "Assets/Resources/ThirdParty/Prefabs";
        const string MatDir = PhRoot + "/Materials";

        [MenuItem("FlyWireSwat/Import Third-Party Assets")]
        public static void ImportAll()
        {
            Directory.CreateDirectory(PrefabDir);
            Directory.CreateDirectory(MatDir);
            AssetDatabase.Refresh();
            ImportHdri();
            foreach (var dir in Directory.GetDirectories(PhRoot + "/Models")) ImportModel(dir.Replace('\\', '/'));
            foreach (var dir in Directory.GetDirectories(PhRoot + "/Textures")) ImportTiledTexture(dir.Replace('\\', '/'));
            ImportKenney();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[FlyWireSwat] Third-party assets imported.");
        }

        static void ImportHdri()
        {
            foreach (var hdr in Directory.GetFiles(PhRoot + "/HDRIs", "*.hdr"))
            {
                string path = hdr.Replace('\\', '/');
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) continue;
                imp.textureShape = TextureImporterShape.TextureCube;
                imp.generateCubemap = TextureImporterGenerateCubemap.AutoCubemap;
                imp.sRGBTexture = false;
                imp.mipmapEnabled = true;
                imp.maxTextureSize = 2048;
                imp.SaveAndReimport();
                var cube = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
                string matPath = MatDir + "/Skybox_" + Path.GetFileNameWithoutExtension(path) + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null) { mat = new Material(Shader.Find("Skybox/Cubemap")); AssetDatabase.CreateAsset(mat, matPath); }
                mat.SetTexture("_Tex", cube);
                mat.SetFloat("_Exposure", 1.0f);
                mat.SetFloat("_Rotation", 0f);
                EditorUtility.SetDirty(mat);
            }
        }

        static Texture2D LoadReadable(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            tex.LoadImage(bytes);
            return tex;
        }

        /// <summary>Bake URP metallic(R)+smoothness(A) mask from a Poly Haven arm (AO/rough/metal) texture.</summary>
        static Texture2D BakeMask(string armPath, string outPath, int maxSize)
        {
            if (File.Exists(outPath)) return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
            var arm = LoadReadable(armPath);
            int w = arm.width, h = arm.height;
            var src = arm.GetPixels32();
            int step = Mathf.Max(1, Mathf.CeilToInt((float)w / maxSize));
            int ow = w / step, oh = h / step;
            var mask = new Texture2D(ow, oh, TextureFormat.RGBA32, false, true);
            var dst = new Color32[ow * oh];
            for (int y = 0; y < oh; y++)
                for (int x = 0; x < ow; x++)
                {
                    var c = src[(y * step) * w + x * step];
                    dst[y * ow + x] = new Color32(c.b, c.b, c.b, (byte)(255 - c.g)); // metallic, smoothness
                }
            mask.SetPixels32(dst); mask.Apply();
            File.WriteAllBytes(outPath, mask.EncodeToPNG());
            Object.DestroyImmediate(arm); Object.DestroyImmediate(mask);
            AssetDatabase.ImportAsset(outPath);
            var imp = (TextureImporter)AssetImporter.GetAtPath(outPath);
            imp.sRGBTexture = false; imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
        }

        static void SetNormal(string path)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null && imp.textureType != TextureImporterType.NormalMap) { imp.textureType = TextureImporterType.NormalMap; imp.SaveAndReimport(); }
        }

        static Material MakeLit(string matPath, string diff, string nor, string mask, Vector2 tiling)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, matPath); }
            if (diff != null) mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(diff));
            if (nor != null) { SetNormal(nor); mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(nor)); mat.EnableKeyword("_NORMALMAP"); }
            if (mask != null)
            {
                mat.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(mask));
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                mat.SetFloat("_Smoothness", 1f);
            }
            mat.SetTextureScale("_BaseMap", tiling);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void ImportModel(string dir)
        {
            string name = Path.GetFileName(dir);
            string fbx = $"{dir}/{name}.fbx";
            if (!File.Exists(fbx)) return;
            var mi = AssetImporter.GetAtPath(fbx) as ModelImporter;
            if (mi != null)
            {
                mi.materialImportMode = ModelImporterMaterialImportMode.None;
                mi.importAnimation = false;
                mi.isReadable = false;
                mi.generateSecondaryUV = true;
                mi.secondaryUVHardAngle = 88f; mi.secondaryUVPackMargin = 4f;
                mi.SaveAndReimport();
            }
            string diff = $"{dir}/{name}_diffuse.jpg";
            string nor = $"{dir}/{name}_nor_gl.jpg";
            string arm = $"{dir}/{name}_arm.jpg";
            string mask = File.Exists(arm) ? BakeMask(arm, $"{dir}/{name}_mask.png", 1024) != null ? $"{dir}/{name}_mask.png" : null : null;
            var mat = MakeLit($"{MatDir}/{name}.mat", File.Exists(diff) ? diff : null, File.Exists(nor) ? nor : null, mask, Vector2.one);

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            inst.name = name;
            foreach (var r in inst.GetComponentsInChildren<Renderer>())
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            foreach (var mf in inst.GetComponentsInChildren<MeshFilter>())
                if (mf.GetComponent<Collider>() == null) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            PrefabUtility.SaveAsPrefabAsset(inst, $"{PrefabDir}/{name}.prefab");
            Object.DestroyImmediate(inst);
        }

        static void ImportTiledTexture(string dir)
        {
            string name = Path.GetFileName(dir);
            string diff = $"{dir}/{name}_diffuse.jpg";
            string nor = $"{dir}/{name}_nor_gl.jpg";
            string arm = $"{dir}/{name}_arm.jpg";
            string mask = File.Exists(arm) ? $"{dir}/{name}_mask.png" : null;
            if (mask != null) BakeMask(arm, mask, 1024);
            MakeLit($"{MatDir}/Tiled_{name}.mat", File.Exists(diff) ? diff : null, File.Exists(nor) ? nor : null, mask, Vector2.one);
        }

        static void ImportKenney()
        {
            string tex = KenneyRoot + "/Models/Textures/variation-a.png";
            string matPath = MatDir + "/Kenney_BlasterKit.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, matPath); }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex));
            mat.SetFloat("_Smoothness", 0.35f);
            var timp = AssetImporter.GetAtPath(tex) as TextureImporter;
            if (timp != null) { timp.filterMode = FilterMode.Point; timp.SaveAndReimport(); }
            foreach (var fbx in Directory.GetFiles(KenneyRoot + "/Models/FBX format", "*.fbx"))
            {
                string path = fbx.Replace('\\', '/');
                var mi = AssetImporter.GetAtPath(path) as ModelImporter;
                if (mi != null && mi.materialImportMode != ModelImporterMaterialImportMode.None)
                {
                    mi.materialImportMode = ModelImporterMaterialImportMode.None;
                    mi.SaveAndReimport();
                }
                string name = Path.GetFileNameWithoutExtension(path);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null) continue;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
                foreach (var r in inst.GetComponentsInChildren<Renderer>())
                {
                    var mats = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                    r.sharedMaterials = mats;
                }
                PrefabUtility.SaveAsPrefabAsset(inst, $"{PrefabDir}/kenney_{name}.prefab");
                Object.DestroyImmediate(inst);
            }
        }

        /// <summary>Report bounds of every generated prefab so the scene builder can place things.</summary>
        [MenuItem("FlyWireSwat/Log Prefab Bounds")]
        public static void LogBounds()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var p in Directory.GetFiles(PrefabDir, "*.prefab"))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p.Replace('\\', '/'));
                var b = PrefabBounds(go);
                sb.AppendLine($"{go.name}: center={b.center:F3} size={b.size:F3}");
            }
            Debug.Log("[FlyWireSwat] Bounds\n" + sb);
        }

        public static Bounds PrefabBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(Vector3.zero, Vector3.zero);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }
    }
}
