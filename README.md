# FlyWireSwat

Unity 6 (6000.6.0f1, URP) simülasyonu: FlyWire açık kaynak *Drosophila* connectome'u ile
sürülen bir sinek, "en optimize sinek öldürme yöntemi"ni bulmak için silah arsenaline karşı
(el, gazete, raket, ... füze) binlerce denemede test edilir.

## Yapı
- `Assets/Scripts/Connectome` – FlyWire nöron/sinaps verisi yükleyici + Burst spiking ağ
- `Assets/Scripts/Fly` – sinek uçuş fiziği, duyu girişi (görsel loom) → ağ → motor çıkışı
- `Assets/Scripts/Weapons` – silah tanımları (ScriptableObject), commondan komiğe
- `Assets/Scripts/Sim` – deneme koşturucu, istatistik, leaderboard
- `Assets/Data` – işlenmiş küçük connectome alt-grafı (git-lfs)
- `Data/raw` – ham FlyWire indirmeleri (git dışı)

## Veri
FlyWire v783 connectome: https://codex.flywire.ai/api/download (nöron tablosu + sinaps tablosu).
Kaçış devresi için ilgi alanı: LC4 / LPLC2 → Giant Fiber (GF) → DLM/TTM motor nöronları.

## Araçlar
- `unity open ~/Projects/FlyWireSwat`
- Claude Code MCP: `unity-editor-mcp` (user scope, bu projeye pinli)
