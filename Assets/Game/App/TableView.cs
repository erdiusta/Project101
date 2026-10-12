using System;
using System.Collections.Generic;
using Project101.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Project101.App
{
    public enum TargetKind
    {
        RackTile,
        RackBoard,
        Stock,
        DiscardPile,
        Meld
    }

    /// <summary>Marks a placeholder object as something the player can click.</summary>
    public sealed class TableTarget : MonoBehaviour
    {
        public TargetKind Kind;
        public int Index;
        public Tile Tile;
    }

    /// <summary>
    /// Draws the table out of primitives and reports clicks on it. It holds no game state:
    /// everything is rebuilt from the round on each <see cref="Render"/>.
    /// </summary>
    public sealed class TableView : MonoBehaviour
    {
        public const int RackColumns = 16;
        public const int RackSlots = RackColumns * 2;

        const float SlotWidth = 0.64f;
        static readonly Vector3 TileSize = new Vector3(0.56f, 0.12f, 0.8f);

        static readonly Color Felt = new Color(0.08f, 0.33f, 0.22f);
        static readonly Color Wood = new Color(0.45f, 0.29f, 0.16f);
        static readonly Color Ivory = new Color(0.96f, 0.93f, 0.84f);
        static readonly Color Gold = new Color(1f, 0.85f, 0.4f);
        static readonly Color Selected = new Color(0.55f, 0.85f, 1f);
        static readonly Color Back = new Color(0.72f, 0.66f, 0.55f);
        static readonly Color Pad = new Color(0.05f, 0.22f, 0.15f);
        static readonly Color[] StagedTints =
        {
            new Color(0.7f, 0.95f, 0.7f), new Color(0.95f, 0.75f, 0.95f), new Color(0.95f, 0.8f, 0.6f),
            new Color(0.75f, 0.8f, 1f), new Color(0.85f, 0.95f, 0.6f)
        };
        static readonly Color[] Inks =
        {
            new Color(0.85f, 0.55f, 0f), new Color(0.1f, 0.4f, 0.85f), new Color(0.1f, 0.1f, 0.1f), new Color(0.8f, 0.1f, 0.1f)
        };

        // Where each seat throws its discards: the corner to the player's right.
        static readonly Vector3[] DiscardSpots =
        {
            new Vector3(6.3f, 0f, -4.4f), new Vector3(6.3f, 0f, 4.4f), new Vector3(-6.3f, 0f, 4.4f), new Vector3(-6.3f, 0f, -4.4f)
        };

        [SerializeField] Material baseMaterial;
        [SerializeField] Camera viewCamera;

        public event Action<Tile> RackTileClicked;
        public event Action<int> RackSlotClicked;
        public event Action StockClicked;
        public event Action<int> DiscardPileClicked;
        public event Action<int> MeldClicked;

        /// <summary>Set by the HUD so clicks on buttons do not reach the table underneath.</summary>
        public Func<Vector2, bool> IsPointerBlocked;

        readonly List<GameObject> dynamicObjects = new List<GameObject>();
        readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
        readonly TextMesh[] seatLabels = new TextMesh[Round.Players];
        Transform rack;
        Font font;

        void Awake()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            Box("Table", transform, new Vector3(0f, -0.1f, 0f), new Vector3(17f, 0.2f, 17f), Felt);

            // The player's rack leans back so its two rows face the camera.
            rack = new GameObject("Rack").transform;
            rack.SetParent(transform, false);
            rack.localPosition = new Vector3(0f, 0.5f, -6.7f);
            rack.localRotation = Quaternion.Euler(-25f, 0f, 0f);
            var board = Box("Board", rack, new Vector3(0f, -0.08f, 0f), new Vector3(RackColumns * SlotWidth + 0.4f, 0.15f, 2.1f), Wood);
            board.AddComponent<TableTarget>().Kind = TargetKind.RackBoard;

            Box("Rack 1", transform, new Vector3(7.7f, 0.25f, 0f), new Vector3(0.5f, 0.5f, 9f), Wood);
            Box("Rack 2", transform, new Vector3(0f, 0.25f, 7.7f), new Vector3(9f, 0.5f, 0.5f), Wood);
            Box("Rack 3", transform, new Vector3(-7.7f, 0.25f, 0f), new Vector3(0.5f, 0.5f, 9f), Wood);

            seatLabels[0] = Label(transform, new Vector3(0f, 0.02f, -5.1f), 0.45f, "", Color.white);
            seatLabels[1] = Label(transform, new Vector3(6.3f, 0.02f, 0f), 0.5f, "", Color.white);
            seatLabels[2] = Label(transform, new Vector3(3.2f, 0.02f, 5.4f), 0.5f, "", Color.white);
            seatLabels[3] = Label(transform, new Vector3(-6.3f, 0.02f, 0f), 0.5f, "", Color.white);
            seatLabels[1].transform.localRotation = Quaternion.Euler(90f, -90f, 0f);
            seatLabels[3].transform.localRotation = Quaternion.Euler(90f, 90f, 0f);

            for (int seat = 0; seat < Round.Players; seat++)
            {
                var pad = Box("Discard " + seat, transform, DiscardSpots[seat] + Vector3.up * 0.01f, new Vector3(0.9f, 0.02f, 1.1f), Pad);
                var target = pad.AddComponent<TableTarget>();
                target.Kind = TargetKind.DiscardPile;
                target.Index = seat;
            }
        }

        void Update()
        {
            var pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame) return;

            Vector2 position = pointer.position.ReadValue();
            if (IsPointerBlocked != null && IsPointerBlocked(position)) return;
            if (!Physics.Raycast(viewCamera.ScreenPointToRay(position), out var hit, 100f)) return;

            var target = hit.collider.GetComponent<TableTarget>();
            if (target == null) return;

            switch (target.Kind)
            {
                case TargetKind.RackTile:
                    RackTileClicked?.Invoke(target.Tile);
                    break;
                case TargetKind.RackBoard:
                    Vector3 local = rack.InverseTransformPoint(hit.point);
                    int column = Mathf.Clamp(Mathf.RoundToInt(local.x / SlotWidth + (RackColumns - 1) / 2f), 0, RackColumns - 1);
                    RackSlotClicked?.Invoke((local.z > 0f ? 0 : RackColumns) + column);
                    break;
                case TargetKind.Stock:
                    StockClicked?.Invoke();
                    break;
                case TargetKind.DiscardPile:
                    DiscardPileClicked?.Invoke(target.Index);
                    break;
                case TargetKind.Meld:
                    MeldClicked?.Invoke(target.Index);
                    break;
            }
        }

        /// <param name="slots">The viewer's rack as arranged on screen, one entry per slot.</param>
        /// <param name="staged">Rack tiles already grouped for an opening, with their group number.</param>
        public void Render(Round round, int viewer, IReadOnlyList<Tile?> slots, ICollection<Tile> selected,
            IReadOnlyDictionary<Tile, int> staged, IReadOnlyList<string> names)
        {
            foreach (var old in dynamicObjects) Destroy(old);
            dynamicObjects.Clear();
            var okey = round.Okey;

            for (int slot = 0; slot < slots.Count; slot++)
            {
                if (slots[slot] == null) continue;
                var tile = slots[slot].Value;
                bool lifted = selected.Contains(tile);
                var position = new Vector3((slot % RackColumns - (RackColumns - 1) / 2f) * SlotWidth, lifted ? 0.3f : 0.06f,
                    slot < RackColumns ? 0.5f : -0.5f);

                Color body = lifted ? Selected
                    : staged.TryGetValue(tile, out int group) ? StagedTints[group % StagedTints.Length]
                    : okey.IsWild(tile) ? Gold : Ivory;
                Target(FaceTile(rack, position, 1f, tile, body), TargetKind.RackTile, 0, tile);
            }

            for (int seat = 0; seat < Round.Players; seat++)
            {
                int count = round.Rack(seat).Count;
                string state = round.Opened(seat) == OpenKind.None ? ""
                    : "  " + Localization.Text(round.Opened(seat) == OpenKind.Pairs ? "seat.pairs" : "seat.open");
                seatLabels[seat].text = names[seat] + "  " + count + state;
                seatLabels[seat].color = seat == round.Current && round.Phase != TurnPhase.Finished ? Gold : Color.white;
                if (seat == viewer) continue;

                // Opponents show only the backs of their tiles.
                for (int i = 0; i < count; i++)
                {
                    float along = (i - (count - 1) / 2f) * 0.37f;
                    var position = seat == 2 ? new Vector3(along, 0.04f, 6.9f) : new Vector3(seat == 1 ? 6.9f : -6.9f, 0.04f, along);
                    var back = Block(transform, position, 0.6f, Back);
                    if (seat != 2) back.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                }
            }

            if (round.StockCount > 0)
            {
                // The pile gets shorter as it is drawn down.
                float layers = 1f + round.StockCount * 0.25f;
                var stock = Block(transform, new Vector3(0.45f, TileSize.y * layers / 2f, 5.3f), 1f, Back);
                stock.transform.localScale = new Vector3(1f, layers, 1f);
                Target(stock, TargetKind.Stock, 0, default);
                Track(Label(transform, new Vector3(1.25f, 0.02f, 5.3f), 0.45f, round.StockCount.ToString(), Color.white).gameObject);
            }
            FaceTile(transform, new Vector3(-0.45f, 0.06f, 5.3f), 1f, round.Indicator, Ivory);

            for (int seat = 0; seat < Round.Players; seat++)
            {
                var pile = round.DiscardPile(seat);
                if (pile.Count == 0) continue;
                var top = pile[pile.Count - 1];
                Target(FaceTile(transform, DiscardSpots[seat] + Vector3.up * 0.08f, 1f, top, okey.IsWild(top) ? Gold : Ivory),
                    TargetKind.DiscardPile, seat, top);
            }

            // Melds flow across the middle of the table, left to right, then down.
            const float scale = 0.95f;
            float step = TileSize.x * scale + 0.03f;
            float x = -5.3f;
            float z = 3.9f;
            for (int m = 0; m < round.Table.Count; m++)
            {
                var tiles = round.Table[m].Tiles;
                if (x + tiles.Count * step > 5.3f)
                {
                    x = -5.3f;
                    z -= TileSize.z * scale + 0.12f;
                }
                foreach (var tile in tiles)
                {
                    Target(FaceTile(transform, new Vector3(x + step / 2f, 0.05f, z), scale, tile, okey.IsWild(tile) ? Gold : Ivory),
                        TargetKind.Meld, m, tile);
                    x += step;
                }
                x += 0.3f;
            }
        }

        GameObject FaceTile(Transform parent, Vector3 position, float scale, Tile tile, Color body)
        {
            var block = Block(parent, position, scale, body);
            string text = tile.IsFalseJoker ? "J" : tile.Number.ToString();
            Color ink = tile.IsFalseJoker ? new Color(0.4f, 0.2f, 0.6f) : Inks[(int)tile.Color];
            Label(block.transform, new Vector3(0f, TileSize.y / 2f + 0.01f, 0f), 0.42f, text, ink);
            return block;
        }

        // An empty parent keeps the uniform scale so labels are not stretched by the tile's proportions.
        GameObject Block(Transform parent, Vector3 position, float scale, Color color)
        {
            var root = new GameObject("Tile");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            root.transform.localScale = Vector3.one * scale;
            Box("Body", root.transform, Vector3.zero, TileSize, color);
            return Track(root);
        }

        GameObject Track(GameObject dynamicObject)
        {
            dynamicObjects.Add(dynamicObject);
            return dynamicObject;
        }

        static void Target(GameObject block, TargetKind kind, int index, Tile tile)
        {
            var target = block.transform.GetChild(0).gameObject.AddComponent<TableTarget>();
            target.Kind = kind;
            target.Index = index;
            target.Tile = tile;
        }

        GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Color color)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial = MaterialFor(color);
            return box;
        }

        Material MaterialFor(Color color)
        {
            if (!materials.TryGetValue(color, out var material))
            {
                material = new Material(baseMaterial) { color = color };
                materials.Add(color, material);
            }
            return material;
        }

        // Flat text lying on the table, readable from the camera side.
        TextMesh Label(Transform parent, Vector3 position, float height, string text, Color color)
        {
            var label = new GameObject("Label").AddComponent<TextMesh>();
            label.transform.SetParent(parent, false);
            label.transform.localPosition = position;
            label.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            label.font = font;
            label.GetComponent<Renderer>().sharedMaterial = font.material;
            label.fontSize = 64;
            label.characterSize = height * 10f / 64f;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontStyle = FontStyle.Bold;
            label.text = text;
            label.color = color;
            return label;
        }
    }
}
