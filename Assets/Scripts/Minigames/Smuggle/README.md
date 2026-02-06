# Smuggle Minigame

Madenler skill dalina bagli kacakcilik operasyonu minigame'i.

## Akis

1. Oyuncu ilgili skill'i acar → `UnlockMinigameEffect` tetiklenir → `MinigameManager.UnlockMinigame(SmuggleMiniGame)` cagirilir
2. Oyuncu minigame'i baslatir → `SmuggleManager.TryStartMinigame()` (acik mi, cooldown'da mi, aktif mi kontrol edilir)
3. Rastgele bir rota paketi (4 rota) ve 3 kurye sunulur → `OnSelectionPhaseStarted`
4. Oyuncu rota secer → `SelectRoute(route)`
5. Oyuncu kurye secer → `SelectCourier(courier)` → operasyon baslar

## Operasyon Sureci

- Operasyon suresi: `route.distance / (courier.speed * 0.1)` saniye
- Her `eventCheckInterval` (varsayilan 5s) saniyede bir event tetiklenme kontrolu yapilir
- Tetiklenme sansi: `route.riskLevel` (0-100 arasi, yuzde olarak)
- Event tetiklenirse operasyon duraklar, oyuncuya secenekler sunulur (`eventDecisionTime` = 10s)
- Sure dolarsa otomatik ilk secenek secilir
- Secimler basari sansi, suphe ve maliyet modifier'lari biriktirir
- Ayni event bir operasyonda iki kez tetiklenmez

## Sonuc Hesaplama

- Basari sansi: `courier.reliability - (route.riskLevel * 0.5) + birikimModifier`
- Her zaman %5-%95 arasi clamp edilir
- Basariliysa: `baseReward - routeCost - courierCost - eventCosts`
- Basarisizsa: `-(routeCost + courierCost + eventCosts)`
- Suphe her durumda artar (basarisizlikta daha fazla)
- Sonrasinda 5 dk cooldown baslar (MiniGameData.cooldownDuration)

## Dosyalar

| Dosya | Tur | Aciklama |
|-------|-----|----------|
| SmuggleManager.cs | MonoBehaviour | Ana minigame yoneticisi, state machine, operasyon zamanlayici |
| SmuggleDatabase.cs | ScriptableObject | Rota paketleri, kuryeler ve event havuzu |
| SmuggleRoutePack.cs | ScriptableObject | 4 rotayi bir araya getiren paket |
| SmuggleRoute.cs | ScriptableObject | Tekil rota verisi (risk, mesafe, maliyet, odul) |
| SmuggleCourier.cs | ScriptableObject | Tekil kurye verisi (guvenilirlik, hiz, maliyet, ihanet, gizlilik) |
| SmuggleEvent.cs | ScriptableObject | Operasyon sirasi event (aciklama + secenekler) |

## State Machine

```
Idle → SelectingRoute → SelectingCourier → InProgress ⇄ EventPhase → Idle
                                                                ↓
                                                         CalculateResult
```

## C# Events (UI icin)

| Event | Parametre | Ne Zaman |
|-------|-----------|----------|
| OnSelectionPhaseStarted | RoutePack, List<Courier> | Rota/kurye secimi basladi |
| OnOperationStarted | Route, Courier, float(sure) | Operasyon basladi |
| OnOperationProgress | float(0-1) | Her frame ilerleme |
| OnSmuggleEventTriggered | SmuggleEvent | Event tetiklendi |
| OnEventDecisionTimerUpdate | float(kalan sure) | Event karar sayaci |
| OnSmuggleEventResolved | SmuggleEventChoice | Oyuncu secim yapti |
| OnOperationCompleted | SmuggleResult | Operasyon bitti |
| OnSmuggleFailed | string(mesaj) | Baslatma basarisiz |

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
