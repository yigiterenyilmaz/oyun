using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class CourierCardUI : MonoBehaviour
{
    [Header("UI Components")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI reliabilityText;
    public TextMeshProUGUI speedText;
    public TextMeshProUGUI costText;
    public Image background;

    [Header("Colors")]
    public Color normalColor = Color.white;
    public Color selectedColor = Color.green;

    private SmuggleCourier courier;
    private bool isSelected = false;

    public SmuggleCourier Courier => courier;

    public void Initialize(SmuggleCourier courier)
    {
        this.courier = courier;
        
        if (nameText != null)
            nameText.text = courier.displayName;
        
        if (reliabilityText != null)
            reliabilityText.text = $"Reliability: {courier.reliability:F0}%";
        
        if (speedText != null)
            speedText.text = $"Speed: {courier.speed:F0}%";
        
        if (costText != null)
            costText.text = $"Cost: ${courier.cost}";

        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        if (background != null)
        {
            background.color = selected ? selectedColor : normalColor;
        }
    }

    public void OnCardClicked()
    {
        if (SmuggleSelectionUI.Instance != null)
        {
            SmuggleSelectionUI.Instance.SelectCourier(courier);
        }
    }
}