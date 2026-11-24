using DetectiveGame.Core;

namespace CarXCase.Precinct
{
    public static class PortLead
    {
        public const string BarFinished = "bar_dmitry_finished";
        public const string AskWitness = "bar_ask_witness";
        public const string Witness = "clue_bar_witness";
        public const string Dmitry = "clue_dmitry_hangar";
        public const string PawnshopFinished = "pawnshop_boris_finished";
        public const string CheckSafe = "pawnshop_check_safe";
        public const string Safe = "clue_pawnshop_hangar_note";
        public const string Boris = "clue_boris_hangar";

        public static readonly string[] Bar = { Dmitry, Witness, BarServiceRoute.CoordinatesId, "clue_warehouse" };
        public static readonly string[] Pawnshop = { Boris, Safe, "clue_receipts", "clue_pawnshop_shipping_receipt" };

        public static bool HasAny(string[] leads) => System.Array.Exists(leads, ClueJournal.Has);
    }
}
