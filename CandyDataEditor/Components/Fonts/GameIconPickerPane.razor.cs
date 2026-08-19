// Components/GameIconPickerPane.razor.cs
using Microsoft.AspNetCore.Components;

namespace CandyDataEditor.Components.Fonts
{
    public partial class GameIconPickerPane : ComponentBase
    {
        [Parameter] public string SearchTerm { get; set; } = string.Empty;
        [Parameter] public string MaxHeight { get; set; } = "380px";
        [Parameter] public EventCallback<string> OnIconSelected { get; set; }

        protected string searchTerm = string.Empty;

        protected override void OnParametersSet()
        {
            if (!string.IsNullOrEmpty(SearchTerm))
            {
                searchTerm = SearchTerm;
            }
        }

        protected async Task SelectIconAsync(string iconTag)
        {
            if (OnIconSelected.HasDelegate)
            {
                await OnIconSelected.InvokeAsync(iconTag);
            }
        }

        public int FilteredCount => FilteredIcons.Count;

        protected List<string> FilteredIcons => string.IsNullOrWhiteSpace(searchTerm)
            ? AllIcons
            : AllIcons.Where(i => i.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)).ToList();

        protected static readonly List<string> AllIcons = new()
        {
            "action", "actor", "aegis", "allfoes", "anvil", "any", "aquarius", "aries", "armor", "arrowhead",
            "banish", "book", "booklet", "boss", "brute", "burn", "buycard", "cancer", "capricorn",
            "card10clubs", "card10diamonds", "card10hearts", "card10spades",
            "card2clubs", "card2diamonds", "card2hearts", "card2spades",
            "card3clubs", "card3diamonds", "card3hearts", "card3spades",
            "card4clubs", "card4diamonds", "card4hearts", "card4spades",
            "card5clubs", "card5diamonds", "card5hearts", "card5spades",
            "card6clubs", "card6diamonds", "card6hearts", "card6spades",
            "card7clubs", "card7diamonds", "card7hearts", "card7spades",
            "card8clubs", "card8diamonds", "card8hearts", "card8spades",
            "card9clubs", "card9diamonds", "card9hearts", "card9spades",
            "cardaceclubs", "cardacediamonds", "cardacehearts", "cardacespades",
            "cardjackclubs", "cardjackdiamonds", "cardjackhearts", "cardjackspades", "cardjoker",
            "cardkingclubs", "cardkingdiamonds", "cardkinghearts", "cardkingspades",
            "cardqueenclubs", "cardqueendiamonds", "cardqueenhearts", "cardqueenspades",
            "cardboardboxclosed", "cardboardbox", "coin", "combat", "concat", "critical", "cycle",
            "d10", "d12", "d201", "d2020", "d4", "d61", "d62", "d63", "d64", "d65", "d66", "d8",
            "damagenull", "damagex", "damage", "day", "defense", "dicefire", "diceshield", "dicetarget", "dice", "diff",
            "directionaldamagenull", "directionaldamagex", "directionaldamage", "discard", "draw", "eatery", "elite",
            "ethereal", "event", "exchange", "flying", "foe", "foes", "fudgeblank", "fudgeminus", "fudgeplus", "gain",
            "gemini", "goto", "health", "hero", "heroes", "idcard", "interruption", "item", "keycard", "key", "leo",
            "libra", "location", "lock", "lose", "marker", "melee", "might", "minus", "mob", "moondial", "move", "nextchoice",
            "no", "npc", "null", "onehanded", "openbook", "overlord", "passive", "pathfinder", "pickup", "pisces", "place",
            "play", "plus", "pull", "push", "quest", "random", "range", "ranged", "reaction", "refreshmarker", "refresh",
            "remove", "repeat2", "repeat3", "repeatcritical", "repeatdice", "repeatfoe", "repeatmarker",
            "repeatmight", "repeatthreat", "repeatvalor", "repeatwarning", "repeatweapon", "repeatwisdom", "repeat",
            "reroll", "retrieve", "return", "room", "sage", "sagittarius", "scorpio", "search", "sellcard", "slota",
            "slotaegislocked", "slotaegis", "slotany", "slotb", "slotblank", "slotc", "slotd", "slote",
            "slotlocked", "slotmarkerlocked", "slotmarker", "slotmightlocked", "slotmight", "slotminus",
            "slotplus", "slotrefresh", "slotthreat", "slotvalorlocked", "slotvalor", "slotwisdomlocked",
            "slotwisdom", "slotx", "snap", "special", "stack", "sundial", "swap", "tableau", "target", "taurus",
            "threat", "tierbronze", "tiergold", "tierplatinum", "tiersilver", "tierstarter", "trade", "trophy",
            "twohanded", "valor", "vanguard", "virgo", "warblade", "warning", "weapon", "wheel", "wing", "wisdom", "x", "yes"
        };
    }
}
