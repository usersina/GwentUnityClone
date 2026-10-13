using System.Collections.Generic;
using Gwent.Rules;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The end of the battle: the outcome in large letters, the round tally, a scoreboard with both sides (leader
// portrait, name, faction, every round's score, a laurel behind each round's winning score) and the way on.
// Built when the battle ends, over the dimmed board.
public class PostGameManager : MonoBehaviour
{
    private const float SideColumn = 400;
    private const float RoundColumn = 170;
    private const float RowHeight = 116;

    public void Show(BattleState outcome, SceneController.MainInfo player, SceneController.MainInfo opponent)
    {
        UITheme theme = UITheme.Current;
        foreach (Transform child in transform)
            Destroy(child.gameObject);

        RectTransform content = UIKit.Rect("Content", transform).Stretch();
        content.gameObject.AddComponent<UIAppear>();

        Color color = outcome == BattleState.WON ? theme.goldBright : outcome == BattleState.LOST ? theme.danger : theme.textSecondary;
        string title = outcome == BattleState.WON ? "Victory" : outcome == BattleState.LOST ? "Defeat" : "Draw";
        int player_rounds = 2 - opponent.Lives, opponent_rounds = 2 - player.Lives;
        string tally = outcome == BattleState.WON ? "You won the battle, " + player_rounds + " rounds to " + opponent_rounds
            : outcome == BattleState.LOST ? "The opponent won the battle, " + opponent_rounds + " rounds to " + player_rounds
            : "Neither side came out ahead";

        Image glow = UIKit.NewImage(content, "Glow", theme.radial, UITheme.WithAlpha(color, 0.22f));
        glow.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 250), new Vector2(1500, 700));
        TextMeshProUGUI heading = UIKit.Text(content, title, UIKit.TextStyle.Title, TextAlignmentOptions.Center);
        heading.fontSize = 120;
        heading.characterSpacing = 10;
        heading.color = color;
        heading.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 300), new Vector2(1200, 150));
        UIKit.Ornament(content, new Vector2(0.5f, 0.5f), new Vector2(0, 222), 620);
        TextMeshProUGUI subtitle = UIKit.Text(content, tally, UIKit.TextStyle.Body, TextAlignmentOptions.Center);
        subtitle.color = theme.textSecondary;
        subtitle.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 176), new Vector2(1200, 44));

        BuildScoreboard(content, player, opponent);

        Button again = UIKit.Button(content, "Play again", UIKit.ButtonStyle.Primary, PlayAgain, new Vector2(300, 72));
        ((RectTransform)again.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(-170, -330), new Vector2(300, 72));
        Button menu = UIKit.Button(content, "Main menu", UIKit.ButtonStyle.Secondary, ToMainMenu, new Vector2(300, 72));
        ((RectTransform)menu.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(170, -330), new Vector2(300, 72));
        UIKit.Focus(again);
    }

    //----------------------------------------Scoreboard----------------------------------------//
    private void BuildScoreboard(RectTransform content, SceneController.MainInfo player, SceneController.MainInfo opponent)
    {
        UITheme theme = UITheme.Current;
        float width = SideColumn + 3 * RoundColumn + 60;
        RectTransform board = UIKit.Panel(content, "Scoreboard").Place(new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(width, 70 + 2 * RowHeight + 20));

        for (int round = 0; round < 3; round++)
        {
            TextMeshProUGUI header = UIKit.Text(board, "Round " + (round + 1), UIKit.TextStyle.Tab, TextAlignmentOptions.Center);
            header.color = theme.textMuted;
            header.rectTransform.Place(new Vector2(0, 1), new Vector2(30 + SideColumn + (round + 0.5f) * RoundColumn, -40), new Vector2(RoundColumn, 40));
        }
        UIKit.Divider(board, -70, 30);

        AddRow(board, 0, player, "Player", player.RoundsScore, opponent.RoundsScore);
        UIKit.Divider(board, -70 - RowHeight, 30);
        AddRow(board, 1, opponent, "Opponent", opponent.RoundsScore, player.RoundsScore);
    }

    private void AddRow(RectTransform board, int index, SceneController.MainInfo info, string name, List<int> scores, List<int> other_scores)
    {
        UITheme theme = UITheme.Current;
        float y = -70 - index * RowHeight - RowHeight / 2;

        // Leader portrait in a gold ring
        Image mask = UIKit.NewImage(board, "Portrait", theme.circle, Color.white);
        mask.rectTransform.Place(new Vector2(0, 1), new Vector2(30 + 56, y), new Vector2(84, 84));
        mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        Sprite art = SpriteCache.Load("Cards/List/591x380/" + info.Leader);
        if (art != null)
        {
            Image portrait = UIKit.NewImage(mask.transform, "Art", art, Color.white);
            portrait.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150, 150 * 591f / 380f), new Vector2(0.5f, 0.72f));
        }
        Image ring = UIKit.NewImage(board, "Ring", theme.ring, theme.gold);
        ring.rectTransform.Place(new Vector2(0, 1), new Vector2(30 + 56, y), new Vector2(92, 92));

        TextMeshProUGUI who = UIKit.Text(board, name, UIKit.TextStyle.Heading, TextAlignmentOptions.BottomLeft);
        who.fontSize = 32;
        who.color = theme.text;
        who.rectTransform.Place(new Vector2(0, 1), new Vector2(30 + 120, y + 4), new Vector2(270, 44), new Vector2(0, 0));
        TextMeshProUGUI faction = UIKit.Text(board, FactionName(info.Faction), UIKit.TextStyle.Caption, TextAlignmentOptions.TopLeft);
        faction.rectTransform.Place(new Vector2(0, 1), new Vector2(30 + 120, y - 2), new Vector2(270, 34), new Vector2(0, 1));

        for (int round = 0; round < 3; round++)
        {
            Vector2 cell = new Vector2(30 + SideColumn + (round + 0.5f) * RoundColumn, y);
            bool played = round < scores.Count && round < other_scores.Count;
            bool won = played && scores[round] > other_scores[round];
            bool lost = played && scores[round] < other_scores[round];
            if (won)
            {
                Image laurel = UIKit.NewImage(board, "Laurel", SpriteCache.Load("Various/pass"), UITheme.WithAlpha(theme.gold, 0.85f));
                laurel.preserveAspect = true;
                laurel.rectTransform.Place(new Vector2(0, 1), cell, new Vector2(110, 96));
            }
            TextMeshProUGUI score = UIKit.Text(board, played ? scores[round].ToString() : "–", UIKit.TextStyle.Heading, TextAlignmentOptions.Center);
            score.fontSize = 44;
            score.color = !played ? theme.textMuted : won ? theme.goldBright : lost ? theme.textMuted : theme.text;
            score.rectTransform.Place(new Vector2(0, 1), cell, new Vector2(RoundColumn, 60));
        }
    }

    private static string FactionName(string faction)
    {
        switch (faction)
        {
            case Factions.Monsters: return "Monsters";
            case Factions.Nilfgaard: return "Nilfgaardian Empire";
            case Factions.NorthernRealms: return "Northern Realms";
            case Factions.Scoiatael: return "Scoia'tael";
            default: return faction;
        }
    }

    //----------------------------------------Buttons----------------------------------------//
    public void PlayAgain()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneNames.Game);
    }

    public void ToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneNames.Menu);
    }
}
