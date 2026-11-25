using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneTransition : MonoBehaviour
{
    public static SceneTransition Instance;

    [SerializeField] private string houseSceneName = "House";
    [SerializeField] private CanvasGroup loadingGroup;
    [SerializeField] private Slider loadingBar;       // loading bar (can be left empty)
    [SerializeField] private float fadeDuration = 0.3f;
    [SerializeField] private float minLoadingTime = 0.8f;

    [Header("Doors inside the same scene")]
    [Tooltip("Fade for doors with Stay In Scene. Empty = a plain full-screen fade is made at start.")]
    [SerializeField] private CanvasGroup fadeGroup;
    [SerializeField] private Color fadeColor = Color.black;
    [SerializeField] private float roomFadeDuration = 0.15f;

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

        if (fadeGroup == null) fadeGroup = MakeFade();
    }

    // A full-screen image on its own canvas, above everything
    CanvasGroup MakeFade()
    {
        GameObject go = new GameObject("RoomFade", typeof(Canvas), typeof(CanvasGroup), typeof(Image));
        go.transform.SetParent(transform, false);
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        go.GetComponent<Image>().color = fadeColor;
        CanvasGroup group = go.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        return group;
    }

    // Door with Stay In Scene: short fade, move the player, switch the camera to the room's bounds (or back to town)
    public void MoveInScene(string spawnAtTarget, Vector2 offset, Vector3 fallback)
    {
        if (isLoading) return;
        StartCoroutine(MoveRoutine(spawnAtTarget, offset, fallback));
    }

    IEnumerator MoveRoutine(string spawnAtTarget, Vector2 offset, Vector3 fallback)
    {
        isLoading = true;
        InteractionState.IsInteracting = true;
        yield return Fade(fadeGroup, 1f, roomFadeDuration);

        NavTarget target = NavTarget.FindExact(spawnAtTarget);
        Vector3 pos = target != null ? target.transform.position + (Vector3)offset : fallback;
        if (target == null) Debug.LogWarning("No NavTarget '" + spawnAtTarget + "' in this scene, using the Door's Spawn Position.");

        GameObject player = PlayerMovement.FindPlayer();
        if (player != null) player.transform.position = new Vector3(pos.x, pos.y, player.transform.position.z);
        Physics2D.SyncTransforms();

        CameraFollow cam = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
        if (cam != null)
        {
            RoomArea room = target != null ? target.GetComponentInParent<RoomArea>() : null;
            if (room != null) cam.SetBounds(room.Center, room.size);
            else cam.ResetBounds();
        }

        yield return null;   // the camera catches up before the fade opens
        yield return Fade(fadeGroup, 0f, roomFadeDuration);
        InteractionState.IsInteracting = false;
        isLoading = false;
    }

    public void GoToScene(string sceneName, Vector3 spawnPosition)
    {
        GoToScene(sceneName, null, Vector2.zero, spawnPosition);
    }

    // spawnAtTarget: NavTarget id in the loaded scene where the player appears (plus offset).
    // spawnPosition is used when the id is empty or no such NavTarget exists there.
    public void GoToScene(string sceneName, string spawnAtTarget, Vector2 offset, Vector3 spawnPosition)
    {
        if (isLoading) return;

        string sceneToLoad = string.IsNullOrEmpty(sceneName) ? houseSceneName : sceneName;
        StartCoroutine(LoadRoutine(sceneToLoad, spawnAtTarget, offset, spawnPosition));
    }

    IEnumerator LoadRoutine(string sceneToLoad, string spawnAtTarget, Vector2 offset, Vector3 spawnPosition)
    {
        isLoading = true;
        InteractionState.IsInteracting = true;   // lock movement during the transition

        SetBar(0f);
        yield return Fade(1f);

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneToLoad);
        op.allowSceneActivation = false;

        float timer = 0f;
        float shown = 0f;

        while (op.progress < 0.9f || timer < minLoadingTime)
        {
            timer += Time.unscaledDeltaTime;

            // The bar follows whichever is slower: actual progress or the minimum time.
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
        yield return null;   // wait one frame so all scene objects are ready

        if (!string.IsNullOrEmpty(spawnAtTarget))
        {
            NavTarget target = NavTarget.FindExact(spawnAtTarget);
            if (target != null)
                spawnPosition = target.transform.position + (Vector3)offset;
            else
                Debug.LogWarning("No NavTarget '" + spawnAtTarget + "' in " + sceneToLoad + ", using the Door's Spawn Position.");
        }

        GameObject player = PlayerMovement.FindPlayer();
        if (player != null)
            player.transform.position = new Vector3(spawnPosition.x, spawnPosition.y, player.transform.position.z);
        else
            Debug.LogWarning("Player object was not found in the scene!");

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
        yield return Fade(loadingGroup, target, fadeDuration);
    }

    static IEnumerator Fade(CanvasGroup group, float target, float duration)
    {
        if (group == null) yield break;

        group.blocksRaycasts = true;
        float start = group.alpha;
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }

        group.alpha = target;
        group.blocksRaycasts = target > 0f;
    }
}