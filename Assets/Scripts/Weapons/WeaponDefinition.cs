using FlyWireSwat.Fly;
using FlyWireSwat.Sim;
using UnityEngine;

namespace FlyWireSwat.Weapons
{
    public enum WeaponShape { Slab, Rod, Sphere, Cloud, Cone, Bullet, Rocket, Flat }
    public enum ItemCategory { Killer, Repellent }
    public enum ItemBehaviour { Melee, Gun, Rocket, Spray, Trap, Cat, Vacuum, Placeable }
    public enum Verdict { Myth, Weak, Works, Kills }

    /// <summary>
    /// Everything the simulation needs to know about one way of killing (or repelling) a fly.
    /// Physical numbers are rough real-world estimates; tweak freely in the Inspector.
    /// </summary>
    [CreateAssetMenu(menuName = "FlyWireSwat/Weapon", fileName = "Weapon")]
    public class WeaponDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id = "swatter";                 // stable key (results, file names)
        public string nameEn = "Fly swatter", nameTr = "Sineklik";
        [TextArea] public string descriptionEn, descriptionTr;
        public string emoji = "";
        public ItemCategory category = ItemCategory.Killer;
        public ItemBehaviour behaviour = ItemBehaviour.Melee;

        [Header("Visual")]
        public Color color = Color.white;
        public WeaponShape shape = WeaponShape.Slab;
        public Vector3 visualScale = new Vector3(0.12f, 0.005f, 0.12f);
        [Tooltip("Resources path of a model/prefab (e.g. Models/swatter). Empty = procedural.")] public string modelPath = "";
        public float modelScale = 1f;
        public Vector3 modelEuler = Vector3.zero;
        public Vector3 modelOffset = Vector3.zero;

        [Header("Looming stimulus (what the fly sees)")]
        public StimulusKind stimulusKind = StimulusKind.Approach;
        public float objectRadius = 0.06f;
        public float startDistance = 0.5f;
        public float windupSpeed = 0.4f;
        public float strikeDistance = 0.3f;
        public float strikeSpeed = 5f;
        public float cloudGrowth = 0f;
        [Range(0f, 1f)] public float visibility = 1f;

        [Header("Lethality")]
        public float lethalRadius = 0.06f;
        public float aimSigma = 0.02f;
        [Range(0f, 1f)] public float killProbabilityInside = 0.9f;
        [Range(0f, 1f)] public float airborneKillFactor = 0.3f;
        public float killDelaySeconds = 0f;
        public float passiveMeanWaitSeconds = 0f;

        [Header("Logistics")]
        public float setupSeconds = 2f;
        public float retrySeconds = 1.5f;
        public int maxAttempts = 15;
        public float costPerAttempt = 0f;
        public float collateralPerAttempt = 0f;
        public bool scaresFlyOnMiss = true;

        [Header("Repellent / attractant (category = Repellent)")]
        [Range(0f, 1f), Tooltip("Probability a fly that would land within repelRadius goes elsewhere")] public float repelStrength = 0f;
        public float repelRadius = 0.5f;
        [Range(0f, 1f), Tooltip("Probability a passing fly is lured to within attractRadius")] public float attractStrength = 0f;
        public float attractRadius = 0.8f;
        [Tooltip("Kills flies that land within contactRadius (UV zapper, vinegar trap)")] public bool killsOnContact = false;
        public float contactRadius = 0.06f;
        public float runningCostPerHour = 0f;
        public float purchaseCost = 0f;
        public Verdict expectedVerdict = Verdict.Myth;
        [TextArea] public string evidenceEn, evidenceTr;
        [Tooltip("FPS: wind push on airborne flies (m/s^2) within repelRadius")] public float windPush = 0f;
        public Color lightColor = Color.clear;   // e.g. purple UV glow
        public bool emitsSmoke = false;

        public string displayName => L10n.Pick(string.IsNullOrEmpty(nameEn) ? id : nameEn, string.IsNullOrEmpty(nameTr) ? nameEn : nameTr);
        public string description => L10n.Pick(descriptionEn, string.IsNullOrEmpty(descriptionTr) ? descriptionEn : descriptionTr);
        public string evidence => L10n.Pick(evidenceEn, string.IsNullOrEmpty(evidenceTr) ? evidenceEn : evidenceTr);
        public bool IsKiller => category == ItemCategory.Killer;

        public StimulusSpec BuildStimulus(float azimuthDeg, float elevationDeg, uint seed)
        {
            return new StimulusSpec
            {
                kind = stimulusKind,
                objectRadius = objectRadius,
                startDistance = startDistance,
                windupSpeed = windupSpeed,
                strikeDistance = Mathf.Min(strikeDistance, startDistance),
                strikeSpeed = strikeSpeed,
                cloudGrowth = cloudGrowth,
                azimuthDeg = azimuthDeg,
                elevationDeg = elevationDeg,
                visibility = visibility,
                seed = seed,
            };
        }
    }
}
