# War For Oil — Minigame Sistemi

## Genel Bakis

Oyuncu, petrol kaynaklari zengin ulkeleri secip hukumetine baski yaparak savas baslatir. Savas surecinde gelen eventlere yanit vererek halk destegini yonetir. Savas sonunda olasilik tabanli bir kontrol yapilir — kazanilirsa ulkenin kaynaklari ele gecirilir, kaybedilirse agir cezalar uygulanir ve **minigame kalici olarak devre disi kalir**.

---

## Mimari

Sistem 4 ScriptableObject + 1 Manager + 2 yardimci siniftan olusur:

```
WarForOilDatabase (SO)          — tum ayarlar ve ulke havuzu
  └── WarForOilCountry (SO)     — tek bir ulke verisi
        └── WarForOilEvent (SO) — savas sirasi event
              └── WarForOilEventChoice (Serializable) — event secenegi

WarForOilManager (MonoBehaviour, Singleton) — ana mantik
WarForOilResult (Serializable)              — savas sonucu verisi
WarForOilState (enum)                       — durum makinesi
```

Asset olusturma: `Assets → Create → Minigames → WarForOil → Database / Country / Event`

---

## Durum Makinesi

```
Idle ──→ PressurePhase ──→ WarProcess ←──→ EventPhase
              │                                 │
              ↓                                 │
         (CancelPressure)                       │
              │                                 │
              ↓                                 ↓
            Idle                          WarProcess
                                              │
                                              ↓
                                         ResultPhase ──→ Idle
```

| Durum | Aciklama |
|-------|----------|
| **Idle** | Minigame bosta. Ulke rotasyonu devam eder. |
| **CountrySelection** | (Rezerve) Ilerde kullanilabilir. Su an Idle'dan dogrudan PressurePhase'e gecilir. |
| **PressurePhase** | Ulke secildi. Oyuncu "Baski Yap" butonuyla siyasi nufuza dayali basari kontrolu yapar. Basarisizsa 20 sn cooldown. |
| **WarProcess** | Savas baslamis. Timer ilerler, belirli araliklarda eventler tetiklenir. |
| **EventPhase** | Savas sirasinda event geldi. Oyun duraklatilir, oyuncu karar verir veya sure dolar. |
| **ResultPhase** | Savas bitti, sonuc ekrani gosteriliyor. Oyun duraklatilmis. UI ekrani kapatinca stat'lar uygulanir. |

---

## Veri Sinifları

### WarForOilDatabase

Tum minigame ayarlarinin tek noktadan yonetildigi ScriptableObject.

| Alan | Varsayilan | Aciklama |
|------|-----------|----------|
| `countries` | — | Tum ulke havuzu |
| `visibleCountryCount` | 3 | UI'da ayni anda goruntulenen ulke sayisi |
| `rotationInterval` | 90 sn | Ulke degisim araligi |
| `pressureCooldown` | 20 sn | Basarisiz baski sonrasi bekleme |
| `politicalInfluenceMultiplier` | 0.01 | Siyasi nufuzun basari sansina carpani |
| `warDuration` | 300 sn | Savas suresi (5 dakika) |
| `eventInterval` | 15 sn | Event kontrol araligi |
| `initialSupportStat` | 50 | Destek stat baslangic degeri |
| `baseWinChance` | 0.375 | Temel kazanma sansi |
| `supportWinBonus` | 0.625 | Tam destegin kazanma sansina max katkisi |
| `minWinChance` | 0.1 | Minimum kazanma sansi (%10) |
| `maxWinChance` | 0.9 | Maximum kazanma sansi (%90) |
| `baseWarReward` | 500 | Kazanma odulu (base) |
| `warLossPenalty` | 200 | Kaybetme para cezasi |
| `warLossPoliticalPenalty` | 20 | Kaybetme siyasi nufuz dususu |
| `warLossSuspicionIncrease` | 15 | Kaybetme suphe artisi |

### WarForOilCountry

Her ulke icin ayri bir ScriptableObject asset'i olusturulur.

| Alan | Aciklama |
|------|----------|
| `id` | Benzersiz kimlik |
| `displayName` | UI'da gorunecek ulke adi |
| `description` | Ulke aciklamasi (TextArea) |
| `resourceRichness` | 0-1 arasi, kaynak zenginligi → kazanc carpani |
| `invasionDifficulty` | 0-1 arasi, isgal zorlugu → kazanma sansini dusurur |
| `events` | Bu ulkeye ozel savas eventlerinin listesi |

### WarForOilEvent

Savas sirasinda tetiklenen karar olaylari.

| Alan | Aciklama |
|------|----------|
| `id` | Benzersiz kimlik |
| `displayName` | Event basligi |
| `description` | Event aciklamasi (TextArea) |
| `decisionTime` | Karar suresi (varsayilan 10 sn) |
| `choices` | Secenek listesi |
| `defaultChoiceIndex` | Sure dolunca secilecek secenek (-1 = ilk secenek) |

### WarForOilEventChoice

Event icindeki tek bir secenek. Serializable sinif.

| Alan | Aciklama |
|------|----------|
| `displayName` | Secenek adi |
| `description` | Secenek aciklamasi (TextArea) |
| `supportModifier` | Destek stat degisimi (+ = destek artar) |
| `suspicionModifier` | Suphe degisimi |
| `costModifier` | Maliyet degisimi (int) |

### WarForOilResult

Savas sonucu. Manager tarafindan olusturulur, event'lerle UI'a iletilir.

| Alan | Aciklama |
|------|----------|
| `country` | Savas yapilan ulke |
| `warWon` | Kazanildi mi |
| `finalSupportStat` | Savas sonu destek degeri |
| `winChance` | Hesaplanan kazanma sansi |
| `wealthChange` | Para degisimi (+ kazanc, - kayip) |
| `suspicionChange` | Suphe degisimi |
| `politicalInfluenceChange` | Siyasi nufuz degisimi |

---

## Formuller

### Baski Basari Sansi

```
successChance = clamp(politicalInfluence * politicalInfluenceMultiplier, 0, 0.95)
```

- Siyasi nufuz 0 veya negatifse → %0 sans
- Siyasi nufuz 95+ (carpan 0.01 ile) → %95 sans (tavan)
- **Ornek:** Nufuz 50 → %50, Nufuz 80 → %80

### Savas Kazanma Sansi

```
winChance = clamp(baseWinChance - invasionDifficulty + (supportStat / 100) * supportWinBonus, minWinChance, maxWinChance)
```

Varsayilan degerlerle:
```
winChance = clamp(0.375 - invasionDifficulty + (supportStat / 100) * 0.625, 0.1, 0.9)
```

| Senaryo | invasionDifficulty | supportStat | Hesaplama | Sonuc |
|---------|--------------------|-------------|-----------|-------|
| Kolay ulke, tam destek | 0.1 | 100 | 0.375 - 0.1 + 1.0 * 0.625 = 0.9 | **%90** |
| Zor ulke, dusuk destek | 0.4 | 20 | 0.375 - 0.4 + 0.2 * 0.625 = 0.1 | **%10** |
| Orta ulke, orta destek | 0.25 | 50 | 0.375 - 0.25 + 0.5 * 0.625 = 0.4375 | **%43.75** |

### Kazanma Odulu

```
reward = baseWarReward * resourceRichness * (supportStat / 100) - accumulatedCostModifier
```

- `resourceRichness` ne kadar zenginse o kadar kazanc
- `supportStat` ne kadar yuksekse o kadar verimli isgal
- Event'lerdeki `costModifier`'lar toplam kazanctan dusulur

### Kaybetme Cezasi

```
wealthChange = -(warLossPenalty + accumulatedCostModifier)
suspicionChange = warLossSuspicionIncrease + accumulatedSuspicionModifier
politicalInfluenceChange = -warLossPoliticalPenalty
```

- Savas kaybedilirse **minigame kalici olarak kapanir** (bir daha oynamaz)

---

## Ulke Rotasyonu

UI'da ayni anda `visibleCountryCount` (varsayilan 3) ulke gosterilir. Her `rotationInterval` (varsayilan 90 sn) saniyede bir tanesi degistirilir.

**Kurallar:**
- Iste giren her ulke en az 1 rotasyon suresi boyunca korunur (swap edilemez)
- Secili ulke (aktif savas/baski) swap edilemez
- Isgal edilmis (conquered) ulkeler havuzdan cikarilir
- Havuzda yeterli ulke yoksa swap yapilmaz
- UI acip kapatmak listeyi degistirmez — rotasyon state'ten bagimsiz calisir

**Akis:**
1. Minigame unlock olunca `InitializeCountryRotation()` havuzdan rastgele ulkeleri secer
2. `UpdateCountryRotation()` her frame calisir, timer dolunca swap uygunluk kontrolu yapar
3. Swap yapilinca `OnActiveCountriesChanged` event'i UI'i bilgilendirir

---

## Manager API

### UI'in Cagirdigi Metodlar

| Metod | Parametre | Ne Yapar |
|-------|-----------|----------|
| `SelectCountry(country)` | WarForOilCountry | Ulke secip PressurePhase'e gecer. Unlock, cooldown, conquered kontrolleri yapar. |
| `AttemptPressure()` | — | Baski denemesi. Basarili → savas baslar. Basarisiz → cooldown. |
| `CancelPressure()` | — | Baskidan vazgecip Idle'a doner. |
| `ResolveEvent(choiceIndex)` | int | Event secimi yapar, modifier'lari uygular, savasa geri doner. |
| `DismissResultScreen()` | — | Sonuc ekranini kapatir, stat'lari uygular, cooldown baslatir. |

### Events (UI Dinleyecek)

| Event | Parametre | Ne Zaman |
|-------|-----------|----------|
| `OnCountrySelected` | WarForOilCountry | Ulke secildi |
| `OnPressureResult` | bool, float | Baski sonucu (basari, cooldown suresi) |
| `OnPressureCooldownUpdate` | float | Cooldown geri sayimi (her frame) |
| `OnWarStarted` | WarForOilCountry, float | Savas baslamis (ulke, sure) |
| `OnWarProgress` | float | Savas ilerlemesi (0-1) |
| `OnWarEventTriggered` | WarForOilEvent | Event tetiklendi |
| `OnEventDecisionTimerUpdate` | float | Event karar sayaci |
| `OnWarEventResolved` | WarForOilEventChoice | Seçim yapildi |
| `OnWarResultReady` | WarForOilResult | Sonuc hazir, ekran goster |
| `OnWarFinished` | WarForOilResult | Sonuc ekrani kapandi, her sey bitti |
| `OnActiveCountriesChanged` | List\<WarForOilCountry\> | Ulke listesi degisti |

### Getter'lar

| Metod | Donus | Aciklama |
|-------|-------|----------|
| `IsActive()` | bool | Minigame aktif mi (Idle degilse true) |
| `IsPermanentlyDisabled()` | bool | Kalici devre disi mi |
| `IsCountryConquered(country)` | bool | Ulke isgal edilmis mi |
| `GetCurrentState()` | WarForOilState | Mevcut durum |
| `GetSelectedCountry()` | WarForOilCountry | Secili ulke |
| `GetSupportStat()` | float | Destek degeri |
| `GetActiveCountries()` | List\<WarForOilCountry\> | UI'daki ulke listesi |
| `GetWarProgress()` | float | Savas ilerlemesi (0-1) |

---

## Oyun Duraklama Davranisi

| Durum | Oyun Durumu | Timer Tipi |
|-------|-------------|------------|
| WarProcess | **Devam ediyor** | `Time.deltaTime` |
| EventPhase | **Duraklatilmis** | `Time.unscaledDeltaTime` |
| ResultPhase | **Duraklatilmis** | Timer yok (UI bekleniyor) |
| PressurePhase | **Devam ediyor** | `Time.deltaTime` |

---

## EventCoordinator Entegrasyonu

Savas sırasında event tetiklemeden once `EventCoordinator.CanShowEvent()` kontrol edilir. Bu, RandomEventManager ve PleasePaperManager gibi diger sistemlerle event cakismasini onler.

Event tetiklendiginde `EventCoordinator.MarkEventShown()` cagirilir → diger sistemler kisa bir cooldown suresince event gonderemez.

---

## Tipik Oyun Akisi

1. **Rotasyon calisir** — UI'da 3 ulke gosterilir
2. **Oyuncu ulke secer** → `SelectCountry()` → PressurePhase
3. **Oyuncu baski yapar** → `AttemptPressure()`
   - Basarisiz → 20 sn cooldown, tekrar dene
   - Basarili → savas baslar
4. **Savas sureci** — 5 dakika, her 15 sn'de event kontrolu
5. **Event gelir** → oyun durur, oyuncu secer → `ResolveEvent()`
   - supportStat ve modifier'lar guncellenir
6. **Savas biter** → olasilik kontrolu → kazanma/kaybetme
7. **Sonuc ekrani** → oyuncu kapatir → `DismissResultScreen()`
   - Kazanildiysa: odul, ulke conquered
   - Kaybedildiyse: ceza, **minigame kalici kapanir**

---

## Dosya Yapisi

```
Assets/Scripts/Minigames/WarForOil/
├── WarForOilCountry.cs    — ulke verisi (ScriptableObject)
├── WarForOilEvent.cs      — event + choice verisi (ScriptableObject + Serializable)
├── WarForOilDatabase.cs   — ayarlar + havuz (ScriptableObject)
├── WarForOilManager.cs    — ana mantik + state machine + rotasyon (MonoBehaviour)
└── warforoil-readme.md    — bu dosya
```
