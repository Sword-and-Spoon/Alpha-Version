using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneController : MonoBehaviour
{
    public static SceneController Instance { get; private set; }

    private string pendingSpawnId;
    [SerializeField] Animator transitionAnimator; // Optional: assign an Animator for scene transition effects

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Call this to load a scene and optionally provide the spawn id to use in the new scene
    public void LoadScene(string sceneName, string spawnId = null)
    {
        StartCoroutine(PlayScene(sceneName, spawnId));
    }

    IEnumerator PlayScene(string sceneName, string spawnId = null)
    {
        pendingSpawnId = spawnId;

        // Fade in
        if (transitionAnimator != null)
        {
            transitionAnimator.SetTrigger("Start");
            // Wait for the animation to finish
            yield return new WaitForSeconds(1f);
        }

        // Load the scene asynchronously
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);

        // Wait until the scene is fully loaded
        while (!operation.isDone)
        {
            yield return null;
        }

        OnSceneLoaded();

        if (transitionAnimator != null)
        {
            // Fade out
            transitionAnimator.SetTrigger("End");
        }
    }

    // Move player to spawn point
    private void OnSceneLoaded()
    {
        var player = GameObject.FindGameObjectWithTag("Player");

        // If GameManager has a persistent player, ensure we are using that one
        if (GameManager.instance != null && GameManager.instance.player != null)
        {
            player = GameManager.instance.player;
        }

        if (!string.IsNullOrEmpty(pendingSpawnId))
        {
            bool movedToTargetSpawn = false;

            // Try direct lookup via registry first (doesn't depend on tag)
            SpawnPoint directSpawn = SpawnPoint.Find(pendingSpawnId);
            if (directSpawn != null && player != null)
            {
                player.transform.position = directSpawn.GetSpawnPosition();
                player.transform.rotation = directSpawn.transform.rotation;
                movedToTargetSpawn = true;
            }

            // Fallback to tag scan (legacy scenes)
            if (!movedToTargetSpawn)
            {
                var spawnObjects = GameObject.FindGameObjectsWithTag("SpawnPoint");
                foreach (var go in spawnObjects)
                {
                    var sp = go.GetComponent<SpawnPoint>();
                    if (sp != null && sp.spawnId == pendingSpawnId)
                    {
                        if (player != null)
                        {
                            player.transform.position = sp.GetSpawnPosition();
                            player.transform.rotation = go.transform.rotation;
                            movedToTargetSpawn = true;
                        }
                        break;
                    }
                }
            }

            if (!movedToTargetSpawn)
            {
                Debug.LogWarning($"[SceneController] SpawnPoint id '{pendingSpawnId}' not found in scene '{SceneManager.GetActiveScene().name}'.");
            }

            pendingSpawnId = null;
        }

        // --- CAMERA FIX ---
        // If using Cinemachine, we need to tell it to follow the persistent player
        // because the virtual camera in the new scene might be looking for a local player.
        var vcam = FindObjectOfType<Cinemachine.CinemachineVirtualCamera>();
        if (vcam != null && player != null)
        {
            vcam.Follow = player.transform;
            vcam.LookAt = player.transform;
        }
    }
}
