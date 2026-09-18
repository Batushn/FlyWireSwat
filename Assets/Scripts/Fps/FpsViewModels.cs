using FlyWireSwat.Sim;
using FlyWireSwat.Weapons;
using UnityEngine;

namespace FlyWireSwat.Fps
{
    /// <summary>Loads the item's model from Resources (Blender-made props, Kenney blasters, Poly Haven bottle) or falls back to primitives.</summary>
    public static class FpsViewModels
    {
        /// <summary>Instantiates the item's model under a fresh root; the root's forward (+Z) is the business end.</summary>
        public static GameObject Build(WeaponDefinition w, bool forHand)
        {
            var root = new GameObject("Model_" + w.id);
            GameObject inst = null;
            if (!string.IsNullOrEmpty(w.modelPath))
            {
                var asset = Resources.Load<GameObject>(w.modelPath);
                if (asset != null)
                {
                    inst = Object.Instantiate(asset, root.transform);
                    foreach (var c in inst.GetComponentsInChildren<Collider>()) Object.Destroy(c);
                    inst.transform.localScale = Vector3.one * w.modelScale;
                    inst.transform.localPosition = w.modelOffset;
                    inst.transform.localRotation = Quaternion.Euler(w.modelEuler);
                    FixMaterials(inst);
                }
            }
            if (inst == null)
            {
                inst = ShowcaseView.BuildPrimitiveWeapon(w);
                inst.transform.SetParent(root.transform, false);
            }
            if (forHand) ApplyHandPose(w, root.transform);
            return root;
        }

        /// <summary>Blender FBX materials arrive opaque; give glass/water a transparent surface.</summary>
        static void FixMaterials(GameObject inst)
        {
            foreach (var r in inst.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i]; if (m == null) continue;
                    string n = m.name.ToLowerInvariant();
                    if (n.Contains("water") || n.Contains("jar") || n.Contains("wrap") || n.Contains("fanblade") || n.Contains("uvtube"))
                    {
                        var c = m.color; c.a = n.Contains("uvtube") ? 0.85f : 0.35f;
                        var t = ShowcaseView.MakeTransparent(c);
                        t.SetFloat("_Smoothness", 0.95f);
                        if (n.Contains("uvtube")) { t.EnableKeyword("_EMISSION"); t.SetColor("_EmissionColor", new Color(0.5f, 0.2f, 1f) * 2.5f); }
                        mats[i] = t;
                    }
                    else if (n.Contains("ember")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", new Color(1f, 0.3f, 0.05f) * 3f); }
                }
                r.sharedMaterials = mats;
            }
        }

        static void ApplyHandPose(WeaponDefinition w, Transform t)
        {
            switch (w.behaviour)
            {
                case ItemBehaviour.Melee:
                    t.localRotation = Quaternion.Euler(-35f, 0f, 0f);
                    t.localPosition = new Vector3(0f, 0f, 0.05f);
                    if (w.id == "hand") { t.localRotation = Quaternion.Euler(-70f, 15f, 0f); t.localPosition = new Vector3(0.03f, -0.04f, 0.06f); }
                    if (w.id == "towel") { t.localRotation = Quaternion.Euler(-10f, 0f, 30f); }
                    break;
                case ItemBehaviour.Vacuum: t.localRotation = Quaternion.Euler(-5f, 0f, 0f); t.localPosition = new Vector3(0f, -0.05f, 0.15f); break;
                case ItemBehaviour.Cat: t.localRotation = Quaternion.Euler(0f, 180f, 0f); t.localPosition = new Vector3(0f, -0.1f, 0.05f); break;
                case ItemBehaviour.Placeable: t.localRotation = Quaternion.Euler(0f, 30f, 0f); t.localPosition = new Vector3(0f, -0.12f, 0.02f); t.localScale = Vector3.one * 0.6f; break;
                case ItemBehaviour.Trap: t.localRotation = Quaternion.Euler(70f, 0f, 0f); break;
                default: break;
            }
        }
    }
}
