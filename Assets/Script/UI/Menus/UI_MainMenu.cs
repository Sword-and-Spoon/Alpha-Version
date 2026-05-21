using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UI_MainMenu : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string newGameScene = "Zone1";

    [Header("Main Buttons")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button creditsButton;
    [SerializeField] private Button exitButton;

    [Header("Play Panel (Pick a save slot)")]
    [SerializeField] private GameObject playPanel;
    [SerializeField] private UI_PlayMenu playMenu;

    [Header("Logo / Title (hidden while Play panel is open)")]
    [SerializeField] private GameObject logoObject;

    [Header("Options Panel")]
    [SerializeField] private GameObject optionsPanel;

    [Header("Credits Panel")]
    [SerializeField] private GameObject creditsPanel;

    private void Start()
    {
        if (playButton != null)    playButton.onClick.AddListener(OnPlay);
        if (optionsButton != null) optionsButton.onClick.AddListener(OnOptions);
        if (creditsButton != null) creditsButton.onClick.AddListener(OnCredits);
        if (exitButton != null)    exitButton.onClick.AddListener(OnExit);
    }

    private void OnPlay()
    {
        if (logoObject != null) logoObject.SetActive(false);
        if (playPanel != null)  playPanel.SetActive(true);
        if (playMenu != null)   playMenu.Refresh();
    }

    public void OnPlayPanelClosed()
    {
        if (logoObject != null) logoObject.SetActive(true);
    }

    private void OnOptions()
    {
        if (optionsPanel != null) optionsPanel.SetActive(true);
    }

    private void OnCredits()
    {
        if (creditsPanel != null) creditsPanel.SetActive(true);
    }

    public void StartNewGame()
    {
        if (string.IsNullOrEmpty(newGameScene))
        {
            Debug.LogError("[UI_MainMenu] newGameScene is not set.");
            return;
        }
        if (SceneController.Instance != null)
            SceneController.Instance.LoadScene(newGameScene, null);
        else
            SceneManager.LoadScene(newGameScene);
    }

    private void OnExit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
