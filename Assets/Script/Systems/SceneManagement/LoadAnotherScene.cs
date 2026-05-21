using UnityEngine;
using UnityEngine.SceneManagement;

public enum TransitionType { Automatic, Manual } // AutomaticExit, ManualEntrance

public class LoadAnotherScene : MonoBehaviour
{
    [SerializeField] private TransitionType type = TransitionType.Automatic;
    [SerializeField] private string sceneName;
    [SerializeField] private string spawnId; // optional: id of the spawn point in target scene

    // Allows other scripts to set which scene to load
    public void SetSceneName(string newSceneName)
    {
        sceneName = newSceneName;
    }

    public void LoadSceneManually()
    {
        TriggerSceneLoad();
    }

    // Allows other scripts to immediately load a given scene (with optional spawn id)
    public void LoadSceneNow(string newSceneName, string newSpawnId = null)
    {
        if (string.IsNullOrEmpty(newSceneName)) return;

        if (DailyJournalRules.ShouldBlockLeavingHome(newSceneName, out string message))
        {
            DailyJournalRules.ShowMessage(message);
            return;
        }

        if (SceneController.Instance != null)
            SceneController.Instance.LoadScene(newSceneName, newSpawnId);
        else
            SceneManager.LoadScene(newSceneName);
    }

    private void TriggerSceneLoad()
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("No scene name assigned to " + gameObject.name);
            return;
        }

        if (DailyJournalRules.ShouldBlockLeavingHome(sceneName, out string message))
        {
            DailyJournalRules.ShowMessage(message);
            return;
        }

        if (SceneController.Instance != null)
            SceneController.Instance.LoadScene(sceneName, spawnId);
        else
            SceneManager.LoadScene(sceneName);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (type == TransitionType.Automatic)
        {
            if (collision.CompareTag("Player"))
            {
                TriggerSceneLoad();
            }
        }
    }
}
