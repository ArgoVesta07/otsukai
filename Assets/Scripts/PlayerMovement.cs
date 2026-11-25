using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public float speed = 5f;

    Rigidbody2D rb;
    Vector2 input;

    // Zones that slow the player down (StairZone), each with its own speed factor. The slowest one counts.
    readonly Dictionary<Object, float> speedFactors = new Dictionary<Object, float>();

    public void SetSpeedFactor(Object source, float factor) => speedFactors[source] = factor;
    public void ClearSpeedFactor(Object source) => speedFactors.Remove(source);

    float SpeedFactor
    {
        get
        {
            float factor = 1f;
            foreach (float f in speedFactors.Values) factor = Mathf.Min(factor, f);
            return factor;
        }
    }

    // The player in the loaded scene, found by component (not by object name), or null
    public static GameObject FindPlayer()
    {
        PlayerMovement player = FindAnyObjectByType<PlayerMovement>();
        return player != null ? player.gameObject : null;
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        if (InteractionState.IsInteracting)
        {
            input = Vector2.zero;
            return;
        }

        var k = Keyboard.current;
        if (k == null) return;

        float x = (k.dKey.isPressed || k.rightArrowKey.isPressed ? 1 : 0)
                - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1 : 0);
        float y = (k.wKey.isPressed || k.upArrowKey.isPressed ? 1 : 0)
                - (k.sKey.isPressed || k.downArrowKey.isPressed ? 1 : 0);

        input = new Vector2(x, y).normalized;
    }

    void FixedUpdate()
    {
        rb.linearVelocity = input * speed * SpeedFactor;
    }
}