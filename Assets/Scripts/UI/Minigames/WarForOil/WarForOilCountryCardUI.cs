using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// Ülke seçim kartı UI bileşeni
/// </summary>
public class WarForOilCountryCardUI : MonoBehaviour
{
    [Header("References")]
    public TextMeshProUGUI countryNameText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI resourcesText;
    public TextMeshProUGUI difficultyText;
    public Slider resourcesBar;
    public Slider difficultyBar;
    public Button selectButton;
    public TextMeshProUGUI selectButtonText;
    public GameObject conqueredOverlay;
    public Image cardBackground;

    [Header("Colors")]
    public Color normalColor = new Color(0.2f, 0.2f, 0.25f);
    public Color conqueredColor = new Color(0.15f, 0.15f, 0.15f);
    public Color easyColor = new Color(0.2f, 0.7f, 0.2f);
    public Color mediumColor = new Color(0.8f, 0.7f, 0.2f);
    public Color hardColor = new Color(0.8f, 0.2f, 0.2f);

    private WarForOilCountry country;
    private Action onClickCallback;

    public void Setup(WarForOilCountry country, bool isConquered, Action onClick)
    {
        this.country = country;
        this.onClickCallback = onClick;

        // Texts
        countryNameText.text = country.displayName;

        if (descriptionText != null)
            descriptionText.text = country.description;

        if (resourcesText != null)
            resourcesText.text = $"Resources: {(country.resourceRichness * 100):F0}%";

        if (difficultyText != null)
            difficultyText.text = $"Difficulty: {(country.invasionDifficulty * 100):F0}%";

        // Bars
        if (resourcesBar != null)
            resourcesBar.value = country.resourceRichness;

        if (difficultyBar != null)
        {
            difficultyBar.value = country.invasionDifficulty;

            // Color difficulty bar
            var fillImage = difficultyBar.fillRect.GetComponent<Image>();
            if (fillImage != null)
            {
                if (country.invasionDifficulty < 0.33f)
                    fillImage.color = easyColor;
                else if (country.invasionDifficulty < 0.66f)
                    fillImage.color = mediumColor;
                else
                    fillImage.color = hardColor;
            }
        }

        // Conquered state
        if (conqueredOverlay != null)
            conqueredOverlay.SetActive(isConquered);

        if (cardBackground != null)
            cardBackground.color = isConquered ? conqueredColor : normalColor;

        // Button
        selectButton.interactable = !isConquered;
        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(OnSelectClicked);

        if (selectButtonText != null)
            selectButtonText.text = isConquered ? "Conquered" : "Select";
    }

    private void OnSelectClicked()
    {
        onClickCallback?.Invoke();
    }
}