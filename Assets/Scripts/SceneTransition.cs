using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransition : MonoBehaviour
{
    public static SceneTransition Instance;

    [SerializeField] private string houseSceneName = "House";

    private Vector3 targetPosition;
    private bool hasTargetPosition;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void GoToScene(string sceneName, Vector3 spawnPosition)
    {
        string sceneToLoad = string.IsNullOrEmpty(sceneName) ? houseSceneName : sceneName;

        targetPosition = spawnPosition;
        hasTargetPosition = true;

        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.LoadScene(sceneToLoad);
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (!hasTargetPosition) return;

        GameObject player = GameObject.Find("player");

        if (player != null)
        {
            player.transform.position = targetPosition;
        }
        else
        {
            Debug.LogWarning("Objek player tidak ditemukan di scene!");
        }

        hasTargetPosition = false;
    }
}