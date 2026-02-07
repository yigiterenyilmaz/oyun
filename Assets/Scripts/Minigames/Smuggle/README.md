# Smuggle Minigame

Madenler skill dalina bagli kacakcilik operasyonu minigame'i.

---

## State Machine

```
                     CancelSelection()
                    ┌──────────────────── Idle
                    │                      │
Idle ──→ SelectingRoute ──→ SelectingCourier ──→ InProgress ←──→ EventPhase
                                                     │               │
                                                     │          causesFailure
                                                     │          failureDelay
                                                     │               │
                                                     ├── FailOperation() ──→ Idle
                                                     │
                                                     ├── CancelOperation() ──→ Idle
                                                     │
                                                     ▼
                                                CalculateResult (basari) ──→ Idle
```

| Durum | Aciklama |
|-------|----------|
| Idle | Beklemede, yeni operasyon baslatilabilir |
| SelectingRoute | Oyuncu rota seciyor (4 rota sunulur) |
| SelectingCourier | Oyuncu kurye seciyor (3 kurye sunulur) + butce kontrolu |
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
    ├── CancelSelection() → Idle'a don (para odenmedi, iade yok)
    │
    ├── SelectRoute(route)
    │
    ├── SelectCourier(courier) → butce kontrolu (route.cost + courier.cost)
    │       │
    │       ├── Para yeterli → maliyet pesin dusulur → StartOperation()
    │       │
    │       └── Para yetersiz → OnSmuggleFailed tetiklenir
    │
    ▼
StartOperation() → sure hesaplanir, zamanlayicilar baslar
    │
    ├── Her 5s → TryTriggerEvent()
    │       │
    │       ├── Event tetiklendi → EventPhase (10s karar suresi)
    │       │       │
    │       │       ├── causesFailure → aninda FailOperation()
    │       │       │
    │       │       ├── failureDelay > 0 → X saniye sonra FailOperation()
    │       │       │
    │       │       ├── Normal secim → modifier birikir, havuz guncellenir
    │       │       │
    │       │       └── Sure doldu → ilk secenek otomatik secilir
    │       │
    │       └── Event tetiklenmedi → devam
    │
    ├── pendingFailureTimer doldu → FailOperation()
    │
    ├── CancelOperation() → kismi iade, Idle'a don
    │
    ▼
Kurye hedefe ulasti → CalculateResult() → her zaman basari
    │
    ├── Kazanc ve suphe uygulanir
    └── Cooldown baslar
```

---

## Basari / Basarisizlik

Basari tamamen eventlere baglidir. Zar atilmaz.

- **Basari:** Kurye yolun sonuna ulastiysa → her zaman basari
- **Aninda basarisizlik:** Event secenegi `causesFailure = true` → operasyon aninda biter
- **Gecikmeli basarisizlik:** Event secenegi `failureDelay > 0` → X saniye sonra operasyon biter
  - Gecikme, kalan operasyon suresine clamp edilir (her zaman varistan en az 1 saniye once patlar)
  - EventPhase sirasinda gecikme sayaci duraklar

---

## Butce Kontrolu

Maliyet `SelectCourier` asamasinda pesin odenir:

1. `totalCost = route.cost + courier.cost`
2. `GameStatManager.HasEnoughWealth(totalCost)` kontrolu yapilir
3. Para yeterliyse → `GameStatManager.AddWealth(-totalCost)` ile dusulur
4. Para yetersizse → `OnSmuggleFailed("Not enough funds. Required: X")` tetiklenir

Basarili operasyonda kazanc: `routePack.baseReward - eventCosts`
Basarisiz operasyonda kayip: sadece event kayiplari (maliyet zaten odendi)

---

## Operasyon Iptali

### Secim Asamasi

`SelectingRoute` veya `SelectingCourier` sirasinda `CancelSelection()` ile iptal edilebilir. Henuz para odenmemistir, iade yoktur.

### Operasyon Sirasinda

Oyuncu `InProgress` veya `EventPhase` sirasinda `CancelOperation()` ile iptal edebilir.

**Iade formulu:** `totalCost * (kalanYuzde / 2)`

| Ilerleme | Kalan | Iade Orani | Ornek (maliyet 200) |
|----------|-------|------------|---------------------|
| %0 | %100 | %50 | 100 |
| %20 | %80 | %40 | 80 |
| %50 | %50 | %25 | 50 |
| %80 | %20 | %10 | 20 |
| %100 | %0 | %0 | 0 |

---

## Event Tetiklenme Sistemi

Her 5 saniyede bir aktif havuzdan rastgele bir event adayi secilir. Adayin `triggerType`'ina gore tetiklenme sansi hesaplanir:

| Trigger | Kaynak | Formul | Ornek |
|---------|--------|--------|-------|
| Risk | Rota | `riskLevel * (1 - reliability/200)` | risk 80, rel 90 → %44 |
| Betrayal | Kurye | `betrayalChance * 100` | chance 0.3 → %30 |
| Incompetence | Kurye | `100 - reliability` | rel 80 → %20 |

- Ayni event bir operasyonda iki kez tetiklenmez
- Yuksek reliability, Risk eventlerinin tetiklenme sansini da dusurur
- Oyuncu seciminde `nextEventPool` varsa aktif havuz degisir (event zinciri)
- `nextEventPool` bos veya null ise zincir biter, artik event tetiklenmez

---

## Event Secenekleri

Her event secenegi su etkilere sahip olabilir:

| Alan | Tip | Islev |
|------|-----|-------|
| `suspicionModifier` | float | Supheye etki (+ veya -) |
| `costModifier` | int | Ekstra maliyet (rusvet, kayip vs.) |
| `causesFailure` | bool | true ise operasyon aninda basarisiz olur |
| `failureDelay` | float | 0'dan buyukse X saniye sonra operasyon basarisiz olur |
| `nextEventPool` | List | Sonraki event havuzu (bossa zincir biter) |

`causesFailure` ve `failureDelay` ayni anda kullanilmaz. `causesFailure` once kontrol edilir.

---

## Sonuc Hesaplama

Maliyet (route.cost + courier.cost) operasyon basinda pesin odenir.

| Durum | Ne Zaman | Wealth Degisimi | Suspicion Degisimi |
|-------|----------|-----------------|-------------------|
| Basari | Kurye hedefe ulasti | `+routePack.baseReward - eventCosts` | `riskLevel * 0.1 + eventSuspicion` |
| Basarisizlik | Event sonucu (aninda/gecikmeli) | `-eventCosts` | `riskLevel * 0.3 + eventSuspicion` |

- Sonrasinda cooldown baslar (MiniGameData.cooldownDuration)

---

## Stat Rolleri

| Stat | Yer | Islev |
|------|-----|-------|
| `riskLevel` | Rota | Risk eventlerini tetikler |
| `distance` | Rota | Operasyon suresini belirler |
| `cost` | Rota | Rotayi kullanma maliyeti (pesin odenir) |
| `baseReward` | Rota Paketi | Basarili operasyonun kazanci |
| `reliability` | Kurye | Incompetence eventlerini tetikler + Risk eventlerini azaltir |
| `speed` | Kurye | Operasyon suresini kisaltir |
| `cost` | Kurye | Kurye kiralama maliyeti (pesin odenir) |
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
| `OnOperationCompleted` | SmuggleResult | Operasyon bitti (basari veya basarisizlik) |
| `OnOperationCancelled` | float(iade miktari) | Operasyon iptal edildi |
| `OnSmuggleFailed` | string(mesaj) | Baslatma basarisiz |

---

## Metodlar

| Metod | Islev |
|-------|-------|
| `TryStartMinigame()` | Minigame'i baslatmayi dener (acik mi, cooldown, aktif mi) |
| `SelectRoute(route)` | Oyuncu rota secti |
| `SelectCourier(courier)` | Oyuncu kurye secti, butce kontrolu + pesin odeme, operasyon baslar |
| `ResolveEvent(choiceIndex)` | Oyuncu event secimi yapti |
| `CancelSelection()` | Secim asamasini iptal eder (iade yok) |
| `CancelOperation()` | Operasyonu iptal eder, kismi iade yapar |
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
| SmuggleEvent.cs | ScriptableObject | Operasyon sirasi event (triggerType, secenekler, failure, event zinciri) |

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
