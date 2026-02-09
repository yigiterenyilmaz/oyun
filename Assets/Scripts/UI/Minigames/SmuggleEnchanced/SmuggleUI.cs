using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class SmuggleSelectionUI : MonoBehaviour
{
    public static SmuggleSelectionUI Instance { get; private set; }

    [Header("Main Panel")]
    public GameObject selectionPanel;

    [Header("Containers")]
    public Transform routesContainer;
    public Transform couriersContainer;

    [Header("Prefabs")]
    public GameObject routeCardPrefab;
    public GameObject courierCardPrefab;

    [Header("UI Text")]
    public TextMeshProUGUI baseRewardText;
    public TextMeshProUGUI totalCostText;

    [Header("Buttons")]
    public Button confirmButton;
    public Button cancelButton;

    // Current data
    private SmuggleRoutePack currentRoutePack;
    private List<SmuggleCourier> currentCouriers = new List<SmuggleCourier>();

    // Current selections
    private SmuggleRoute selectedRoute;
    private SmuggleCourier selectedCourier;

    // Card references
    private List<RouteCardUI> routeCards = new List<RouteCardUI>();
    private List<CourierCardUI> courierCards = new List<CourierCardUI>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // Setup buttons
        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(OnConfirmClicked);
            confirmButton.interactable = false;
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(OnCancelClicked);
        }

        // Subscribe to SmuggleManager events
        SmuggleManager.OnSelectionPhaseStarted += OnSelectionPhaseStarted;

        // Hide panel initially
        if (selectionPanel != null)
            selectionPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        SmuggleManager.OnSelectionPhaseStarted -= OnSelectionPhaseStarted;
    }

    private void OnSelectionPhaseStarted(SmuggleRoutePack routePack, List<SmuggleCourier> couriers)
    {
        currentRoutePack = routePack;
        currentCouriers = couriers;
        selectedRoute = null;
        selectedCourier = null;

        // Clear old cards
        ClearCards();

        // Create route cards
        if (routePack != null && routePack.routes != null)
        {
            Debug.Log($"SmuggleUI: {routePack.routes.Count} rota kartı oluşturuluyor (routePack: {routePack.name})");
            foreach (SmuggleRoute route in routePack.routes)
            {
                CreateRouteCard(route);
            }
        }

        // Create courier cards
        if (couriers != null)
        {
            Debug.Log($"SmuggleUI: {couriers.Count} kurye kartı oluşturuluyor");
            foreach (SmuggleCourier courier in couriers)
            {
                CreateCourierCard(courier);
            }
        }

        // Update UI
        if (baseRewardText != null)
            baseRewardText.text = $"Base Reward: ${routePack.baseReward}";

        UpdateTotalCost();

        // Show panel
        if (selectionPanel != null)
            selectionPanel.SetActive(true);
    }

    private void CreateRouteCard(SmuggleRoute route)
    {
        if (routeCardPrefab == null || routesContainer == null) return;

        GameObject cardObj = Instantiate(routeCardPrefab, routesContainer);

        //prefab'da script yoksa runtime'da ekle
        RouteCardUI card = cardObj.GetComponent<RouteCardUI>();
        if (card == null)
        {
            card = cardObj.AddComponent<RouteCardUI>();
        }

        card.Initialize(route);
        routeCards.Add(card);

        //Button bileşeni yoksa ekle
        Button btn = cardObj.GetComponent<Button>();
        if (btn == null)
            btn = cardObj.AddComponent<Button>();

        //tıklama için raycast hedefi lazım
        EnsureButtonHasTargetGraphic(btn, cardObj);

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => card.OnCardClicked());
    }

    private void CreateCourierCard(SmuggleCourier courier)
    {
        if (courierCardPrefab == null || couriersContainer == null) return;

        GameObject cardObj = Instantiate(courierCardPrefab, couriersContainer);

        //prefab'da script yoksa runtime'da ekle
        CourierCardUI card = cardObj.GetComponent<CourierCardUI>();
        if (card == null)
        {
            card = cardObj.AddComponent<CourierCardUI>();
        }

        card.Initialize(courier);
        courierCards.Add(card);

        //Button bileşeni yoksa ekle
        Button btn = cardObj.GetComponent<Button>();
        if (btn == null)
            btn = cardObj.AddComponent<Button>();

        //tıklama için raycast hedefi lazım
        EnsureButtonHasTargetGraphic(btn, cardObj);

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => card.OnCardClicked());
    }

    public void SelectRoute(SmuggleRoute route)
    {
        selectedRoute = route;

        // Update visual selection
        foreach (RouteCardUI card in routeCards)
        {
            card.SetSelected(card.Route == route);
        }

        UpdateTotalCost();
        UpdateConfirmButton();
    }

    public void SelectCourier(SmuggleCourier courier)
    {
        selectedCourier = courier;

        // Update visual selection
        foreach (CourierCardUI card in courierCards)
        {
            card.SetSelected(card.Courier == courier);
        }

        UpdateTotalCost();
        UpdateConfirmButton();
    }

    private void UpdateTotalCost()
    {
        int totalCost = 0;

        if (selectedRoute != null)
            totalCost += selectedRoute.cost;

        if (selectedCourier != null)
            totalCost += selectedCourier.cost;

        if (totalCostText != null)
            totalCostText.text = $"Total Cost: ${totalCost}";
    }

    private void UpdateConfirmButton()
    {
        if (confirmButton != null)
            confirmButton.interactable = (selectedRoute != null && selectedCourier != null);
    }

    private void OnConfirmClicked()
    {
        if (selectedRoute == null || selectedCourier == null) return;

        // Tell SmuggleManager about selections
        if (SmuggleManager.Instance != null)
        {
            SmuggleManager.Instance.SelectRoute(selectedRoute);
            SmuggleManager.Instance.SelectCourier(selectedCourier);
        }

        // Hide panel
        if (selectionPanel != null)
            selectionPanel.SetActive(false);
    }

    private void OnCancelClicked()
    {
        if (SmuggleManager.Instance != null)
        {
            SmuggleManager.Instance.CancelSelection();
        }

        if (selectionPanel != null)
            selectionPanel.SetActive(false);
    }

    private void ClearCards()
    {
        // Destroy route cards
        foreach (RouteCardUI card in routeCards)
        {
            if (card != null)
                Destroy(card.gameObject);
        }
        routeCards.Clear();

        // Destroy courier cards
        foreach (CourierCardUI card in courierCards)
        {
            if (card != null)
                Destroy(card.gameObject);
        }
        courierCards.Clear();
    }

    /// <summary>
    /// Button'un tıklanabilmesi için targetGraphic ve raycastTarget ayarlar.
    /// </summary>
    private void EnsureButtonHasTargetGraphic(Button btn, GameObject cardObj)
    {
        Image img = cardObj.GetComponent<Image>();

        if (img == null)
            img = cardObj.GetComponentInChildren<Image>();

        if (img == null)
        {
            img = cardObj.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);
        }

        img.raycastTarget = true;
        btn.targetGraphic = img;
    }

    public void OpenSelectionMenu()
    {
        if (SmuggleManager.Instance != null)
        {
            SmuggleManager.Instance.TryStartMinigame();
        }
    }
}
