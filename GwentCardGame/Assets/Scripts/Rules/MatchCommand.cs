using System.IO;

namespace Gwent.Rules
{
    public enum CommandKind : byte
    {
        ChooseFirst,    // the Scoia'tael side says who goes first (Flag: itself)
        Redraw,         // a card of the opening hand goes back for the deck's top card
        EndRedraw,      // the hand is kept
        Play,           // a card from the hand (a decoy on TargetId)
        Leader,         // the leader's ability (CardId: the weather card a weather leader plays, when chosen already)
        Pass,
        Choose,         // the pending choice's card (a revived unit or a weather card: on Row); a look ends
        Discard,        // a card of the hand, for Eredin, Destroyer of Worlds
        Skip,           // the pending choice given up (the turn ends)
        GiveUp,
    }

    // A side's move or answer, as one device sends it to the match (Match.Apply). Plain data: it is what a battle's log
    // keeps and what travels between two devices.
    public struct MatchCommand
    {
        public CommandKind Kind;
        public int CardId;      // the card played, redrawn, discarded or chosen (-1: none)
        public int HandIndex;   // its place in the hand when known (with two alike, the one chosen); -1
        public Row Row;         // where it goes: a unit's row, a horn's row, Row.Weather for weather and Scorch; a decoy's target's row
        public int TargetId;    // a decoy's target (-1: none)
        public bool Flag;       // ChooseFirst: the side choosing goes first

        public static MatchCommand Of(CommandKind kind, int card_id = -1, Row row = Row.Close, int hand_index = -1, int target_id = -1)
        {
            return new MatchCommand { Kind = kind, CardId = card_id, Row = row, HandIndex = hand_index, TargetId = target_id };
        }

        public static MatchCommand ChooseFirst(bool me_first) { MatchCommand c = Of(CommandKind.ChooseFirst); c.Flag = me_first; return c; }
        public static MatchCommand Redraw(int card_id, int hand_index = -1) { return Of(CommandKind.Redraw, card_id, Row.Close, hand_index); }
        public static MatchCommand EndRedraw() { return Of(CommandKind.EndRedraw); }
        public static MatchCommand Play(int card_id, Row row, int hand_index = -1) { return Of(CommandKind.Play, card_id, row, hand_index); }
        public static MatchCommand Decoy(int decoy_id, int target_id, Row row, int hand_index = -1) { return Of(CommandKind.Play, decoy_id, row, hand_index, target_id); }
        public static MatchCommand Leader(int weather_card_id = -1) { return Of(CommandKind.Leader, weather_card_id, Row.Weather); }
        public static MatchCommand Pass() { return Of(CommandKind.Pass); }
        public static MatchCommand Choose(int card_id, Row row = Row.Close) { return Of(CommandKind.Choose, card_id, row); }
        public static MatchCommand Discard(int card_id, int hand_index = -1) { return Of(CommandKind.Discard, card_id, Row.Close, hand_index); }
        public static MatchCommand Skip() { return Of(CommandKind.Skip); }
        public static MatchCommand GiveUp() { return Of(CommandKind.GiveUp); }

        // The AI's move as a command
        public static MatchCommand From(AiAction action)
        {
            switch (action.Type)
            {
                case AiActionType.PlayCard: return action.TargetId >= 0 ? Decoy(action.CardId, action.TargetId, action.Row) : Play(action.CardId, action.Row);
                case AiActionType.UseLeader: return Leader(action.CardId);
                default: return Pass();
            }
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write((byte)Kind);
            writer.Write(CardId);
            writer.Write((short)HandIndex);
            writer.Write((byte)Row);
            writer.Write(TargetId);
            writer.Write(Flag);
        }

        public static MatchCommand Read(BinaryReader reader)
        {
            return new MatchCommand
            {
                Kind = (CommandKind)reader.ReadByte(),
                CardId = reader.ReadInt32(),
                HandIndex = reader.ReadInt16(),
                Row = (Row)reader.ReadByte(),
                TargetId = reader.ReadInt32(),
                Flag = reader.ReadBoolean(),
            };
        }

        public override string ToString()
        {
            switch (Kind)
            {
                case CommandKind.ChooseFirst: return "choose first: " + (Flag ? "me" : "them");
                case CommandKind.EndRedraw:
                case CommandKind.Pass:
                case CommandKind.Skip:
                case CommandKind.GiveUp: return Kind.ToString();
                case CommandKind.Play: return "play " + CardId + " on " + Row + (TargetId >= 0 ? " for " + TargetId : "") + (HandIndex >= 0 ? " (hand " + HandIndex + ")" : "");
                case CommandKind.Leader: return "leader" + (CardId >= 0 ? " " + CardId : "");
                default: return Kind + " " + CardId + (Kind == CommandKind.Choose ? " on " + Row : "");
            }
        }
    }
}
