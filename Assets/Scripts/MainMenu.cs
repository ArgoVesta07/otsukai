using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    public string gameSceneName = "MainScene";

    [Tooltip("Optional. Hidden when there is no save file.")]
    [SerializeField] Button continueButton;

    void Start()
    {
        if (continueButton != null)
            continueButton.gameObject.SetActive(SaveSystem.HasSave);
    }

    // New game: starts from the beginning. The old save file stays until the next save.
    public void StartGame()
    {
        SaveSystem.ResetProgress();
        SceneManager.LoadScene(gameSceneName);
    }

    public void ContinueGame()
    {
        if (!SaveSystem.LoadGame(gameSceneName))
            Debug.LogWarning("Couldn't load the save.");
    }

    public void QuitGame()
    {
        Debug.Log("Quit");
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}