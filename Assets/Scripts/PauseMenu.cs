using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Pause menu: Esc pauses the game (time stops, the player is locked) and shows a panel with
// Resume / Main Menu. Quitting the game is done from the main menu.
// Put this on its own root canvas (e.g. PauseCanvas).
public class PauseMenu : MonoBehaviour
{
    static PauseMenu instance;

    [SerializeField] GameObject panel;           // the pause panel with the buttons
    [SerializeField] GameObject confirmPanel;    // optional "Unsaved progress will be lost" panel
    [SerializeField] Key pauseKey = Key.Escape;
    [SerializeField] string mainMenuScene = "MainMenu";

    public static bool IsPaused { get; private set; }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        IsPaused = false;
        if (panel != null) panel.SetActive(false);
        if (confirmPanel != null) confirmPanel.SetActive(false);
    }

    void Update()
    {
        var k = Keyboard.current;
        if (k == null || !k[pauseKey].wasPressedThisFrame) return;

        if (IsPaused)
        {
            // Esc on the confirm panel goes back to the pause panel first
            if (confirmPanel != null && confirmPanel.activeSelf)
                CancelMainMenu();
            else
                Resume();
        }
        else if (!InteractionState.IsInteracting)   // not during dialogue, loading or banners
        {
            Pause();
        }
    }

    public void Pause()
    {
        IsPaused = true;
        Time.timeScale = 0f;                       // stops movement and all timers
        InteractionState.IsInteracting = true;     // blocks E interactions and the inventory key

        if (panel != null) panel.SetActive(true);
        if (confirmPanel != null) confirmPanel.SetActive(false);
    }

    // Hook to the Resume button
    public void Resume()
    {
        if (panel != null) panel.SetActive(false);
        if (confirmPanel != null) confirmPanel.SetActive(false);

        Time.timeScale = 1f;
        InteractionState.IsInteracting = false;
        IsPaused = false;
    }

    // Hook to the Main Menu button. Only asks for confirmation when there is unsaved progress.
    public void AskMainMenu()
    {
        if (confirmPanel != null && SaveSystem.HasUnsavedProgress())
        {
            confirmPanel.SetActive(true);
            if (panel != null) panel.SetActive(false);
        }
        else
        {
            GoToMainMenu();
        }
    }

    // Hook to the confirm panel's "No" button
    public void CancelMainMenu()
    {
        if (confirmPanel != null) confirmPanel.SetActive(false);
        if (panel != null) panel.SetActive(true);
    }

    // Hook to the confirm panel's "Yes" button. Progress since the last save is lost.
    public void GoToMainMenu()
    {
        IsPaused = false;
        GameSession.End();
        SceneManager.LoadScene(mainMenuScene);
    }
}