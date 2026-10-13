using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Hold to pass: the left button (or a finger) on the button, or the Space key as in The Witcher 3
public class PassManager : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private bool pointerDown;
    private float pointerDownTimer;
    private bool keyHeld;

    public float requiredHoldTime;
    public UnityEvent onLongClick;
    public Image fillImage;

    // Held with the left button (or a finger), like every other button
    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            pointerDown = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            Reset();
    }

    // The button is hidden when the turn changes: a press in progress must not carry over to the next turn
    private void OnDisable()
    {
        Reset();
    }

    private void Start()
    {
        // The Witcher 3's "[SPACE] [HOLD] Pass" hint, on keyboards
        if (Application.isMobilePlatform)
            return;
        TMPro.TextMeshProUGUI label = GetComponentInChildren<TMPro.TextMeshProUGUI>();
        if (label != null)
            label.text = "<size=72%><color=#" + ColorUtility.ToHtmlStringRGB(UITheme.Current.textSecondary) + ">[Space]</color></size>  Hold to pass";
        RectTransform rect = (RectTransform)transform;
        rect.sizeDelta = new Vector2(Mathf.Max(rect.sizeDelta.x, 300), rect.sizeDelta.y);
    }

    private void Update()
    {
        // Space counts while it stays down; releasing it (or a dialog opening) cancels the hold
        Keyboard keyboard = Keyboard.current;
        bool key_down = keyboard != null && keyboard.spaceKey.isPressed && !UIOverlay.HasModal;
        if (key_down && !keyHeld && keyboard.spaceKey.wasPressedThisFrame)
        {
            keyHeld = true;
            pointerDown = true;
        }
        else if (keyHeld && !key_down)
        {
            Reset();
        }

        if (pointerDown)
        {
            pointerDownTimer += Time.deltaTime;
            if (pointerDownTimer >= requiredHoldTime)
            {
                if (onLongClick != null)
                    onLongClick.Invoke();

                Reset();
            }
            fillImage.fillAmount = pointerDownTimer / requiredHoldTime;
        }
    }

    private void Reset()
    {
        pointerDown = false;
        keyHeld = false;
        pointerDownTimer = 0;
        fillImage.fillAmount = 0;
    }
}
