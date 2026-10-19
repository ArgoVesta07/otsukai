using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneTransition : MonoBehaviour
{
    public static SceneTransition Instance;

    [SerializeField] private string houseSceneName = "House";
    [SerializeField] private CanvasGroup loadingGroup;
    [SerializeField] private Slider loadingBar;       // bar loading (boleh dikosongin)
    [SerializeField] private float fadeDuration = 0.3f;
    [SerializeField] private float minLoadingTime = 0.8f;

    private bool isLoading;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (loadingGroup != null)
        {
            loadingGroup.alpha = 0f;
            loadingGroup.blocksRaycasts = false;
        }
    }

    public void GoToScene(string sceneName, Vector3 spawnPosition)
    {
        if (isLoading) return;

        string sceneToLoad = string.IsNullOrEmpty(sceneName) ? houseSceneName : sceneName;
        StartCoroutine(LoadRoutine(sceneToLoad, spawnPosition));
    }

    IEnumerator LoadRoutine(string sceneToLoad, Vector3 spawnPosition)
    {
        isLoading = true;
        InteractionState.IsInteracting = true;   // kunci gerakan selama transisi

        SetBar(0f);
        yield return Fade(1f);

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneToLoad);
        op.allowSceneActivation = false;

        float timer = 0f;
        float shown = 0f;

        while (op.progress < 0.9f || timer < minLoadingTime)
        {
            timer += Time.unscaledDeltaTime;

            // bar ngikutin yang paling lambat: progress asli atau waktu minimal
            float real = Mathf.Clamp01(op.progress / 0.9f);
            float byTime = Mathf.Clamp01(timer / minLoadingTime);
            float target = Mathf.Min(real, byTime);

            shown = Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime * 2f);
            SetBar(shown);
            yield return null;
        }

        SetBar(1f);
        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;
        yield return null;   // tunggu satu frame biar semua objek scene siap

        GameObject player = GameObject.Find("player");
        if (player != null)
            player.transform.position = spawnPosition;
        else
            Debug.LogWarning("Objek player tidak ditemukan di scene!");

        yield return Fade(0f);

        InteractionState.IsInteracting = false;
        isLoading = false;
    }

    void SetBar(float value)
    {
        if (loadingBar != null)
            loadingBar.value = value;
    }

    IEnumerator Fade(float target)
    {
        if (loadingGroup == null) yield break;

        loadingGroup.blocksRaycasts = true;
        float start = loadingGroup.alpha;
        float t = 0f;

        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            loadingGroup.alpha = Mathf.Lerp(start, target, t / fadeDuration);
            yield return null;
        }

        loadingGroup.alpha = target;
        loadingGroup.blocksRaycasts = target > 0f;
    }
}