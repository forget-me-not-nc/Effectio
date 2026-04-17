using System.Text;
using UnityEngine;

namespace EffectioDemo
{
    /// <summary>
    /// Tiny IMGUI overlay that prints each <see cref="CharacterStats"/>' HP
    /// and active statuses in the top-left corner. No Canvas, no UI prefabs -
    /// the goal is to keep the demo readable without any scene setup.
    /// </summary>
    public class StatusHud : MonoBehaviour
    {
        static readonly StringBuilder _sb = new StringBuilder(512);

        void OnGUI()
        {
            var characters = FindObjectsOfType<CharacterStats>();

            _sb.Clear();
            _sb.AppendLine("<b>Controls:</b>  F=Fire   W=Water   H=Heal");
            _sb.AppendLine("<b>Tip:</b>  Fire + Water on the same target = Vaporize reaction (-40 HP, Stunned)");
            _sb.AppendLine();

            foreach (var c in characters)
            {
                if (c.Entity == null) continue;

                int hp = Mathf.CeilToInt(c.Health);
                int max = Mathf.CeilToInt(c.MaxHealth);
                _sb.Append("<b>").Append(c.DisplayName).Append("</b>  HP ")
                   .Append(hp).Append('/').Append(max);

                var statuses = c.Entity.ActiveStatusKeys;
                if (statuses.Count > 0)
                {
                    _sb.Append("  [ ");
                    bool first = true;
                    foreach (var s in statuses)
                    {
                        if (!first) _sb.Append(", ");
                        _sb.Append(s);
                        first = false;
                    }
                    _sb.Append(" ]");
                }
                _sb.AppendLine();
            }

            var style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                richText = true,
                fontSize = 14,
                padding = new RectOffset(12, 12, 10, 10)
            };
            GUI.Box(new Rect(12, 12, 520, 140), _sb.ToString(), style);
        }
    }
}
