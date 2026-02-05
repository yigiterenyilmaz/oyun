using UnityEngine;
using UnityEngine.UI;

public class SkillNodeUI : MonoBehaviour
{
    public Image iconImage;
    public GameObject lockOverlay;
    public Button button;

    private System.Action onClick;

    public void Setup(Sprite icon, bool isUnlocked, System.Action onClickCallback)
    {
        
        iconImage.sprite = icon;
        lockOverlay.SetActive(!isUnlocked);
        button.interactable = isUnlocked;
        onClick = onClickCallback;
    }

    public void OnNodeClick()
    {
        onClick?.Invoke();
        Debug.Log("OnNodeClick");
    }
}