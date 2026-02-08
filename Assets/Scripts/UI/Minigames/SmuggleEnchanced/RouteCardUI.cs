using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class RouteCardUI : MonoBehaviour
{
    [Header("UI Components")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI riskText;
    public TextMeshProUGUI costText;
    public Image background;

    [Header("Colors")]
    public Color normalColor = Color.white;
    public Color selectedColor = Color.green;

    private SmuggleRoute route;
    private bool isSelected = false;

    public SmuggleRoute Route => route;

    public void Initialize(SmuggleRoute route)
    {
        this.route = route;
        
        if (nameText != null)
            nameText.text = route.displayName;
        
        if (riskText != null)
            riskText.text = $"Risk: {route.riskLevel:F0}%";
        
        if (costText != null)
            costText.text = $"Cost: ${route.cost}";

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
            SmuggleSelectionUI.Instance.SelectRoute(route);
        }
    }
}