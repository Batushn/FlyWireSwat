using System.Collections.Generic;
using UnityEngine;

namespace FlyWireSwat.Sim
{
    /// <summary>Tiny two-language string table. English is the default; Turkish via the menu (saved in PlayerPrefs).</summary>
    public static class L10n
    {
        public static string Lang { get; private set; } = "en";
        public static bool IsTr => Lang == "tr";
        const string PrefKey = "flywireswat.lang";

        static L10n()
        {
            try { Lang = PlayerPrefs.GetString(PrefKey, "en"); } catch { Lang = "en"; }
        }

        public static void SetLang(string lang)
        {
            Lang = lang == "tr" ? "tr" : "en";
            try { PlayerPrefs.SetString(PrefKey, Lang); PlayerPrefs.Save(); } catch { }
        }

        public static void Toggle() => SetLang(IsTr ? "en" : "tr");

        public static string Pick(string en, string tr) => IsTr ? tr : en;

        public static string T(string key) => Table.TryGetValue(key, out var v) ? (IsTr ? v.tr : v.en) : key;

        static readonly Dictionary<string, (string en, string tr)> Table = new Dictionary<string, (string, string)>
        {
            ["title"] = ("FlyWireSwat", "FlyWireSwat"),
            ["tagline"] = ("A real fly brain (FlyWire connectome, 1,517 neurons) vs. every way to kill a fly.\nWhich method is actually optimal?", "Gerçek sinek beyni (FlyWire connectome, 1.517 nöron) vs. sinek öldürmenin her yolu.\nGerçekten en optimize yöntem hangisi?"),
            ["menu.showcase"] = ("1  Benchmark + slow-motion showcase", "1  Benchmark + ağır çekim showcase"),
            ["menu.fps"] = ("2  FPS mode - kill the fly yourself", "2  FPS modu - sineği kendin öldür"),
            ["menu.record"] = ("3  Record videos of every weapon", "3  Tüm silahların videosunu kaydet"),
            ["menu.lang"] = ("Language: English  (press T)", "Dil: Türkçe  (T tuşu)"),
            ["loading"] = ("Loading the FlyWire v783 escape circuit...", "FlyWire v783 kaçış devresi yükleniyor..."),
            ["loaded"] = ("{0} neurons, {1} connections, {2} synapses loaded", "{0} nöron, {1} bağlantı, {2} sinaps yüklendi"),
            ["bench.title"] = ("FlyWireSwat - connectome-driven fly-swatting benchmark", "FlyWireSwat - connectome tabanlı sinek öldürme benchmark'ı"),
            ["bench.running"] = ("Benchmark: {0}  ({1}/{2}) x {3} trials  [{4} threads]", "Benchmark: {0}  ({1}/{2}) x {3} deneme  [{4} iş parçacığı]"),
            ["bench.repel"] = ("Repellent test: {0}", "Kovuculuk testi: {0}"),
            ["bench.done"] = ("Benchmark done in {0:0.0} s. Results: {1}", "Benchmark bitti ({0:0.0} sn). Sonuç: {1}"),
            ["bench.cached"] = ("Cached benchmark results loaded (F5 = re-run)", "Önbellekteki benchmark sonuçları yüklendi (F5 = yeniden koştur)"),
            ["lb.title"] = ("LEADERBOARD  -  lethality", "LEADERBOARD  -  öldürücülük"),
            ["lb.sub"] = ("{0} trials per weapon  -  score = kill% x kill% / (time, cost, damage)", "silah başına {0} deneme  -  skor = öldürme% x öldürme% / (süre, maliyet, hasar)"),
            ["lb.h.weapon"] = ("weapon", "silah"),
            ["lb.h.score"] = ("score", "skor"),
            ["lb.h.kill"] = ("kill%", "öl%"),
            ["lb.h.first"] = ("1st try", "ilk%"),
            ["lb.h.time"] = ("time", "süre"),
            ["lb.h.cost"] = ("$/kill", "$/av"),
            ["lb.h.dmg"] = ("damage$", "hasar$"),
            ["lb.h.gf"] = ("GF escape", "GF kaçış"),
            ["rb.title"] = ("REPELLENT TEST  -  does it keep flies away?", "KOVUCULUK TESTİ  -  sineği uzak tutuyor mu?"),
            ["rb.sub"] = ("{0} simulated landings over {1} h  -  protection = fewer landings within 50 cm", "{1} saatte {0} simüle konma  -  koruma = 50 cm içine daha az konma"),
            ["rb.lures"] = ("lures +{0:0}%", "çeker +{0:0}%"),
            ["rb.h.item"] = ("item", "yöntem"),
            ["rb.h.prot"] = ("protection", "koruma"),
            ["rb.h.kills"] = ("kills/h", "öl/saat"),
            ["rb.h.cost"] = ("cost$", "maliyet$"),
            ["rb.h.verdict"] = ("verdict", "hüküm"),
            ["verdict.myth"] = ("MYTH", "ŞEHİR EFSANESİ"),
            ["verdict.weak"] = ("weak", "zayıf"),
            ["verdict.works"] = ("WORKS", "İŞE YARIYOR"),
            ["verdict.kills"] = ("KILLS", "ÖLDÜRÜYOR"),
            ["hud.time"] = ("t = {0:0.0} ms   (x{1:0.###} speed)", "t = {0:0.0} ms   (x{1:0.###} hız)"),
            ["hud.approach"] = ("approach: {0:0} deg azimuth, l/v = {1:0.0} ms, impact at {2:0} ms", "yaklaşma: {0:0} derece azimut, l/v = {1:0.0} ms, çarpma {2:0} ms"),
            ["hud.gf"] = ("GF (DNp01) spike: {0}", "GF (DNp01) spike: {0}"),
            ["hud.gf.at"] = ("{0:0.0} ms  ({1:0.0} ms before impact)", "{0:0.0} ms  (çarpmadan {1:0.0} ms önce)"),
            ["hud.gf.none"] = ("did not fire", "ateşlemedi"),
            ["hud.takeoff"] = ("take-off: {0}   mode: {1}", "kalkış: {0}   mod: {1}"),
            ["mode.short"] = ("GF short-mode escape", "GF kısa-mod kaçış"),
            ["mode.long"] = ("long-mode (coordinated) escape", "uzun-mod (koordineli) kaçış"),
            ["mode.none"] = ("no escape", "kaçış yok"),
            ["hud.dist"] = ("distance at impact: {0:0.0} cm  (lethal radius {1:0.0} cm)", "çarpmada uzaklık: {0:0.0} cm  (öldürücü yarıçap {1:0.0} cm)"),
            ["hud.result"] = ("result: {0}   p(kill) = {1:P0}", "sonuç: {0}   p(öl) = {1:P0}"),
            ["res.dead"] = ("DEAD", "ÖLDÜ"),
            ["res.hit"] = ("hit but survived", "vuruldu ama kurtuldu"),
            ["res.escaped"] = ("ESCAPED", "KAÇTI"),
            ["res.waiting"] = ("waiting...", "bekliyor..."),
            ["gf.flash"] = ("GIANT FIBER SPIKE", "GIANT FIBER SPIKE"),
            ["splat"] = ("SPLAT.", "SPLAT."),
            ["miss"] = ("MISSED!", "ISKALADI!"),
            ["raster"] = ("spike rate (1 ms bins) - from stimulus onset", "spike oranı (1 ms binler) - uyaran başlangıcından"),
            ["brain.pip"] = ("escape circuit - 1,517 FlyWire neurons (LC4/LPLC2 -> GF -> DNp)", "kaçış devresi - 1.517 FlyWire nöronu (LC4/LPLC2 -> GF -> DNp)"),
            ["help.showcase"] = ("Space/Right next  Left prev  1-9 pick  R new trial  Enter replay  P pause  +/- speed  L tables  B brain  V record  F FPS  T language  F5 benchmark  Esc menu",
                                 "Space/Sağ sonraki  Sol önceki  1-9 seç  R yeni deneme  Enter tekrar  P duraklat  +/- hız  L tablolar  B beyin  V kayıt  F FPS  T dil  F5 benchmark  Esc menü"),
            ["rec"] = ("REC  {0}", "KAYIT  {0}"),
            ["rec.status"] = ("Recording: {0} ({1}/{2})  frame {3}/{4}", "Kayıt: {0} ({1}/{2})  kare {3}/{4}"),
            ["rec.done"] = ("Videos ready: {0}", "Videolar hazır: {0}"),
            ["fps.help"] = ("WASD move | Shift run | LMB attack/place | 1-9/0, Q/E, wheel: item | Esc exit | Tab cursor", "WASD yürü | Shift koş | Sol tık saldır/yerleştir | 1-9/0, Q/E, tekerlek: eşya | Esc çıkış | Tab imleç"),
            ["fps.kills"] = ("kills: {0}   attempts: {1}   time: {2:0}s", "öldürme: {0}   deneme: {1}   süre: {2:0}s"),
            ["fps.cost"] = ("cost: ${0:0.00}   damage: ${1:0}", "maliyet: ${0:0.00}   hasar: ${1:0}"),
            ["fps.fly"] = ("fly: {0}", "sinek: {0}"),
            ["fps.angle"] = ("visual angle {0:0.0} deg   expansion {1:0} deg/s", "görsel açı {0:0.0} derece   genişleme {1:0} derece/s"),
            ["fly.resting"] = ("resting", "dinleniyor"),
            ["fly.escaping"] = ("ESCAPING", "KAÇIYOR"),
            ["fly.flying"] = ("flying", "uçuyor"),
            ["fly.landing"] = ("landing", "konuyor"),
            ["fly.poisoned"] = ("poisoned", "zehirlendi"),
            ["fly.dead"] = ("dead - a new one is coming", "öldü - yenisi geliyor"),
            ["fly.stuck"] = ("stuck", "yapıştı"),
            ["msg.miss"] = ("missed", "ıskaladın"),
            ["msg.whiff"] = ("swung at nothing", "boşa salladın"),
            ["msg.escaped"] = ("{0} - the fly escaped ({1})", "{0} - sinek kaçtı ({1})"),
            ["msg.kill"] = ("SPLAT!  kills: {0}", "SPLAT!  öldürme: {0}"),
            ["msg.hole"] = ("missed (hole in the wall)", "ıskaladın (duvarda delik)"),
            ["msg.house"] = ("SPLAT! (and the house is gone)", "SPLAT! (ve ev gitti)"),
            ["msg.rocketmiss"] = ("...how did you miss that?", "...bunu nasıl ıskaladın?"),
            ["msg.spray"] = ("the fly got sprayed: ~8 s (really ~45 s)", "sinek ilacı yedi: ~8 sn (gerçekte ~45 sn)"),
            ["msg.spraymiss"] = ("the cloud missed the fly", "bulut sineği yakalamadı"),
            ["msg.placed"] = ("placed: {0}", "yerleştirildi: {0}"),
            ["msg.placeaim"] = ("aim at a surface to place it", "yerleştirmek için bir yüzeye nişan al"),
            ["msg.trap"] = ("sticky trap set, wait...", "yapışkan tuzak kuruldu, bekle..."),
            ["msg.trapkill"] = ("the sticky trap worked!", "yapışkan tuzak çalıştı!"),
            ["msg.zap"] = ("ZAP! the UV lamp got it", "ZAP! mor lamba yakaladı"),
            ["msg.vacuum"] = ("the vacuum swallowed it!", "süpürge yuttu!"),
            ["msg.cat"] = ("the cat got it!", "kedi yakaladı!"),
            ["msg.catmiss"] = ("the cat missed (a vase broke)", "kedi ıskaladı (vazo kırıldı)"),
        };
    }
}
