using UnityEngine;
using UnityEngine.UI;

public class PauseMenuController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Buttons")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button exitToMainMenuButton;
    [SerializeField] private Button settingsBackButton;

    [Header("Main Menu Scene Name")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    private bool wasInteractWindowOpenLastFrame;

    private void Awake()
    {
        continueButton.onClick.AddListener(OnContinue);
        settingsButton.onClick.AddListener(OnSettings);
        exitToMainMenuButton.onClick.AddListener(OnExitToMainMenu);

        if (settingsBackButton != null)
            settingsBackButton.onClick.AddListener(OnSettingsBack);

        pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    private void Update()
    {
        bool isInteractWindowOpen = UI_StateManager.Instance != null && UI_StateManager.Instance.interactWindowOpen;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // If ESC was used to close interaction UI this frame (or the interaction was open last frame),
            // don't also toggle pause in the same key press.
            if (isInteractWindowOpen || wasInteractWindowOpenLastFrame)
            {
                wasInteractWindowOpenLastFrame = isInteractWindowOpen;
                return;
            }

            if (NPCDialoguePanel.LastEscapeCloseFrame == Time.frameCount)
            {
                wasInteractWindowOpenLastFrame = isInteractWindowOpen;
                return;
            }

            TogglePause();
        }

        wasInteractWindowOpenLastFrame = isInteractWindowOpen;
    }

    private void TogglePause()
    {
        if (!UI_StateManager.Instance.CanOpenMenu() && !UI_StateManager.Instance.menuOpen)
            return;

        bool willOpen = !UI_StateManager.Instance.menuOpen;

        if (!willOpen && settingsPanel != null && settingsPanel.activeSelf)
        {
            settingsPanel.SetActive(false);
            pausePanel.SetActive(true);
            return;
        }

        pausePanel.SetActive(willOpen);
        if (!willOpen && settingsPanel != null) settingsPanel.SetActive(false);

        Time.timeScale = willOpen ? 0f : 1f;
        UI_StateManager.Instance.menuOpen = willOpen;
    }

    private void OnContinue()
    {
        pausePanel.SetActive(false);
        Time.timeScale = 1f;
        UI_StateManager.Instance.menuOpen = false;
    }

    private void OnSettings()
    {
        pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    private void OnSettingsBack()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        pausePanel.SetActive(true);
    }

    private void OnExitToMainMenu()
    {
        Time.timeScale = 1f;
        UI_StateManager.Instance.menuOpen = false;

        if (SceneController.Instance != null)
            SceneController.Instance.LoadScene(mainMenuSceneName);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(mainMenuSceneName);
    }
}
