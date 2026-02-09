# Please Paper Minigame

Ülkelerin yurt dışında saklanan altınlarını ekonomik krizleri kullanarak ele geçirme minigame'i. Oyuncuya belirli aralıklarla teklif gelir, oyuncu krizin gerçek mi sahte mi olduğunu değerlendirip karar verir. Kendi UI paneli yok — mevcut event panelini kullanır.

---

## Durum Makinesi

```
                          RejectOffer()
                         ┌──────────────── Idle
                         │                  │
Idle ──→ OfferPending ──┤
                         │
                         ├── Sahte kriz → FakeCrisisProcess ←──→ EventPhase
                         │                      │
                         │                      ▼
                         │               CompleteFakeCrisis ──→ Idle (zarar)
                         │
                         └── Gerçek kriz → ActiveProcess ←──→ EventPhase
                                               │
                                               ├── controlStat = 0 → FailProcess ──→ Idle
                                               │
                                               ├── Süre doldu, controlStat < 50 → FailProcess ──→ Idle
                                               │
                                               └── Süre doldu, controlStat >= 50 → BargainingPhase ──→ Idle
```

| Durum | Açıklama |
|-------|----------|
| Idle | Teklif bekleniyor (90-150s arasında rastgele) |
| OfferPending | Teklif geldi, oyuncu kabul/ret bekliyor (15s) |
| FakeCrisisProcess | Sahte kriz — event zinciri sırayla gösterilir, sonuç hep zarar |
| ActiveProcess | Gerçek kriz — 5 dakikalık süreç, controlStat yönetimi |
| EventPhase | Event geldi, oyuncu karar bekliyor (10s) |
| BargainingPhase | Süreç başarılı bitti, kazanç controlStat'a göre hesaplanır |

---

## Akış

```
Skill açılır
    │
    ▼
UnlockMinigameEffect → MinigameManager.UnlockMinigame()
    │
    ▼
Idle — offerTimer geri sayıyor (90-150s)
    │
    ▼
GenerateOffer() → EventCoordinator slot kontrolü
    │
    ├── Slot doluysa → bu cycle atlanır, yeni interval başlar
    │
    ├── Slot boşsa → slot alınır, offerEvents havuzundan rastgele teklif seçilir
    │
    ▼
OfferPending — oyuncuya teklif event'inin bilgisi sunulur (15s)
    │
    ├── RejectOffer() → slot bırakılır, Idle'a dön
    │
    ├── AcceptOffer() → isFakeCrisis kontrolü
    │       │
    │       ├── Sahte kriz → StartFakeCrisisProcess()
    │       │       │
    │       │       ├── Offer'ın fakeCrisisEvents zinciri sırayla gösterilir
    │       │       │
    │       │       └── Zincir bitti → CompleteFakeCrisis() → zarar uygulanır, Idle
    │       │
    │       └── Gerçek kriz → StartActiveProcess()
    │               │
    │               ├── controlStat = 40, processTimer başlar (300s)
    │               │
    │               ├── Her 15s → TryTriggerProcessEvent()
    │               │       │
    │               │       ├── Event tetiklendi → EventPhase (10s karar süresi)
    │               │       │       │
    │               │       │       ├── controlStatModifier uygulanır
    │               │       │       │
    │               │       │       ├── controlStat = 0 → FailProcess()
    │               │       │       │
    │               │       │       └── Normal → ActiveProcess'e dön
    │               │       │
    │               │       └── Event tetiklenmedi → devam
    │               │
    │               ▼
    │          Süre doldu → CalculateResult()
    │               │
    │               ├── controlStat < 50 → FailProcess()
    │               │
    │               └── controlStat >= 50 → StartBargaining()
    │
    └── Süre doldu → otomatik ret
```

---

## Event Grupları

Tüm eventler tek `PleasePaperEvent` sınıfından oluşturulur. `eventType` alanına göre Inspector'da sadece ilgili alanlar gösterilir.

| Grup | eventType | Açıklama |
|------|-----------|----------|
| Teklif eventleri | Offer | Ülke bilgisi, baseReward, isFakeCrisis taşır. Database'de `offerEvents` listesinde |
| Sahte kriz eventleri | FakeCrisis | Choices ile zarar verir. Offer event'inin `fakeCrisisEvents` listesinde |
| Süreç eventleri | Process | Choices ile controlStat etkiler. Database'de `processEvents` listesinde |

Inspector'da `eventType` seçilince:
- **Offer** → baseReward, isFakeCrisis, fakeCrisisEvents alanları görünür
- **FakeCrisis / Process** → choices listesi, karar süresi, default seçenek görünür

---

## Asset Oluşturma Rehberi

Tüm asset'ler `Assets/GameData/Minigames/PleasePaper/` klasöründe oluşturulur.
Sağ tık → Create → Minigames → PleasePaper → Event veya Database.

### Yapı

```
PleasePaperDatabase
  ├─ offerEvents (Offer tipinde event'ler)
  │    ├─ Venezuela Krizi (isFakeCrisis = false) → gerçek kriz
  │    └─ Kıbrıs Tuzağı (isFakeCrisis = true)
  │         └─ fakeCrisisEvents: [Adım1, Adım2, Adım3]  ← FakeCrisis tipinde event'ler
  │
  └─ processEvents (Process tipinde event'ler)
       ├─ İşçi Grevi
       ├─ Medya Sızıntısı
       └─ Nakliye Pususu
```

### Oluşturma Sırası

1. **Process event'leri** oluştur (eventType = Process, seçenekler + modifier'lar doldur)
2. **FakeCrisis event'leri** oluştur (eventType = FakeCrisis, zincirin her adımı ayrı asset)
3. **Offer event'leri** oluştur (eventType = Offer):
   - Gerçek kriz: isFakeCrisis = false, baseReward doldur
   - Sahte kriz: isFakeCrisis = true, fakeCrisisEvents listesine FakeCrisis event'lerini sırayla sürükle
4. **Database** oluştur → offerEvents ve processEvents listelerine ilgili event'leri sürükle

FakeCrisis event'leri database'e eklenmez — sahte Offer event'inin içindeki `fakeCrisisEvents` listesine bağlanır.

---

## controlStat Mekaniği

Gerçek kriz sürecinin temel stat'ı. 40'tan başlar, event seçimlerine göre artar veya azalır. 0-100 arasında clamp edilir.

| Durum | Sonuç |
|-------|-------|
| controlStat = 0 | Anında başarısız (süreç bitmeden) |
| Süreç sonu, controlStat < 50 | Başarısız |
| Süreç sonu, controlStat >= 50 | Başarılı → pazarlık aşaması |

Oyuncunun amacı 5 dakika boyunca controlStat'ı 50'nin üzerinde tutmak. Eventlere verilen seçimler controlStat'ı artırabilir veya düşürebilir.

---

## Pazarlık

Süreç başarılı bittiğinde oyuncunun ne kadar kazanacağı controlStat'a göre belirlenir:

- `bargainingPower = (controlStat - 50) / 50` → 0.0 ile 1.0 arası
- Nihai kazanç: `baseReward * (0.5 + bargainingPower * 0.5)`

| controlStat | bargainingPower | Kazanç Oranı |
|-------------|-----------------|--------------|
| 50 | 0.0 | %50 |
| 75 | 0.5 | %75 |
| 100 | 1.0 | %100 |

controlStat tam sınırda (50) olsa bile oyuncu başarılı sayılır ama kazancı minimum düzeydedir.

Pazarlık veya game over ekranı gösterildiğinde oyun duraklatılır (`GameManager.PauseGame()`). Oyuncu ekranı kapatana kadar hiçbir sistem çalışmaz. UI `DismissResultScreen()` çağırdığında stat'lar uygulanır ve oyun devam eder.

---

## Sahte Kriz

Bazı teklif event'lerinin `isFakeCrisis = true` alanı vardır. Oyuncu bu teklifi kabul ederse gerçek bir süreç başlamaz — bunun yerine teklif event'inin `fakeCrisisEvents` listesindeki eventler sırayla gösterilir. Her event oyuncuya zarar verir (wealth kaybı, suspicion artışı). Zincir bitince sonuç hesaplanır — oyuncu her durumda zararlı çıkar.

Oyuncunun sahte krizi gerçek krizden ayırt edecek bilgiye sahip olması beklenir (event açıklaması, ipuçları vb.).

---

## Anti-Overlap (EventCoordinator)

Please Paper event'leri ve RandomEventManager aynı anda event gösteremez. `EventCoordinator` static sınıfı paylaşımlı bir slot yönetir:

- Teklif gelmeden önce slot kontrol edilir → doluysa teklif ertelenir
- RandomEventManager event tetiklemeden önce slot kontrol eder → doluysa bu cycle atlanır
- Event/teklif çözüldüğünde slot serbest bırakılır

Bu sayede iki sistem birbirini kesintiye uğratmaz.

---

## Bütçe ve Sonuç

Please Paper'da peşin ödeme yoktur. Kazanç veya zarar tamamen süreç sonucuna göre hesaplanır.

| Durum | Wealth Değişimi | Suspicion Değişimi |
|-------|-----------------|-------------------|
| Gerçek kriz başarı | `+baseReward * kazançOranı - eventCosts` | `+eventSuspicion` |
| Gerçek kriz başarısızlık | `-eventCosts` | `+eventSuspicion` |
| Sahte kriz | `-eventCosts` | `+eventSuspicion` |

Sonrasında cooldown başlar (MiniGameData.cooldownDuration).

---

## Event Sistemi

Gerçek kriz sürecinde her 15 saniyede bir aktif havuzdan rastgele bir event seçilir. Aynı event bir süreçte iki kez tetiklenmez. Oyuncu seçiminde `nextEventPool` varsa aktif havuz değişir (event zinciri). `nextEventPool` boş veya null ise zincir biter.

Her event seçeneği controlStat değişimi (`controlStatModifier`), şüphe (`suspicionModifier`), maliyet (`costModifier`) ve sonraki event havuzu (`nextEventPool`) içerebilir.

Sahte krizde ise eventler zincir halinde sıralı gösterilir — havuzdan rastgele seçim yapılmaz.

---

## Events

| Event | Ne Zaman |
|-------|----------|
| `OnOfferReceived` | Teklif geldi (Offer event) |
| `OnOfferDecisionTimerUpdate` | Teklif karar sayacı güncellendi |
| `OnProcessStarted` | Süreç başladı (offer, süre) |
| `OnProcessProgress` | Her frame ilerleme (0-1) |
| `OnControlStatChanged` | controlStat değişti |
| `OnPleasePaperEventTriggered` | Event tetiklendi |
| `OnEventDecisionTimerUpdate` | Event karar sayacı güncellendi |
| `OnPleasePaperEventResolved` | Oyuncu event seçimi yaptı |
| `OnBargainingStarted` | Pazarlık başladı, oyun duraklatıldı (bargainingPower). UI `DismissResultScreen()` çağırmalı |
| `OnGameOver` | Süreç başarısız bitti, oyun duraklatıldı (sebep mesajı). UI `DismissResultScreen()` çağırmalı |
| `OnProcessCompleted` | Sonuç ekranı kapatıldı, stat'lar uygulandı (başarı, başarısızlık veya sahte kriz) |
| `OnPleasePaperFailed` | Minigame başlatılamadı |

---

## Metodlar

| Metod | İşlev |
|-------|-------|
| `AcceptOffer()` | Teklifi kabul et |
| `RejectOffer()` | Teklifi reddet |
| `ResolveEvent(choiceIndex)` | Event seçimi yap |
| `DismissResultScreen()` | Pazarlık/game over ekranını kapat, stat uygula, oyunu devam ettir |
| `IsActive()` | Minigame aktif mi |
| `GetCurrentState()` | Mevcut state |
| `GetControlStat()` | controlStat değeri |
| `GetProcessProgress()` | Süreç ilerleme oranı (0-1) |
