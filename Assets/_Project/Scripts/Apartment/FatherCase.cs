namespace CarXCase.Apartment
{
    public static class FatherCase
    {
        public const string Door = "clue_door_lock";
        public const string Valuables = "clue_valuables";
        public const string Matches = "clue_bar_matches";
        public const string Messages = "clue_answering_machine";
        public const string Record = "clue_record";
        public const string Bar = "concl_dmitry_bar";
        public const string Port = "concl_father_port";
        public const string Guest = "concl_guest";

        public static readonly string[] RequiredClues = { Door, Valuables, Matches, Messages };

        public static readonly (string id, string title, string observation, string thought)[] Evidence =
        {
            (Door, "Дверь открыли изнутри",
                "Личинка и косяк целы. На полке внутри лежат ключи отца с потёртым якорем.",
                "Следов лома или отмычки нет. Похоже, отец сам впустил кого-то знакомого."),
            (Valuables, "Ценности не тронуты",
                "ЭЛТ-телевизор, часы «Победа» и серебряный портсигар остались на месте. Ящики вывернуты.",
                "Обычные воры забрали бы телевизор и часы. Здесь искали конкретную вещь или бумаги."),
            (Matches, "Коробок из бара «Стерлядь»",
                "На тумбочке лежит коробок с осетром и надписью «Стерлядь».",
                "Спички из Митькиного бара. Их мог принести отец. Или Дмитрий был здесь вчера."),
            (Messages, "Запись с автоответчика",
                "19:15 — гудки, стук и далёкий портовый гудок. 21:40 — неизвестный требует передать Дмитрию угрозу. Товар или пятнадцать тысяч до утра пятницы. На двенадцатом пирсе вскрыт контейнер, бумаги пропали.",
                "Первая запись почти стёрта. Надо спросить Дмитрия и проверить смену отца в порту."),
            (Record, "Пластинка отца",
                "На проигрывателе осталась джазовая пластинка. Она лежит стороной Б вверх, рядом потёртый конверт.",
                "Сторона Б. По воскресеньям отец ставил её, пока варил кофе. Я ещё не открывал глаза, а по первому щелчку уже знал: папа дома.")
        };
    }
}
