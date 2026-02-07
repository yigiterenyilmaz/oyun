# Smuggle Minigame

Madenler skill dalina bagli kacakcilik operasyonu minigame'i.

---

## State Machine

```
Idle ──→ SelectingRoute ──→ SelectingCourier ──→ InProgress ←──→ EventPhase
                                                     │
                                                     ▼
                                                CalculateResult ──→ Idle
```

| Durum | Aciklama |
|-------|----------|
| Idle | Beklemede, yeni operasyon baslatilabilir |
| SelectingRoute | Oyuncu rota seciyor (4 rota sunulur) |
| SelectingCourier | Oyuncu kurye seciyor (3 kurye sunulur) |
| InProgress | Operasyon devam ediyor, kurye yolda |
| EventPhase | Event tetiklendi, oyuncu karar bekliyor (10s) |

---

## Akis

```
Skill acilir
    │
    ▼
UnlockMinigameEffect → MinigameManager.UnlockMinigame()
    │
    ▼
TryStartMinigame() → acik mi? cooldown'da mi? aktif mi?
    │
    ▼
StartSelectionPhase() → rastgele 1 rota paketi + 3 kurye secilir
    │
    ├── SelectRoute(route)
    │
    ├── SelectCourier(courier)
    │
    ▼
StartOperation() → sure hesaplanir, zamanlayicilar baslar
    │
    ├── Her 5s → TryTriggerEvent()
    │       │
    │       ├── Event tetiklendi → EventPhase (10s karar suresi)
    │       │       │
    │       │       ├── Oyuncu secim yapti → modifier birikir, havuz guncellenir
    │       │       │
    │       │       └── Sure doldu → ilk secenek otomatik secilir
    │       │
    │       └── Event tetiklenmedi → devam
    │
    ▼
Operasyon suresi doldu → CalculateResult()
    │
    ├── Basari/basarisizlik (tamamen event modifier'larina bagli)
    ├── Wealth ve suspicion degisimi uygulanir
    └── 5 dk cooldown baslar
```

---

## Event Tetiklenme Sistemi

Her 5 saniyede bir aktif havuzdan rastgele bir event adayi secilir. Adayin `triggerType`'ina gore tetiklenme sansi hesaplanir:

| Trigger | Kaynak | Formul | Ornek |
|---------|--------|--------|-------|
| Risk | Rota | `riskLevel * (1 - reliability/200)` | risk 80, rel 90 → %44 |
| Betrayal | Kurye | `betrayalChance * 100` | chance 0.3 → %30 |
| Incompetence | Kurye | `100 - reliability` | rel 80 → %20 |

- Ayni event bir operasyonda iki kez tetiklenmez
- Oyuncu seciminde `nextEventPool` varsa aktif havuz degisir (event zinciri)
- `nextEventPool` bos veya null ise zincir biter, artik event tetiklenmez

---

## Sonuc Hesaplama

- Basari sansi: `100 + accumulatedSuccessModifier` (tamamen eventlere bagli)
- Her zaman %5-%95 arasi clamp edilir
- Basariliysa: `routePack.baseReward - route.cost - courier.cost - eventCosts`
- Basarisizsa: `-(route.cost + courier.cost + eventCosts)`
- Suphe her durumda artar (basarisizlikta daha fazla)
- Sonrasinda 5 dk cooldown baslar (MiniGameData.cooldownDuration)

---

## Stat Rolleri

| Stat | Yer | Islev |
|------|-----|-------|
| `riskLevel` | Rota | Risk eventlerini tetikler |
| `distance` | Rota | Operasyon suresini belirler |
| `cost` | Rota | Rotayi kullanma maliyeti |
| `baseReward` | Rota Paketi | Basarili operasyonun kazanci |
| `reliability` | Kurye | Incompetence eventlerini tetikler + Risk eventlerini azaltir |
| `speed` | Kurye | Operasyon suresini kisaltir |
| `cost` | Kurye | Kurye kiralama maliyeti |
| `betrayalChance` | Kurye | Betrayal eventlerini tetikler |

---

## Events

| Event | Parametre | Ne Zaman |
|-------|-----------|----------|
| `OnSelectionPhaseStarted` | RoutePack, List&lt;Courier&gt; | Rota/kurye secimi basladi |
| `OnOperationStarted` | Route, Courier, float(sure) | Operasyon basladi |
| `OnOperationProgress` | float(0-1) | Her frame ilerleme |
| `OnSmuggleEventTriggered` | SmuggleEvent | Event tetiklendi |
| `OnEventDecisionTimerUpdate` | float(kalan sure) | Event karar sayaci |
| `OnSmuggleEventResolved` | SmuggleEventChoice | Oyuncu secim yapti |
| `OnOperationCompleted` | SmuggleResult | Operasyon bitti |
| `OnSmuggleFailed` | string(mesaj) | Baslatma basarisiz |

---

## Metodlar

| Metod | Islev |
|-------|-------|
| `TryStartMinigame()` | Minigame'i baslatmayi dener (acik mi, cooldown, aktif mi) |
| `SelectRoute(route)` | Oyuncu rota secti |
| `SelectCourier(courier)` | Oyuncu kurye secti, operasyon baslar |
| `ResolveEvent(choiceIndex)` | Oyuncu event secimi yapti |
| `CanPlay()` | Minigame oynanabilir mi |
| `GetOperationProgress()` | Operasyon ilerleme orani (0-1) |
| `GetCurrentState()` | Mevcut state |

---

## Dosyalar

| Dosya | Tur | Aciklama |
|-------|-----|----------|
| SmuggleManager.cs | MonoBehaviour | Ana minigame yoneticisi, state machine, operasyon zamanlayici |
| SmuggleDatabase.cs | ScriptableObject | Rota paketleri, kuryeler ve event havuzu |
| SmuggleRoutePack.cs | ScriptableObject | Rota paketi (baseReward + 4 rota) |
| SmuggleRoute.cs | ScriptableObject | Tekil rota verisi (risk, mesafe, maliyet) |
| SmuggleCourier.cs | ScriptableObject | Tekil kurye verisi (beceri, hiz, maliyet, ihanet) |
| SmuggleEvent.cs | ScriptableObject | Operasyon sirasi event (triggerType, secenekler, event zinciri) |

---

## Asset Klasor Yapisi

```
Assets/GameData/Minigames/Smuggle/
├── SmuggleMiniGame.asset      (MiniGameData)
├── SmuggleDatabase.asset      (SmuggleDatabase)
├── Routes/                    (SmuggleRoute asset'leri)
├── RoutePacks/                (SmuggleRoutePack asset'leri)
├── Couriers/                  (SmuggleCourier asset'leri)
└── Events/                    (SmuggleEvent asset'leri)
```
