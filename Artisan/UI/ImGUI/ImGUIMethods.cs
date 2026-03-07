using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using System;
using System.Numerics;

namespace Artisan.UI.ImGUI
{
    internal static class ImGUIMethods
    {
        private static Vector2 GetIconSize(FontAwesomeIcon icon)
        {
            ImGui.PushFont(UiBuilder.IconFont);
            var iconSize = ImGui.CalcTextSize(icon.ToIconString());
            ImGui.PopFont();
            return iconSize;
        }

        public static bool IconTextButton(FontAwesomeIcon icon, string text, Vector2 size = new(), bool iconOnRight = false)
        {
            var iconSize = GetIconSize(icon);
            var textSize = ImGui.CalcTextSize(text);
            var padding = ImGui.GetStyle().FramePadding;
            var spacing = ImGui.GetStyle().ItemInnerSpacing;

            var buttonSizeX = iconSize.X + textSize.X + padding.X * 2 + spacing.X;
            var buttonSizeY = MathF.Max(iconSize.Y, textSize.Y) + padding.Y * 2;
            var buttonSize = size == Vector2.Zero ? new Vector2(buttonSizeX, buttonSizeY) : size;

            var buttonClicked = ImGui.Button("###" + icon.ToIconString() + text, buttonSize);

            var restorePos = ImGui.GetCursorScreenPos();
            var buttonMin = ImGui.GetItemRectMin();
            var contentWidth = iconSize.X + spacing.X + textSize.X;
            var contentStartX = buttonMin.X + MathF.Max(padding.X, (buttonSize.X - contentWidth) * 0.5f);
            var iconPosY = buttonMin.Y + (buttonSize.Y - iconSize.Y) * 0.5f;
            var textPosY = buttonMin.Y + (buttonSize.Y - textSize.Y) * 0.5f;

            var textPosX = iconOnRight ? contentStartX : contentStartX + iconSize.X + spacing.X;
            var iconPosX = iconOnRight ? textPosX + textSize.X + spacing.X : contentStartX;

            ImGui.SetCursorScreenPos(new Vector2(iconPosX, iconPosY));
            ImGui.PushFont(UiBuilder.IconFont);
            ImGui.TextUnformatted(icon.ToIconString());
            ImGui.PopFont();

            ImGui.SetCursorScreenPos(new Vector2(textPosX, textPosY));
            ImGui.TextUnformatted(text);

            ImGui.SetCursorScreenPos(restorePos);
            return buttonClicked;
        }

        public static bool FlippedInputInt(string id, ref int v)
        {
            ImGui.Text(id);
            ImGui.SameLine();
            return ImGui.InputInt($"###{id}", ref v, 0, 0);
        }

        public static bool FlippedCheckbox(string label, ref bool v)
        {
            ImGui.Text(label);
            ImGui.SameLine();
            return ImGui.Checkbox($"###{label}", ref v);
        }

        public static bool SliderInt(string label, ref int v, int v_min, int v_max, bool leftLabel = false)
        {
            if (leftLabel)
            {
                ImGui.Text($"{label}");
                ImGui.SameLine();
            }

            var ret = ImGui.SliderInt(leftLabel ? $"###{label}" : label, ref v, v_min, v_max);
            return ret;
        }

        public static bool InputIntBound(string label, ref int v, int v_min, int v_max, bool leftLabel = false)
        {
            if (leftLabel)
            {
                ImGui.Text($"{label}");
                ImGui.SameLine();
            }

            var ret = ImGui.InputInt(leftLabel ? $"###{label}" : label, ref v, 0, 0);
            if (v < v_min)
                v = v_min;

            if (v > v_max)
                v = v_max;

            return ret;
        }
    }
}
