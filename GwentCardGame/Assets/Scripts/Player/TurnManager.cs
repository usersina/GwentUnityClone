using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Scoia'tael's perk: the player decides who takes the first turn. A themed dialog over the dimmed board, built
// the first time the picker shows.
public class TurnManager : MonoBehaviour
{
    [HideInInspector]
    public SceneController controller;

    private GameObject dialog;
    private Button goFirst;

    private void Start()
    {
        controller = SceneController.Instance;
    }

    private void OnEnable()
    {
        if (dialog == null)
            Build();
        UIKit.Focus(goFirst); // Enter goes first
    }

    private void Build()
    {
        RectTransform panel = UIKit.Panel(transform, "Dialog").Place(new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(900, 420));
        UIKit.Ornament(panel, new Vector2(0.5f, 1f), new Vector2(0, -46), 400);

        TextMeshProUGUI title = UIKit.Text(panel, "Scoia'tael perk", UIKit.TextStyle.Heading, TextAlignmentOptions.Center);
        title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -100), new Vector2(820, 60));
        TextMeshProUGUI body = UIKit.Text(panel, "You decide who takes the first turn.", UIKit.TextStyle.Body, TextAlignmentOptions.Center);
        body.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -168), new Vector2(820, 44));
        TextMeshProUGUI hint = UIKit.Text(panel, "Going second lets you answer the opponent's first move.", UIKit.TextStyle.Caption, TextAlignmentOptions.Center);
        hint.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -214), new Vector2(820, 36));

        goFirst = UIKit.Button(panel, "Go first", UIKit.ButtonStyle.Primary, GoFirst, new Vector2(360, 72));
        ((RectTransform)goFirst.transform).Place(new Vector2(0.5f, 0f), new Vector2(-195, 78), new Vector2(360, 72));
        Button goSecond = UIKit.Button(panel, "Go second", UIKit.ButtonStyle.Secondary, GoSecond, new Vector2(360, 72));
        ((RectTransform)goSecond.transform).Place(new Vector2(0.5f, 0f), new Vector2(195, 78), new Vector2(360, 72));
        dialog = panel.gameObject;
    }

    public void GoFirst()
    {
        if (controller == null)
            controller = SceneController.Instance;
        GameLog.Info("Going First");
        controller.TurnCallBack(false);
        gameObject.SetActive(false);
    }

    public void GoSecond()
    {
        if (controller == null)
            controller = SceneController.Instance;
        // Start Enemy Turn
        GameLog.Info("Going Second");
        controller.TurnCallBack(true);
        gameObject.SetActive(false);
    }
}
