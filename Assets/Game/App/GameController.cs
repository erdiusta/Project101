using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Project101.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Project101.App
{
    /// <summary>
    /// Runs a single player match: the human sits at seat 0 and bots fill the other three.
    /// All rule decisions are made by <see cref="Round"/>; this only turns clicks into moves.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        public const int Human = 0;
        string[] names;

        [SerializeField] TableView table;
        [SerializeField] UIDocument hud;

        /// <summary>Seconds a bot waits before playing, so its move can be followed.</summary>
        public float BotDelay = 0.7f;

        public Match Match { get; private set; }
        Round Round => Match.Round;

        readonly Tile?[] slots = new Tile?[TableView.RackSlots];
        readonly List<Tile> selected = new List<Tile>();
        readonly List<List<Tile>> staged = new List<List<Tile>>();
        Coroutine bots;
        string message = "";

        Label status, opening, scores, resultsText;
        Button meld, open, unstage, discard, giveBack, next;
        VisualElement results;

        void Start()
        {
            var root = hud.rootVisualElement;
            Localization.Apply(root);
            names = Enumerable.Range(0, Round.Players).Select(seat => Localization.Text("seat." + seat)).ToArray();
            status = root.Q<Label>("status");
            opening = root.Q<Label>("opening");
            scores = root.Q<Label>("scores");
            results = root.Q("results");
            resultsText = root.Q<Label>("results-text");

            meld = Bind(root, "meld", MeldSelected);
            open = Bind(root, "open", OpenStaged);
            unstage = Bind(root, "unstage", Unstage);
            discard = Bind(root, "discard", DiscardSelected);
            giveBack = Bind(root, "return", () => Do(Round.ReturnTakenDiscard(Human)));
            next = Bind(root, "next", NextRound);
            Bind(root, "sort-runs", () => Sort(byNumberFirst: false));
            Bind(root, "sort-sets", () => Sort(byNumberFirst: true));
            Bind(root, "menu", () => SceneManager.LoadScene(Scenes.MainMenu));
            Bind(root, "results-menu", () => SceneManager.LoadScene(Scenes.MainMenu));

            table.RackTileClicked += ClickRackTile;
            table.RackSlotClicked += ClickRackSlot;
            table.StockClicked += DrawFromStock;
            table.DiscardPileClicked += ClickDiscardPile;
            table.MeldClicked += ClickMeld;
            table.IsPointerBlocked = IsOverHud;

            Match = new Match(new RuleSet(), System.Environment.TickCount);
            Refresh();
        }

        static Button Bind(VisualElement root, string name, System.Action action)
        {
            var button = root.Q<Button>(name);
            button.clicked += action;
            return button;
        }

        bool IsOverHud(Vector2 screenPosition)
        {
            var panel = hud.rootVisualElement.panel;
            var position = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPosition.x, Screen.height - screenPosition.y));
            return panel.Pick(position) != null;
        }

        public void ClickRackTile(Tile tile)
        {
            // Clicking a staged tile breaks its group up again.
            if (staged.RemoveAll(group => group.Contains(tile)) == 0)
            {
                if (!selected.Remove(tile)) selected.Add(tile);
            }
            message = "";
            Refresh();
        }

        public void ClickRackSlot(int slot)
        {
            if (selected.Count != 1 || slots[slot] != null) return;
            slots[System.Array.IndexOf(slots, selected[0])] = null;
            slots[slot] = selected[0];
            selected.Clear();
            Refresh();
        }

        public void DrawFromStock() => Do(Round.DrawFromStock(Human));

        public void ClickDiscardPile(int seat)
        {
            if (seat == Human) DiscardSelected();
            else if (seat == Round.Players - 1) Do(Round.TakeDiscard(Human));
        }

        public void ClickMeld(int index)
        {
            if (selected.Count != 1)
            {
                Say(Localization.Text("msg.selectforadd"));
                return;
            }
            var result = Round.AddToMeld(Human, index, selected[0], atFront: false);
            if (result == MoveResult.InvalidMeld) result = Round.AddToMeld(Human, index, selected[0], atFront: true);
            Do(result);
        }

        /// <summary>Groups the selected tiles: staged for the opening before the player is open, laid down after.</summary>
        public void MeldSelected()
        {
            if (!MeldRules.TryArrange(selected, Round.Okey, Match.Rules, out var arranged, out _))
            {
                Say(Localization.Text("msg.notameld"));
                return;
            }

            if (Round.Opened(Human) != OpenKind.None)
            {
                Do(Round.Lay(Human, arranged));
                return;
            }
            staged.Add(arranged);
            selected.Clear();
            Say("");
        }

        public void OpenStaged()
        {
            var result = Round.Open(Human, staged);
            if (result == MoveResult.Ok) staged.Clear();
            Do(result);
        }

        public void DiscardSelected()
        {
            if (selected.Count != 1) Say(Localization.Text("msg.selectdiscard"));
            else Do(Round.Discard(Human, selected[0]));
        }

        void Unstage()
        {
            staged.Clear();
            Say("");
        }

        void Sort(bool byNumberFirst)
        {
            var okey = Round.Okey;
            var tiles = Round.Rack(Human)
                .OrderBy(t => byNumberFirst ? okey.Resolve(t).Number : (int)okey.Resolve(t).Color)
                .ThenBy(t => byNumberFirst ? (int)okey.Resolve(t).Color : okey.Resolve(t).Number)
                .ToList();
            for (int i = 0; i < slots.Length; i++) slots[i] = i < tiles.Count ? tiles[i] : (Tile?)null;
            Refresh();
        }

        void NextRound()
        {
            if (Match.IsOver)
            {
                SceneManager.LoadScene(Scenes.Game);
                return;
            }
            Match.NextRound();
            System.Array.Clear(slots, 0, slots.Length);
            staged.Clear();
            selected.Clear();
            Say("");
        }

        void Do(MoveResult result)
        {
            if (result == MoveResult.Ok) selected.Clear();
            Say(Describe(result));
        }

        void Say(string text)
        {
            message = text;
            Refresh();
        }

        void Refresh()
        {
            SyncSlots();

            var stagedGroups = new Dictionary<Tile, int>();
            for (int group = 0; group < staged.Count; group++)
            {
                foreach (var tile in staged[group]) stagedGroups[tile] = group;
            }
            table.Render(Round, Human, slots, selected, stagedGroups, names);
            UpdateHud();

            if (Round.Phase == TurnPhase.Finished) Match.ScoreRound();
            else if (Round.Current != Human && bots == null) bots = StartCoroutine(PlayBots());
            ShowResults();
        }

        // Keeps the on-screen rack arrangement in step with the tiles the player actually holds.
        void SyncSlots()
        {
            var rack = Round.Rack(Human);
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && !rack.Contains(slots[i].Value)) slots[i] = null;
            }
            foreach (var tile in rack)
            {
                if (System.Array.IndexOf(slots, tile) < 0) slots[System.Array.IndexOf(slots, (Tile?)null)] = tile;
            }
            selected.RemoveAll(tile => !rack.Contains(tile));
            staged.RemoveAll(group => group.Any(tile => !rack.Contains(tile)));
        }

        IEnumerator PlayBots()
        {
            while (Round.Phase != TurnPhase.Finished && Round.Current != Human)
            {
                if (BotDelay > 0f) yield return new WaitForSeconds(BotDelay);
                else yield return null;
                Bot.PlayTurn(Round, Round.Current);
                Refresh();
            }
            bots = null;
        }

        void UpdateHud()
        {
            bool myTurn = Round.Phase != TurnPhase.Finished && Round.Current == Human;
            bool playing = myTurn && Round.Phase == TurnPhase.Play;
            bool isOpen = Round.Opened(Human) != OpenKind.None;

            string turn = Round.Phase == TurnPhase.Finished ? Localization.Text("turn.over")
                : !myTurn ? Localization.Text("turn.other", names[Round.Current])
                : Round.Phase == TurnPhase.Draw ? Localization.Text("turn.draw", names[Round.Players - 1])
                : Localization.Text("turn.play");
            status.text = message.Length > 0 ? turn + "\n" + message : turn;

            int points = 0;
            int pairs = 0;
            foreach (var group in staged)
            {
                MeldRules.TryEvaluate(group, Round.Okey, Match.Rules, out var info);
                points += info.Points;
                if (info.Kind == MeldKind.Pair) pairs++;
            }
            opening.text = isOpen ? Localization.Text("open.done")
                : Localization.Text("open.need", Round.RequiredPoints, Round.RequiredPairs, points, pairs);
            scores.text = Localization.Text("round.of", Match.RoundNumber, Match.Rules.Rounds) + "    "
                          + string.Join("    ", names.Select((name, seat) => name + " " + Match.Totals[seat]));

            meld.text = Localization.Text(isOpen ? "hud.lay" : "hud.group");
            meld.SetEnabled(playing && selected.Count >= 2);
            open.SetEnabled(playing && !isOpen && staged.Count > 0);
            unstage.SetEnabled(staged.Count > 0);
            discard.SetEnabled(playing && selected.Count == 1);
            giveBack.SetEnabled(playing && Round.HoldsTakenDiscard);
        }

        void ShowResults()
        {
            bool finished = Match.RoundScores != null;
            results.style.display = finished ? DisplayStyle.Flex : DisplayStyle.None;
            if (!finished) return;

            var text = new StringBuilder();
            text.AppendLine(Round.Winner >= 0 ? Localization.Text("results.finished", names[Round.Winner]) : Localization.Text("results.stock"));
            text.AppendLine();
            for (int seat = 0; seat < Round.Players; seat++)
            {
                text.AppendLine(Localization.Text("results.line", names[seat], Match.RoundScores[seat].ToString("+0;-0;0"), Match.Totals[seat]));
            }
            if (Match.IsOver)
            {
                int best = Match.Totals.Min();
                text.AppendLine();
                text.AppendLine(Localization.Text("results.winner", string.Join(", ", names.Where((_, seat) => Match.Totals[seat] == best))));
            }
            resultsText.text = text.ToString();
            next.text = Localization.Text(Match.IsOver ? "hud.newmatch" : "hud.next");
        }

        static string Describe(MoveResult result)
        {
            if (result == MoveResult.Ok) return "";
            string key = "msg." + result;
            string text = Localization.Text(key);
            return text == key ? Localization.Text("msg.notallowed") : text;
        }
    }
}
