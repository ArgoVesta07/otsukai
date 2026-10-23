using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// Card shown when an NPC offers an errand (classic mode): who asks, the steps, Accept / Later.
// Put this on a root canvas (e.g. HudCanvas). Hook the Accept and Later buttons to the public methods.
[DefaultExecutionOrder(100)]   // after PauseMenu, so Esc closing this card doesn't also open the pause menu
public class ErrandOfferPanel : MonoBehaviour
{
    static ErrandOfferPanel instance;

    [SerializeField] GameObject panel;
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text stepsText;

    [Header("Text")]
    [Tooltip("{0} = NPC name")]
    [GameText] [SerializeField] string titleFormat = "Errand from {0}";
    [Tooltip("One line per step. {0} = number, {1} = step text")]
    [SerializeField] string stepFormat = "{0}. {1}";

    [Header("Keys")]
    [SerializeField] Key acceptKey = Key.E;
    [SerializeField] Key laterKey = Key.Escape;

    Action onAccept;
    int openedFrame;

    public static bool IsOpen => instance != null && instance.panel != null && instance.panel.activeSelf;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        if (panel != null) panel.SetActive(false);
    }

    // Shows the card. Returns false when there is no card in the scene (the caller can accept directly).
    public static bool Show(ErrandData data, string giverName, Action accept)
    {
        if (instance == null || instance.panel == null || data == null) return false;

        instance.Open(data, giverName, accept);
        return true;
    }

    void Open(ErrandData data, string giverName, Action accept)
    {
        onAccept = accept;
        openedFrame = Time.frameCount;

        string giver = string.IsNullOrEmpty(giverName) ? "" : Loc.T(giverName);

        if (titleText != null)
            titleText.text = Loc.F(titleFormat, giver);

        if (stepsText != null)
        {
            StringBuilder lines = new StringBuilder();
            if (data.steps != null)
            {
                for (int i = 0; i < data.steps.Length; i++)
                {
                    if (data.steps[i] == null) continue;
                    string step = Loc.T(data.steps[i].shortText).Replace("{giver}", giver);
                    if (lines.Length > 0) lines.Append('\n');
                    lines.Append(string.Format(stepFormat, i + 1, step));
                }
            }
            stepsText.text = lines.ToString();
        }

        InteractionState.IsInteracting = true;   // lock the player while the card is open
        panel.SetActive(true);
    }

    void Update()
    {
        if (!IsOpen) return;
        if (Time.frameCount == openedFrame) return;   // the key that closed the dialogue mustn't answer the card

        var k = Keyboard.current;
        if (k == null) return;

        if (k[acceptKey].wasPressedThisFrame) Accept();
        else if (k[laterKey].wasPressedThisFrame) Later();
    }

    public void Accept()
    {
        Action action = onAccept;
        Close();
        action?.Invoke();
    }

    public void Later()
    {
        Close();
    }

    void Close()
    {
        onAccept = null;
        if (panel != null) panel.SetActive(false);
        InteractionState.IsInteracting = false;
    }
}
