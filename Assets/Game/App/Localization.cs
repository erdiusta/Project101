using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project101.App
{
    public enum Language
    {
        English,
        Turkish
    }

    /// <summary>
    /// The game's texts in every supported language. The chosen language is kept in PlayerPrefs,
    /// so it survives between sessions; a first launch follows the device language.
    /// </summary>
    public static class Localization
    {
        const string PrefsKey = "language";

        static Language? current;

        public static Language Current
        {
            get
            {
                if (current == null)
                {
                    var fallback = Application.systemLanguage == SystemLanguage.Turkish ? Language.Turkish : Language.English;
                    int saved = PlayerPrefs.GetInt(PrefsKey, (int)fallback);
                    current = Enum.IsDefined(typeof(Language), saved) ? (Language)saved : fallback;
                }
                return current.Value;
            }
            set
            {
                current = value;
                PlayerPrefs.SetInt(PrefsKey, (int)value);
                PlayerPrefs.Save();
            }
        }

        public static string Text(string key)
        {
            if (!Texts.TryGetValue(key, out var text)) return key;
            return Current == Language.Turkish ? text.tr : text.en;
        }

        public static string Text(string key, params object[] args) => string.Format(Text(key), args);

        /// <summary>
        /// Translates every element under the root whose text is a key written as "#key" in the UXML.
        /// Calling it again after a language change updates the same elements.
        /// </summary>
        public static void Apply(VisualElement root)
        {
            root.Query<TextElement>().ForEach(element =>
            {
                if (!(element.userData is string key))
                {
                    if (string.IsNullOrEmpty(element.text) || element.text[0] != '#') return;
                    key = element.text.Substring(1);
                    element.userData = key;
                }
                element.text = Text(key);
            });
        }

        static readonly Dictionary<string, (string en, string tr)> Texts = new Dictionary<string, (string en, string tr)>
        {
            ["menu.singleplayer"] = ("Single Player", "Tek Oyunculu"),
            ["menu.multiplayer"] = ("Multiplayer", "Çok Oyunculu"),
            ["menu.comingsoon"] = ("Coming soon", "Yakında"),
            ["menu.language"] = ("Language: English", "Dil: Türkçe"),
            ["menu.quit"] = ("Quit", "Çıkış"),

            ["hud.menu"] = ("Menu", "Menü"),
            ["hud.group"] = ("Group", "Grupla"),
            ["hud.lay"] = ("Lay Meld", "Per İndir"),
            ["hud.open"] = ("Open", "El Aç"),
            ["hud.unstage"] = ("Clear Groups", "Grupları Boz"),
            ["hud.discard"] = ("Discard", "Taş At"),
            ["hud.return"] = ("Return Tile", "Taşı Geri Koy"),
            ["hud.sortcolor"] = ("Sort by Color", "Renge Göre Diz"),
            ["hud.sortnumber"] = ("Sort by Number", "Sayıya Göre Diz"),
            ["hud.next"] = ("Next Round", "Sonraki El"),
            ["hud.newmatch"] = ("New Match", "Yeni Oyun"),

            ["seat.0"] = ("You", "Sen"),
            ["seat.1"] = ("East", "Doğu"),
            ["seat.2"] = ("North", "Kuzey"),
            ["seat.3"] = ("West", "Batı"),
            ["seat.open"] = ("(open)", "(açtı)"),
            ["seat.pairs"] = ("(pairs)", "(çift)"),

            ["turn.over"] = ("Round over", "El bitti"),
            ["turn.other"] = ("{0} is playing", "{0} oynuyor"),
            ["turn.draw"] = ("Your turn: click the stock to draw, or {0}'s discard to take it",
                "Sıra sende: ortadan taş çek ya da {0} oyuncusunun attığı taşı al"),
            ["turn.play"] = ("Your turn: meld if you can, then discard", "Sıra sende: açabiliyorsan aç, sonra taş at"),
            ["open.done"] = ("You are open: lay melds or add tiles to the table", "Elin açık: per indir ya da yerdeki perlere taş işle"),
            ["open.need"] = ("Open with {0} points or {1} pairs.  Staged: {2} points, {3} pairs",
                "{0} sayı ya da {1} çift ile aç.  Hazırlanan: {2} sayı, {3} çift"),
            ["round.of"] = ("Round {0} of {1}", "El {0} / {1}"),

            ["results.finished"] = ("{0} finished the round", "{0} eli bitirdi"),
            ["results.stock"] = ("The stock ran out", "Ortadaki taşlar bitti"),
            ["results.line"] = ("{0}:  {1}   (total {2})", "{0}:  {1}   (toplam {2})"),
            ["results.winner"] = ("Match over. Winner: {0}", "Oyun bitti. Kazanan: {0}"),

            ["msg.selectforadd"] = ("Select one tile, then click the meld to add it to.", "Bir taş seç, sonra işleyeceğin pere tıkla."),
            ["msg.notameld"] = ("Those tiles are not a run, a set or a pair.", "Bu taşlar seri, küt ya da çift değil."),
            ["msg.selectdiscard"] = ("Select the one tile to discard.", "Atacağın tek taşı seç."),
            ["msg.NotYourTurn"] = ("It is not your turn.", "Sıra sende değil."),
            ["msg.WrongPhase"] = ("You cannot do that at this point of the turn.", "Bunu şu anda yapamazsın."),
            ["msg.InvalidMeld"] = ("That is not a valid meld.", "Bu geçerli bir per değil."),
            ["msg.MixedOpening"] = ("Open with runs and sets, or with pairs, not both.", "Ya perle ya çiftle aç, ikisi birlikte olmaz."),
            ["msg.OpeningTooLow"] = ("That is not enough to open yet.", "Bu, açmak için henüz yetmiyor."),
            ["msg.NotOpened"] = ("You have to open first.", "Önce elini açmalısın."),
            ["msg.WrongMeldKind"] = ("After opening with pairs you can only lay pairs, otherwise only runs and sets.",
                "Çiftle açtıysan yalnızca çift, perle açtıysan yalnızca per indirebilirsin."),
            ["msg.NoDiscardToTake"] = ("There is no discard to take.", "Alınacak taş yok."),
            ["msg.MustDrawFromStock"] = ("You returned the discard, so draw from the stock.", "Taşı geri koydun, ortadan çekmelisin."),
            ["msg.MustUseTakenTile"] = ("Meld the tile you took, or return it.", "Aldığın taşı kullan ya da geri koy."),
            ["msg.MustKeepDiscard"] = ("Keep one tile to discard.", "Atmak için bir taş bırak."),
            ["msg.notallowed"] = ("That move is not allowed.", "Bu hamle yapılamaz.")
        };
    }
}
