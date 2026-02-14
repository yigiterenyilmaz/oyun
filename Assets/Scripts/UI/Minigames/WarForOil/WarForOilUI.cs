using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class WarForOilUI : MonoBehaviour
{
    [Header("Panels")]
    public GameObject selectionPanel;
    public GameObject pressurePanel;
    public GameObject warPanel;
    public GameObject eventPanel;
    public GameObject resultPanel;

    [Header("Selection Panel")]
    public Transform countryContainer;
    public GameObject countryCardPrefab;

    [Header("Pressure Panel")]
    public TextMeshProUGUI pressureCountryName;
    public TextMeshProUGUI pressureCooldownText;
    public Button pressureButton;
    public Button cancelButton;

    [Header("War Panel")]
    public TextMeshProUGUI warCountryName;
    public TextMeshProUGUI warTimerText;
    public TextMeshProUGUI supportText;
    public Slider progressBar;
    public Button ceasefireButton;

    [Header("Event Panel")]
    public TextMeshProUGUI eventTitle;
    public TextMeshProUGUI eventDescription;
    public TextMeshProUGUI eventTimerText;
    public Transform choiceContainer;
    public GameObject choiceButtonPrefab;

    [Header("Result Panel")]
    public TextMeshProUGUI resultTitle;
    public TextMeshProUGUI resultDescription;
    public TextMeshProUGUI resultStats;
    public Button dismissButton;

    // Runtime
    private List<GameObject> spawnedCards = new List<GameObject>();
    private List<GameObject> spawnedChoices = new List<GameObject>();
    private WarForOilManager manager;
    private float warDuration;
    private float eventDuration;
    private float pressureCooldown;
    private bool initialized = false;
    public static WarForOilUI Instance { get; private set; }

    void Awake()
    {
        Instance = this;
        gameObject.SetActive(false);
    }

    void SetupButtons()
    {
        pressureButton.onClick.AddListener(() => manager.AttemptPressure());
        cancelButton.onClick.AddListener(Close);
        ceasefireButton.onClick.AddListener(() => manager.RequestCeasefire());
        dismissButton.onClick.AddListener(() => manager.DismissResultScreen());
    }

    void Close()
    {
        selectionPanel.SetActive(false);
        pressurePanel.SetActive(false);
        warPanel.SetActive(false);
        eventPanel.SetActive(false);
        resultPanel.SetActive(false);
        gameObject.SetActive(false);

        UImanager.Instance.ToggleUI();
    }
    
    void OnCancelClicked()
    {
        manager.CancelPressure();
        CloseWarForOil();
    }
    
    public static void OpenWarForOil()
    {
        if (Instance == null)
        {
            Debug.LogError("[WarForOilUI] Instance not found!");
            return;
        }

        // Set manager BEFORE anything else
        Instance.manager = WarForOilManager.Instance;
    
        // Setup if first time
        if (!Instance.initialized)
        {
            Instance.SubscribeEvents();
            Instance.SetupButtons();
            Instance.initialized = true;
        }

        Instance.gameObject.SetActive(true);

        // Show panel based on current state
        var state = Instance.manager.GetCurrentState();
        switch (state)
        {
            case WarForOilState.PressurePhase:
                Instance.ShowPanel(Instance.pressurePanel);
                var country = Instance.manager.GetSelectedCountry();
                if (country != null)
                {
                    Instance.pressureCountryName.text = country.displayName;
                }
                break;
            
            case WarForOilState.WarProcess:
            case WarForOilState.EventPhase:
                Instance.ShowPanel(Instance.warPanel);
                break;
            
            case WarForOilState.ResultPhase:
                Instance.ShowPanel(Instance.resultPanel);
                break;
            
            default:
                Instance.ShowPanel(Instance.selectionPanel);
                Instance.TryRefreshCountries();
                break;
        }

        UImanager.Instance.ToggleUI();
    }
    
    public static void CloseWarForOil()
    {
        if (Instance == null) return;
    
        Instance.selectionPanel.SetActive(false);
        Instance.pressurePanel.SetActive(false);
        Instance.warPanel.SetActive(false);
        Instance.eventPanel.SetActive(false);
        Instance.resultPanel.SetActive(false);
        Instance.gameObject.SetActive(false);

        UImanager.Instance.ToggleUI();
    }

    void Update()
    {
        // Try to refresh country list if empty
        if (initialized && selectionPanel.activeSelf && spawnedCards.Count == 0)
        {
            TryRefreshCountries();
        }
    }

    void TryRefreshCountries()
    {
        if (manager == null)
        {
            Debug.LogError("[WarForOilUI] Manager is null!");
            return;
        }

        if (countryCardPrefab == null)
        {
            Debug.LogError("[WarForOilUI] countryCardPrefab is not assigned!");
            return;
        }

        if (countryContainer == null)
        {
            Debug.LogError("[WarForOilUI] countryContainer is not assigned!");
            return;
        }

        var countries = manager.GetActiveCountries();
    
        Debug.Log($"[WarForOilUI] TryRefreshCountries: {countries?.Count ?? 0} countries");
    
        if (countries == null)
        {
            Debug.LogError("[WarForOilUI] GetActiveCountries returned null!");
            return;
        }

        if (countries.Count == 0)
        {
            Debug.LogWarning("[WarForOilUI] GetActiveCountries returned empty list!");
        
            if (manager.database != null)
            {
                Debug.Log($"[WarForOilUI] Database has {manager.database.countries?.Count ?? 0} countries");
            }
            return;
        }

        RefreshCountryCards(countries);
    }

    void OnEnable()
    {
        if (initialized && manager != null)
        {
            TryRefreshCountries();
        }
    }

    void OnDestroy() => UnsubscribeEvents();

    void SubscribeEvents()
    {
        WarForOilManager.OnActiveCountriesChanged += OnCountriesChanged;
        WarForOilManager.OnCountrySelected += OnCountrySelected;
        WarForOilManager.OnPressureResult += OnPressureResult;
        WarForOilManager.OnPressureCooldownUpdate += OnCooldownUpdate;
        WarForOilManager.OnWarStarted += OnWarStarted;
        WarForOilManager.OnWarProgress += OnWarProgress;
        WarForOilManager.OnWarEventTriggered += OnEventTriggered;
        WarForOilManager.OnEventDecisionTimerUpdate += OnEventTimerUpdate;
        WarForOilManager.OnWarEventResolved += OnEventResolved;
        WarForOilManager.OnCeasefireResult += OnResult;
        WarForOilManager.OnWarResultReady += OnResult;
        WarForOilManager.OnWarFinished += OnWarFinished;
    }

    void UnsubscribeEvents()
    {
        WarForOilManager.OnActiveCountriesChanged -= OnCountriesChanged;
        WarForOilManager.OnCountrySelected -= OnCountrySelected;
        WarForOilManager.OnPressureResult -= OnPressureResult;
        WarForOilManager.OnPressureCooldownUpdate -= OnCooldownUpdate;
        WarForOilManager.OnWarStarted -= OnWarStarted;
        WarForOilManager.OnWarProgress -= OnWarProgress;
        WarForOilManager.OnWarEventTriggered -= OnEventTriggered;
        WarForOilManager.OnEventDecisionTimerUpdate -= OnEventTimerUpdate;
        WarForOilManager.OnWarEventResolved -= OnEventResolved;
        WarForOilManager.OnCeasefireResult -= OnResult;
        WarForOilManager.OnWarResultReady -= OnResult;
        WarForOilManager.OnWarFinished -= OnWarFinished;
    }

    void ShowPanel(GameObject panel)
    {
        selectionPanel.SetActive(panel == selectionPanel);
        pressurePanel.SetActive(panel == pressurePanel);
        warPanel.SetActive(panel == warPanel);
        resultPanel.SetActive(panel == resultPanel);

        if (panel == selectionPanel)
        {
            TryRefreshCountries();
        }
    }

    void RefreshCountryCards(List<WarForOilCountry> countries)
    {
        foreach (var go in spawnedCards)
        {
            if (go != null) Destroy(go);
        }
        spawnedCards.Clear();

        if (countries == null || countries.Count == 0)
        {
            Debug.LogWarning("[WarForOilUI] No countries to display");
            return;
        }

        Debug.Log($"[WarForOilUI] Refreshing {countries.Count} country cards");

        foreach (var country in countries)
        {
            if (country == null) continue;

            var card = Instantiate(countryCardPrefab, countryContainer);
            spawnedCards.Add(card);

            var txt = card.GetComponentInChildren<TextMeshProUGUI>();
            var btn = card.GetComponent<Button>();

            if (txt == null)
            {
                Debug.LogError("[WarForOilUI] CountryCardPrefab missing TextMeshProUGUI!");
                continue;
            }

            bool conquered = manager.IsCountryConquered(country);
            txt.text = conquered
                ? $"<s>{country.displayName}</s>\n<size=70%>CONQUERED</size>"
                : $"{country.displayName}\n<size=70%>Resources: {country.resourceRichness:P0} | Difficulty: {country.invasionDifficulty:P0}</size>";

            if (btn != null)
            {
                btn.interactable = !conquered;
                var countryCopy = country;
                btn.onClick.AddListener(() => 
                {
                    Debug.Log($"[WarForOilUI] CLICK FIRED: {countryCopy.displayName}");
                    manager.SelectCountry(countryCopy);
                });
            }
        }
    }

    // ==================== EVENT HANDLERS ====================

    void OnCountriesChanged(List<WarForOilCountry> countries)
    {
        Debug.Log($"[WarForOilUI] OnCountriesChanged: {countries?.Count ?? 0} countries");
        RefreshCountryCards(countries);
    }

    void OnCountrySelected(WarForOilCountry country)
    {
        Debug.Log($"[WarForOilUI] OnCountrySelected: {country?.displayName}");
        CloseWarForOil(); // Close menu when country selected
    }

    void OnPressureResult(bool success, float cooldown)
    {
        Debug.Log($"[WarForOilUI] OnPressureResult: success={success}, cooldown={cooldown}");
        if (!success)
        {
            pressureCooldown = cooldown;
            pressureButton.interactable = false;
        }
    }

    void OnCooldownUpdate(float remaining)
    {
        pressureCooldownText.text = remaining > 0 ? $"Cooldown: {remaining:F1}s" : "";
        if (remaining <= 0) pressureButton.interactable = true;
    }

    void OnWarStarted(WarForOilCountry country, float duration)
    {
        Debug.Log($"[WarForOilUI] OnWarStarted: {country?.displayName}, duration={duration}");
        ShowPanel(warPanel);
        warDuration = duration;
        warCountryName.text = country.displayName;
        progressBar.value = 0;
        supportText.text = $"Support: {manager.GetSupportStat():F0}%";
    }

    void OnWarProgress(float progress)
    {
        progressBar.value = progress;
        float remaining = warDuration * (1f - progress);
        warTimerText.text = $"{Mathf.FloorToInt(remaining / 60)}:{Mathf.FloorToInt(remaining % 60):D2}";
        supportText.text = $"Support: {manager.GetSupportStat():F0}%";
        ceasefireButton.interactable = manager.CanRequestCeasefire();
    }

    void OnEventTriggered(WarForOilEvent evt)
    {
        Debug.Log($"[WarForOilUI] OnEventTriggered: {evt?.displayName}");
        eventPanel.SetActive(true);
        eventTitle.text = evt.displayName;
        eventDescription.text = evt.description;
        eventDuration = evt.decisionTime;

        foreach (var go in spawnedChoices)
        {
            if (go != null) Destroy(go);
        }
        spawnedChoices.Clear();

        for (int i = 0; i < evt.choices.Count; i++)
        {
            int idx = i;
            var choice = evt.choices[i];
            var btn = Instantiate(choiceButtonPrefab, choiceContainer);
            spawnedChoices.Add(btn);

            var txt = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null)
            {
                txt.text = $"{choice.displayName}\n<size=70%>{FormatModifiers(choice)}</size>";
            }

            var button = btn.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(() => manager.ResolveEvent(idx));
            }
        }
    }

    void OnEventTimerUpdate(float remaining)
    {
        eventTimerText.text = $"{remaining:F1}s";
    }

    void OnEventResolved(WarForOilEventChoice choice)
    {
        Debug.Log($"[WarForOilUI] OnEventResolved: {choice?.displayName}");
        eventPanel.SetActive(false);
    }

    void OnResult(WarForOilResult result)
    {
        Debug.Log($"[WarForOilUI] OnResult: won={result.warWon}, ceasefire={result.wasCeasefire}");
        ShowPanel(resultPanel);

        if (result.wasCeasefire)
            resultTitle.text = "Ceasefire";
        else if (result.warWon)
            resultTitle.text = "Victory!";
        else
            resultTitle.text = "Defeat";

        resultDescription.text = result.wasCeasefire
            ? $"Negotiated ceasefire with {result.country.displayName}."
            : result.warWon
                ? $"Conquered {result.country.displayName}!"
                : $"Failed to conquer {result.country.displayName}. War operations disabled.";

        resultStats.text = $"Wealth: {(result.wealthChange >= 0 ? "+" : "")}{result.wealthChange:F0}\n" +
                           $"Suspicion: {(result.suspicionChange >= 0 ? "+" : "")}{result.suspicionChange:F0}";
    }

    void OnWarFinished(WarForOilResult result)
    {
        Debug.Log($"[WarForOilUI] OnWarFinished: disabled={manager.IsPermanentlyDisabled()}");
        CloseWarForOil();
    }

    string FormatModifiers(WarForOilEventChoice c)
    {
        var parts = new List<string>();
        if (c.supportModifier != 0) parts.Add($"Support {(c.supportModifier > 0 ? "+" : "")}{c.supportModifier}");
        if (c.suspicionModifier != 0) parts.Add($"Suspicion {(c.suspicionModifier > 0 ? "+" : "")}{c.suspicionModifier}");
        if (c.costModifier != 0) parts.Add($"Cost {(c.costModifier > 0 ? "+" : "")}{c.costModifier}");
        return string.Join(" | ", parts);
    }
}