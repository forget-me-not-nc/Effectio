using System.Text;
using UnityEngine;

namespace EffectioDemo
{
    /// <summary>
    /// Tiny IMGUI overlay. Top-left: controls and live HP / status read-out for
    /// every <see cref="CharacterStats"/> in the scene. Bottom-left: colour
    /// legend matching the floor pads.
    /// </summary>
    public class StatusHud : MonoBehaviour
    {
        static readonly StringBuilder _sb = new StringBuilder(512);

        // Must match the colours and order used by DemoBootstrap.
        static readonly (string label, Color color)[] _legend =
        {
            ("Fire (Burning x3 max)",     new Color(1.00f, 0.30f, 0.20f)),
            ("Water (Wet)",               new Color(0.20f, 0.50f, 1.00f)),
            ("Lightning (Charged)",       new Color(1.00f, 0.85f, 0.20f)),
            ("Haste (+50% Speed)",        new Color(0.30f, 0.95f, 0.95f)),
            ("Slow (-50% Speed)",         new Color(0.20f, 0.25f, 0.55f)),
            ("Heal +20 HP",               new Color(0.30f, 1.00f, 0.40f)),
            ("Sanctuary (cleanse)",       new Color(0.95f, 0.95f, 0.95f)),
            ("Poison (DoT + Slow + Weak)",new Color(0.70f, 0.30f, 1.00f)),
            ("Bleed (stack x5)",          new Color(0.55f, 0.10f, 0.10f)),
            ("Adrenaline (HP<30 -> +25)", new Color(1.00f, 0.55f, 0.10f)),
        };

        GUIStyle _boxStyle;
        GUIStyle _legendBoxStyle;

        void OnGUI()
        {
            EnsureStyles();
            DrawCharacterPanel();
            DrawLegend();
        }

        void EnsureStyles()
        {
            if (_boxStyle != null) return;

            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                richText = true,
                fontSize = 14,
                padding = new RectOffset(12, 12, 10, 10),
                normal = { textColor = Color.white },
            };
            _legendBoxStyle = new GUIStyle(_boxStyle) { fontSize = 13 };
        }

        void DrawCharacterPanel()
        {
            var characters = FindObjectsOfType<CharacterStats>();
            var statuses = EffectioWorld.Instance != null ? EffectioWorld.Instance.Manager.Statuses : null;

            _sb.Clear();
            _sb.AppendLine("<b>Controls:</b>  WASD / arrows = walk");
            _sb.AppendLine("<b>Goal:</b>  combine elementals on yourself; reactions fire automatically.");
            _sb.AppendLine();

            foreach (var c in characters)
            {
                if (c.Entity == null) continue;

                int hp = Mathf.CeilToInt(c.Health);
                int max = Mathf.CeilToInt(c.MaxHealth);
                _sb.Append("<b>").Append(c.DisplayName).Append("</b>  HP ")
                   .Append(hp).Append('/').Append(max);

                var keys = c.Entity.ActiveStatusKeys;
                if (keys.Count > 0)
                {
                    _sb.Append("   [ ");
                    bool first = true;
                    foreach (var s in keys)
                    {
                        if (!first) _sb.Append(", ");
                        _sb.Append(s);

                        // Stack count, when > 1, is the readable signal that
                        // stackable statuses (Burning x3, Bleeding x5) are stacking.
                        if (statuses != null)
                        {
                            int stacks = statuses.GetStacks(c.Entity, s);
                            if (stacks > 1) _sb.Append(" x").Append(stacks);
                        }
                        first = false;
                    }
                    _sb.Append(" ]");
                }
                _sb.AppendLine();
            }

            GUI.Box(new Rect(12, 12, 600, 150), _sb.ToString(), _boxStyle);
        }

        void DrawLegend()
        {
            const float swatchSize = 18f;
            const float rowHeight = 24f;
            const float padding = 10f;

            float panelWidth = 300f;
            float panelHeight = padding * 2 + rowHeight * _legend.Length + 22f;
            float y = Screen.height - panelHeight - 12f;

            GUI.Box(new Rect(12, y, panelWidth, panelHeight), "<b>Floor pads</b>", _legendBoxStyle);

            float rowY = y + 28f;
            for (int i = 0; i < _legend.Length; i++)
            {
                var swatchRect = new Rect(12 + padding, rowY + 3, swatchSize, swatchSize);
                var prev = GUI.color;
                GUI.color = _legend[i].color;
                GUI.DrawTexture(swatchRect, Texture2D.whiteTexture);
                GUI.color = prev;

                var labelRect = new Rect(swatchRect.xMax + 8, rowY, panelWidth - swatchSize - 32, rowHeight);
                GUI.Label(labelRect, _legend[i].label);
                rowY += rowHeight;
            }
        }
    }
}

