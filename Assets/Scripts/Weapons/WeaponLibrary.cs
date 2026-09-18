using System.Collections.Generic;
using FlyWireSwat.Fly;
using UnityEngine;

namespace FlyWireSwat.Weapons
{
    /// <summary>Built-in arsenal and repellent list, created in memory so the scene needs no asset wiring.</summary>
    public static class WeaponLibrary
    {
        static WeaponDefinition Make(string id, string en, string tr, string emoji, string descEn, string descTr, Color color, System.Action<WeaponDefinition> cfg)
        {
            var w = ScriptableObject.CreateInstance<WeaponDefinition>();
            w.name = id; w.id = id; w.nameEn = en; w.nameTr = tr; w.emoji = emoji;
            w.descriptionEn = descEn; w.descriptionTr = descTr; w.color = color;
            cfg(w);
            return w;
        }

        public static List<WeaponDefinition> CreateDefaults()
        {
            return new List<WeaponDefinition>
            {
                Make("hand", "Bare hand", "Çıplak el", "✋", "The classic. ~10 cm palm, slow raise, fast slap.", "Klasik. Avuç ~10 cm, yavaş kalkış, hızlı iniş.",
                    new Color(0.87f, 0.62f, 0.48f), w =>
                    {
                        w.behaviour = ItemBehaviour.Melee; w.shape = WeaponShape.Flat; w.modelPath = "Models/hand"; w.modelScale = 1f; w.modelEuler = new Vector3(0f, 0f, 0f);
                        w.objectRadius = 0.05f; w.startDistance = 0.45f; w.windupSpeed = 0.5f; w.strikeDistance = 0.25f; w.strikeSpeed = 3.5f;
                        w.lethalRadius = 0.04f; w.aimSigma = 0.02f; w.killProbabilityInside = 0.6f; w.airborneKillFactor = 0.15f;
                        w.setupSeconds = 0f; w.retrySeconds = 1.2f; w.maxAttempts = 20;
                    }),
                Make("newspaper", "Rolled newspaper", "Rulo gazete", "📰", "Stiff, narrow, fast. The fly still sees it coming.", "Sert, dar, hızlı. Sinek yine de kalkışı görüyor.",
                    new Color(0.86f, 0.84f, 0.76f), w =>
                    {
                        w.behaviour = ItemBehaviour.Melee; w.shape = WeaponShape.Rod; w.modelPath = "Models/newspaper";
                        w.objectRadius = 0.045f; w.startDistance = 0.5f; w.windupSpeed = 0.6f; w.strikeDistance = 0.3f; w.strikeSpeed = 6f;
                        w.lethalRadius = 0.045f; w.aimSigma = 0.02f; w.killProbabilityInside = 0.9f; w.airborneKillFactor = 0.25f;
                        w.setupSeconds = 3f; w.retrySeconds = 1.0f; w.maxAttempts = 20; w.collateralPerAttempt = 0.2f;
                    }),
                Make("swatter", "Fly swatter", "Sineklik", "🏓", "Perforated head pushes little air; the fly notices late.", "Delikli plastik kafa az hava iter, sinek geç fark eder.",
                    new Color(0.85f, 0.15f, 0.12f), w =>
                    {
                        w.behaviour = ItemBehaviour.Melee; w.shape = WeaponShape.Slab; w.modelPath = "Models/swatter";
                        w.objectRadius = 0.06f; w.startDistance = 0.5f; w.windupSpeed = 0.7f; w.strikeDistance = 0.3f; w.strikeSpeed = 7f; w.visibility = 0.7f;
                        w.lethalRadius = 0.06f; w.aimSigma = 0.02f; w.killProbabilityInside = 0.92f; w.airborneKillFactor = 0.35f;
                        w.setupSeconds = 4f; w.retrySeconds = 0.8f; w.maxAttempts = 25;
                    }),
                Make("racket", "Electric racket", "Elektrikli raket", "🎾", "Wide, zaps in mid-air too. Slow but merciless.", "Geniş, havada da çarpar. Yavaş ama affetmez.",
                    new Color(0.98f, 0.8f, 0.1f), w =>
                    {
                        w.behaviour = ItemBehaviour.Melee; w.shape = WeaponShape.Slab; w.modelPath = "Models/racket";
                        w.objectRadius = 0.09f; w.startDistance = 0.5f; w.windupSpeed = 0.5f; w.strikeDistance = 0.3f; w.strikeSpeed = 3.5f; w.visibility = 0.6f;
                        w.lethalRadius = 0.09f; w.aimSigma = 0.03f; w.killProbabilityInside = 0.95f; w.airborneKillFactor = 0.9f;
                        w.setupSeconds = 6f; w.retrySeconds = 1.0f; w.maxAttempts = 25; w.costPerAttempt = 0.001f;
                    }),
                Make("towel", "Towel snap", "Havlu şaklatma", "🧣", "Tip at 15 m/s. Tiny target, big ego.", "Ucu 15 m/s. Küçük hedef, büyük ego.",
                    new Color(0.35f, 0.6f, 0.85f), w =>
                    {
                        w.behaviour = ItemBehaviour.Melee; w.shape = WeaponShape.Rod; w.modelPath = "Models/towel";
                        w.objectRadius = 0.025f; w.startDistance = 0.6f; w.windupSpeed = 1.0f; w.strikeDistance = 0.35f; w.strikeSpeed = 15f;
                        w.lethalRadius = 0.02f; w.aimSigma = 0.045f; w.killProbabilityInside = 0.8f; w.airborneKillFactor = 0.2f;
                        w.setupSeconds = 5f; w.retrySeconds = 2.0f; w.maxAttempts = 20; w.collateralPerAttempt = 0.5f;
                    }),
                Make("vacuum", "Vacuum cleaner", "Elektrik süpürgesi", "🌀", "Approaches slowly but sucks airborne flies in.", "Yavaş yaklaşır ama havadaki sineği de çeker.",
                    new Color(0.35f, 0.36f, 0.4f), w =>
                    {
                        w.behaviour = ItemBehaviour.Vacuum; w.shape = WeaponShape.Cone; w.modelPath = "Models/vacuum";
                        w.objectRadius = 0.02f; w.startDistance = 0.5f; w.windupSpeed = 0.3f; w.strikeDistance = 0.12f; w.strikeSpeed = 0.5f;
                        w.lethalRadius = 0.05f; w.aimSigma = 0.02f; w.killProbabilityInside = 0.85f; w.airborneKillFactor = 0.85f;
                        w.setupSeconds = 25f; w.retrySeconds = 3f; w.maxAttempts = 15; w.costPerAttempt = 0.01f;
                    }),
                Make("spray", "Insect spray", "Sinek ilacı", "🧴", "The cloud grows slowly; the fly drops 30-90 s later.", "Bulut yavaş büyür, sinek 30-90 sn sonra düşer.",
                    new Color(0.6f, 0.9f, 0.6f), w =>
                    {
                        w.behaviour = ItemBehaviour.Spray; w.shape = WeaponShape.Cloud; w.modelPath = "ThirdParty/Prefabs/all_purpose_cleaner"; w.modelScale = 0.8f; w.modelEuler = new Vector3(0f, 180f, 0f); w.modelOffset = new Vector3(0f, -0.2f, 0f);
                        w.stimulusKind = StimulusKind.ExpandingCloud; w.objectRadius = 0.02f; w.startDistance = 0.4f; w.cloudGrowth = 0.7f; w.visibility = 0.35f;
                        w.lethalRadius = 0.45f; w.aimSigma = 0.05f; w.killProbabilityInside = 0.85f; w.airborneKillFactor = 0.7f; w.killDelaySeconds = 45f;
                        w.setupSeconds = 8f; w.retrySeconds = 2f; w.maxAttempts = 6; w.costPerAttempt = 0.25f; w.collateralPerAttempt = 3f;
                    }),
                Make("flypaper", "Sticky fly paper", "Yapışkan sinek kağıdı", "🍯", "Do nothing, wait. Can take hours.", "Hiçbir şey yapma, bekle. Saatler sürebilir.",
                    new Color(0.95f, 0.7f, 0.2f), w =>
                    {
                        w.behaviour = ItemBehaviour.Trap; w.shape = WeaponShape.Flat; w.visualScale = new Vector3(0.2f, 0.002f, 0.06f);
                        w.stimulusKind = StimulusKind.Passive; w.passiveMeanWaitSeconds = 3600f; w.lethalRadius = 0f; w.killProbabilityInside = 0.9f;
                        w.setupSeconds = 30f; w.retrySeconds = 0f; w.maxAttempts = 1; w.costPerAttempt = 0.5f;
                    }),
                Make("cat", "Cat", "Kedi", "🐈", "Nature's own mechanism. Low accuracy, high mess.", "Doğal av mekanizması. İsabet düşük, ortalık dağılır.",
                    new Color(0.42f, 0.36f, 0.33f), w =>
                    {
                        w.behaviour = ItemBehaviour.Cat; w.shape = WeaponShape.Sphere; w.modelPath = "Models/cat"; w.modelScale = 0.6f;
                        w.objectRadius = 0.04f; w.startDistance = 0.6f; w.windupSpeed = 0.8f; w.strikeDistance = 0.25f; w.strikeSpeed = 4f;
                        w.lethalRadius = 0.05f; w.aimSigma = 0.03f; w.killProbabilityInside = 0.35f; w.airborneKillFactor = 0.5f;
                        w.setupSeconds = 15f; w.retrySeconds = 4f; w.maxAttempts = 12; w.collateralPerAttempt = 4f;
                    }),
                Make("pistol", "Pistol (9mm)", "Tabanca (9mm)", "🔫", "Bullet at 360 m/s: invisible, but the target is 3 mm.", "Mermi 360 m/s: görülmez, ama hedef 3 mm.",
                    new Color(0.2f, 0.2f, 0.22f), w =>
                    {
                        w.behaviour = ItemBehaviour.Gun; w.shape = WeaponShape.Bullet; w.visualScale = new Vector3(0.009f, 0.009f, 0.02f); w.modelPath = "ThirdParty/Prefabs/kenney_blaster-k"; w.modelScale = 0.35f; w.modelOffset = new Vector3(0f, -0.05f, 0f);
                        w.objectRadius = 0.0045f; w.startDistance = 3f; w.windupSpeed = 0f; w.strikeDistance = 3f; w.strikeSpeed = 360f;
                        w.lethalRadius = 0.0075f; w.aimSigma = 0.04f; w.killProbabilityInside = 1f; w.airborneKillFactor = 1f;
                        w.setupSeconds = 5f; w.retrySeconds = 1.0f; w.maxAttempts = 17; w.costPerAttempt = 0.4f; w.collateralPerAttempt = 40f;
                    }),
                Make("shotgun", "Shotgun", "Pompalı tüfek", "💥", "300 pellets, 10 cm pattern. The wall goes.", "300 saçma, 10 cm desen. Duvar gider.",
                    new Color(0.45f, 0.3f, 0.15f), w =>
                    {
                        w.behaviour = ItemBehaviour.Gun; w.shape = WeaponShape.Cone; w.visualScale = new Vector3(0.1f, 0.1f, 0.05f); w.modelPath = "ThirdParty/Prefabs/kenney_blaster-q"; w.modelScale = 0.42f; w.modelOffset = new Vector3(0f, -0.06f, 0.05f);
                        w.objectRadius = 0.05f; w.startDistance = 3f; w.windupSpeed = 0f; w.strikeDistance = 3f; w.strikeSpeed = 400f;
                        w.lethalRadius = 0.10f; w.aimSigma = 0.05f; w.killProbabilityInside = 0.24f; w.airborneKillFactor = 1f;
                        w.setupSeconds = 6f; w.retrySeconds = 1.5f; w.maxAttempts = 8; w.costPerAttempt = 1.2f; w.collateralPerAttempt = 250f;
                    }),
                Make("bazooka", "Bazooka (RPG-7)", "Bazuka (RPG-7)", "🚀", "l/v = 0.3 ms. The fly never sees it. Neither does the house.", "l/v = 0.3 ms. Sinek asla göremez. Ev de kalmaz.",
                    new Color(0.3f, 0.45f, 0.25f), w =>
                    {
                        w.behaviour = ItemBehaviour.Rocket; w.shape = WeaponShape.Rocket; w.visualScale = new Vector3(0.08f, 0.08f, 0.9f); w.modelPath = "ThirdParty/Prefabs/kenney_blaster-e"; w.modelScale = 0.5f; w.modelOffset = new Vector3(-0.05f, -0.08f, -0.3f);
                        w.objectRadius = 0.04f; w.startDistance = 10f; w.windupSpeed = 0f; w.strikeDistance = 10f; w.strikeSpeed = 115f;
                        w.lethalRadius = 3f; w.aimSigma = 0.3f; w.killProbabilityInside = 1f; w.airborneKillFactor = 1f;
                        w.setupSeconds = 20f; w.retrySeconds = 10f; w.maxAttempts = 2; w.costPerAttempt = 2500f; w.collateralPerAttempt = 150000f;
                    }),
            };
        }

        /// <summary>
        /// Folk remedies and commercial repellents. Numbers are our best reading of the literature;
        /// "evidence" strings say why. These are scored by a separate landing simulation, not by kills.
        /// </summary>
        public static List<WeaponDefinition> CreateRepellents()
        {
            return new List<WeaponDefinition>
            {
                Make("waterbag", "Bag of water + coins", "Sirkeli su torbası", "💧", "Hung over the table: the refraction is supposed to scare flies.", "Masanın üstüne asılır: ışığı kırıp sineği korkutması beklenir.",
                    new Color(0.7f, 0.85f, 1f), w =>
                    {
                        w.category = ItemCategory.Repellent; w.behaviour = ItemBehaviour.Placeable; w.modelPath = "Models/water_bag";
                        w.repelStrength = 0.0f; w.repelRadius = 0.6f; w.purchaseCost = 0.1f; w.expectedVerdict = Verdict.Myth;
                        w.evidenceEn = "Controlled tests (incl. Mythbusters, NC State ext.) found no reduction in fly activity."; w.evidenceTr = "Kontrollü testlerde (Mythbusters, NC State) sinek aktivitesinde azalma bulunmadı.";
                    }),
                Make("coffee", "Burning coffee grounds", "Kahve telvesi yakma", "☕", "Smoldering grounds make a smoky, bitter smell.", "Tüten telve dumanlı, acı bir koku verir.",
                    new Color(0.25f, 0.15f, 0.08f), w =>
                    {
                        w.category = ItemCategory.Repellent; w.behaviour = ItemBehaviour.Placeable; w.modelPath = "Models/coffee_bowl"; w.emitsSmoke = true;
                        w.repelStrength = 0.18f; w.repelRadius = 0.6f; w.purchaseCost = 0.2f; w.expectedVerdict = Verdict.Weak;
                        w.evidenceEn = "Smoke in general mildly repels flies; no specific study on coffee. Effect fades in minutes."; w.evidenceTr = "Duman genel olarak hafif kovucu; kahveye özel çalışma yok. Etkisi dakikalar içinde geçer.";
                    }),
                Make("deet", "DEET spray", "DEET sprey", "🧪", "The mosquito repellent, applied around the table.", "Sivrisinek kovucusu, masanın etrafına sıkılır.",
                    new Color(0.15f, 0.45f, 0.25f), w =>
                    {
                        w.category = ItemCategory.Repellent; w.behaviour = ItemBehaviour.Placeable; w.modelPath = "Models/deet";
                        w.repelStrength = 0.5f; w.repelRadius = 0.5f; w.purchaseCost = 6f; w.expectedVerdict = Verdict.Works;
                        w.evidenceEn = "DEET is proven for mosquitoes; house-fly repellency is moderate in lab tests (~50%)."; w.evidenceTr = "DEET sivrisinekte kanıtlı; karasinekte lab testlerinde orta düzeyde (~%50) kovuculuk.";
                    }),
                Make("lemoncloves", "Lemon with cloves", "Limon + karanfil", "🍋", "Half a lemon studded with cloves (eugenol).", "Karanfil saplanmış yarım limon (öjenol).",
                    new Color(0.95f, 0.85f, 0.15f), w =>
                    {
                        w.category = ItemCategory.Repellent; w.behaviour = ItemBehaviour.Placeable; w.modelPath = "Models/lemon_cloves";
                        w.repelStrength = 0.22f; w.repelRadius = 0.4f; w.purchaseCost = 0.5f; w.expectedVerdict = Verdict.Weak;
                        w.evidenceEn = "Clove oil (eugenol) repels flies at high concentration in lab; a studded lemon gives very little vapour."; w.evidenceTr = "Karanfil yağı (öjenol) labda yüksek dozda kovucu; limona saplı karanfil çok az buhar verir.";
                    }),
                Make("ultrasonic", "Ultrasonic repeller", "Ultrasonik kovucu", "📡", "A plug-in gadget emitting ultrasound.", "Prize takılan ultrason cihazı.",
                    new Color(0.92f, 0.92f, 0.94f), w =>
                    {
                        w.category = ItemCategory.Repellent; w.behaviour = ItemBehaviour.Placeable; w.modelPath = "Models/ultrasonic";
                        w.repelStrength = 0.0f; w.repelRadius = 1.0f; w.purchaseCost = 15f; w.runningCostPerHour = 0.001f; w.expectedVerdict = Verdict.Myth;
                        w.evidenceEn = "Many studies, incl. Kansas State and the FTC: no measurable effect on flies or mosquitoes."; w.evidenceTr = "Kansas State ve FTC dahil birçok çalışma: sinek/sivrisinekte ölçülebilir etki yok.";
                    }),
                Make("fan", "Table fan", "Vantilatör", "🌬️", "Steady wind across the table.", "Masanın üstünde sabit rüzgar.",
                    new Color(0.9f, 0.9f, 0.9f), w =>
                    {
                        w.category = ItemCategory.Repellent; w.behaviour = ItemBehaviour.Placeable; w.modelPath = "Models/fan"; w.modelScale = 0.9f; w.windPush = 4f;
                        w.repelStrength = 0.72f; w.repelRadius = 0.8f; w.purchaseCost = 20f; w.runningCostPerHour = 0.01f; w.expectedVerdict = Verdict.Works;
                        w.evidenceEn = "Flies avoid landing in wind above ~1 m/s; fans are a documented, cheap deterrent in kitchens and picnics."; w.evidenceTr = "Sinekler ~1 m/s üstü rüzgara konmaz; vantilatör mutfak ve pikniklerde belgelenmiş ucuz bir caydırıcı.";
                    }),
                Make("vinegartrap", "Vinegar + soap trap", "Sirke + deterjan tuzağı", "🫙", "Apple cider vinegar lures, the soap drowns.", "Elma sirkesi çeker, deterjan boğar.",
                    new Color(0.75f, 0.55f, 0.2f), w =>
                    {
                        w.category = ItemCategory.Repellent; w.behaviour = ItemBehaviour.Placeable; w.modelPath = "Models/vinegar_trap";
                        w.attractStrength = 0.3f; w.attractRadius = 0.8f; w.killsOnContact = true; w.contactRadius = 0.05f; w.purchaseCost = 0.3f; w.expectedVerdict = Verdict.Kills;
                        w.evidenceEn = "Works well on fruit flies, modestly on house flies (they prefer protein baits)."; w.evidenceTr = "Meyve sineğinde iyi, karasinekte orta (onlar protein yemi sever).";
                    }),
                Make("uvzapper", "UV bug zapper", "Mor ışık (UV) sinek öldürücü", "💜", "UV light attracts, the grid zaps.", "UV ışık çeker, ızgara çarpar.",
                    new Color(0.7f, 0.4f, 1f), w =>
                    {
                        w.category = ItemCategory.Repellent; w.behaviour = ItemBehaviour.Placeable; w.modelPath = "Models/uv_zapper"; w.lightColor = new Color(0.6f, 0.3f, 1f);
                        w.attractStrength = 0.45f; w.attractRadius = 1.5f; w.killsOnContact = true; w.contactRadius = 0.07f; w.purchaseCost = 25f; w.runningCostPerHour = 0.003f; w.expectedVerdict = Verdict.Kills;
                        w.evidenceEn = "House flies are strongly attracted to UV-A; commercial light traps are standard in food industry. (Zappers also spray fly bits - use a glue board.)"; w.evidenceTr = "Karasinek UV-A'ya güçlü çekilir; ışık tuzakları gıda sektöründe standart. (Çarpan modeller parçacık saçar - yapışkan levhalı tercih et.)";
                    }),
            };
        }
    }
}
