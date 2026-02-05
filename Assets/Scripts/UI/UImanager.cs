using UnityEngine;
using UnityEngine.SceneManagement;

public class UImanager : MonoBehaviour
{
    public GameObject pausePanel;
    public GameObject skillTreePanel;   
    public GameObject pauseButton;
    public GameObject skillTreeButton;
    public GameObject moneyBar;
    public MapController mainCamera;
    
    
    public void OnRestartPress()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void OnGameResumePress()
    {
        pausePanel.SetActive(false);
        skillTreePanel.SetActive(false);
        pauseButton.SetActive(true);
        skillTreeButton.SetActive(true);
        moneyBar.SetActive(true);
        mainCamera.enable = true;
    }

    public void OnGameExitPress()
    {
        Application.Quit();
    }

    public void OnEnterPausePress()
    {
        pausePanel.SetActive(true);
        skillTreePanel.SetActive(false);
        pauseButton.SetActive(false);
        skillTreeButton.SetActive(false);
        moneyBar.SetActive(false);
        mainCamera.enable = false;
    }

    public void OnSkillTreeOpen()
    {
        pausePanel.SetActive(false);
        skillTreePanel.SetActive(true);
        pauseButton.SetActive(false);
        skillTreeButton.SetActive(false);
        moneyBar.SetActive(false);
        mainCamera.enable = false;
    }

    public void OnSkillTreeClose()
    {
        pausePanel.SetActive(false);
        skillTreePanel.SetActive(false);
        pauseButton.SetActive(true);
        skillTreeButton.SetActive(true);
        moneyBar.SetActive(true);
        mainCamera.enable = true;
        
    }
}