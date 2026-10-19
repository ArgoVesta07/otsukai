using System;
using TMPro;
using UnityEngine;

public class DialogBox : MonoBehaviour
{
    static DialogBox instance;

    [SerializeField] GameObject panel;
    [SerializeField] TMP_Text nameText;
    [SerializeField] TMP_Text bodyText;   // Overflow must be set to Page in the Inspector

    string[] lines;
    int index;
    Action onFinished;

    public static bool IsOpen => instance != null && instance.panel.activeSelf;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        panel.SetActive(false);
    }

    // Opens the box and shows the first line. onFinished runs after the last line.
    public static void Open(string speaker, string[] newLines, Action finished = null)
    {
        if (instance == null)
        {
            Debug.LogWarning("DialogBox was not found!");
            finished?.Invoke();
            return;
        }

        instance.nameText.text = speaker;
        instance.lines = newLines;
        instance.index = 0;
        instance.onFinished = finished;
        instance.panel.SetActive(true);   // must be active before measuring the text
        instance.ShowLine();
    }

    // Next page of the current line, or the next line, or close after the last one.
    public static void Next()
    {
        if (!IsOpen) return;

        // Still has pages left in this line? Show the next page first.
        if (instance.bodyText.pageToDisplay < instance.bodyText.textInfo.pageCount)
        {
            instance.bodyText.pageToDisplay++;
            return;
        }

        instance.index++;

        if (instance.index >= instance.lines.Length)
        {
            Action done = instance.onFinished;
            instance.Close();
            done?.Invoke();
        }
        else
        {
            instance.ShowLine();
        }
    }

    // Closes the box without running onFinished.
    public static void Cancel()
    {
        if (instance != null) instance.Close();
    }

    void ShowLine()
    {
        bodyText.text = lines[index];
        bodyText.pageToDisplay = 1;

        // Update layout now so textInfo.pageCount is correct this frame
        Canvas.ForceUpdateCanvases();
        bodyText.ForceMeshUpdate();
    }

    void Close()
    {
        panel.SetActive(false);
        onFinished = null;
    }
}