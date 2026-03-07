using Artisan.CraftingLogic.Solvers;
using Artisan.UI.ImGUI;
using Artisan.UI.Tables;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using OtterGui;
using PunishLib.ImGuiMethods;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using static Artisan.CraftingLogic.Solvers.ExpertSolverProfiles;

namespace Artisan.UI
{
    internal class ExpertProfilesUI
    {
        private static string T(string key) => L10n.Tr(key);
        private static string T(string key, params object[] args) => L10n.Tr(key, args);

        internal static ExpertProfile selectedProfile = new();
        public static bool Processing;
        private static readonly ExpertProfileList EPL = new();

        internal static void Draw()
        {
            try
            {
                ImGui.TextWrapped(T("An expert solver profile is a snapshot, or \"loadout\", of specific expert solver settings. Like macros, different profiles can be assigned to specific expert recipes."));

                ImGuiEx.TextWrapped(ImGuiColors.DalamudYellow, T("IMPORTANT: These are not advanced settings or \"expert user\" profiles. They are exclusively for the expert recipe solver."));
                var expertIcon = P.PluginUi.ExpertSettingsUI.expertIcon;
                if (expertIcon != null)
                {
                    ImGuiEx.TextWrapped(ImGuiColors.DalamudYellow, T("Expert recipes have this icon in the crafting log:"));
                    ImGui.SameLine();
                    ImGui.Image(expertIcon.Handle, expertIcon.Size, new Vector2(0, 0), new Vector2(1, 1), new Vector4(0.94f, 0.57f, 0f, 1f));
                }

                ImGui.Dummy(new Vector2(0, 5f));
                if (ImGUIMethods.IconTextButton(Dalamud.Interface.FontAwesomeIcon.ExternalLinkAlt, T("Edit Global Expert Solver Settings")))
                    P.PluginUi.OpenWindow = OpenWindow.Main;

                ImGui.Dummy(new Vector2(0, 10f));
                ImGui.TextWrapped(T("Left click a profile to edit. Right click a profile to select it without editing."));

                ImGui.Dummy(new Vector2(0, 5f));
                ImGui.Separator();
                ImGui.Dummy(new Vector2(0, 5f));

                EPL.Draw(ImGui.GetContentRegionAvail().X);

                ImGui.Spacing();
            }
            catch (Exception ex)
            {
                ex.Log();
            }
        }
    }

    internal class ExpertProfileList
    {
        private static string T(string key) => L10n.Tr(key);
        private static string T(string key, params object[] args) => L10n.Tr(key, args);

        private const string AddPopupId = "##ExpertProfileAdd";
        private const string DuplicatePopupId = "##ExpertProfileDuplicate";
        private string _filterInput = string.Empty;
        private string _newProfileInput = string.Empty;
        private bool _focusNamePopup;
        private int _duplicateSourceIdx = -1;
        private int _currentIdx = -1;

        private List<ExpertProfile> Profiles => P.Config.ExpertSolverProfiles.ExpertProfiles;

        public void Draw(float width)
        {
            SyncCurrentSelection();
            DrawLocalizedFilter(width);

            var childHeight = Math.Max(ImGui.GetTextLineHeightWithSpacing() * 4, ImGui.GetContentRegionAvail().Y - ImGui.GetFrameHeightWithSpacing());
            ImGui.BeginChild("ProfileSelector", new Vector2(width, childHeight), true);
            foreach (var idx in Enumerable.Range(0, Profiles.Count).Where(idx => !IsFiltered(idx)))
                DrawProfileRow(idx);
            ImGui.EndChild();

            DrawLocalizedButtons(width);
            DrawAddPopup();
            DrawDuplicatePopup();
        }

        private void SyncCurrentSelection()
        {
            if (Profiles.Count == 0)
            {
                _currentIdx = -1;
                return;
            }

            if (ExpertProfilesUI.selectedProfile.ID != 0)
            {
                var selectedIdx = Profiles.FindIndex(x => x.ID == ExpertProfilesUI.selectedProfile.ID);
                if (selectedIdx >= 0)
                    _currentIdx = selectedIdx;
            }

            if (_currentIdx >= Profiles.Count)
                _currentIdx = Profiles.Count - 1;
        }

        private bool IsFiltered(int idx)
            => _filterInput.Length != 0 && !Profiles[idx].Name.Contains(_filterInput, StringComparison.InvariantCultureIgnoreCase);

        private void DrawLocalizedFilter(float width)
        {
            var newFilter = _filterInput;
            ImGui.SetNextItemWidth(width);
            var enterPressed = ImGui.InputTextWithHint("##ExpertProfilesFilter", T("Filter..."), ref newFilter, 128, ImGuiInputTextFlags.EnterReturnsTrue);
            if (newFilter != _filterInput)
                _filterInput = newFilter;

            if (!enterPressed)
                return;

            var firstVisible = Enumerable.Range(0, Profiles.Count).FirstOrDefault(idx => !IsFiltered(idx));
            if (Profiles.Count > 0 && !IsFiltered(firstVisible))
                _currentIdx = firstVisible;
        }

        private void DrawLocalizedButtons(float width)
        {
            var buttonWidth = width / 3f;

            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                if (ImGui.Button(FontAwesomeIcon.Plus.ToIconString(), Vector2.UnitX * buttonWidth))
                {
                    _newProfileInput = string.Empty;
                    _focusNamePopup = true;
                    ImGui.OpenPopup(AddPopupId);
                }
            }
            ImGuiUtil.HoverTooltip(T("Add new profile"));

            ImGui.SameLine();

            using (ImRaii.Disabled(_currentIdx < 0))
            {
                using (ImRaii.PushFont(UiBuilder.IconFont))
                {
                    if (ImGui.Button(FontAwesomeIcon.Clone.ToIconString(), Vector2.UnitX * buttonWidth))
                    {
                        _duplicateSourceIdx = _currentIdx;
                        _newProfileInput = string.Empty;
                        _focusNamePopup = true;
                        ImGui.OpenPopup(DuplicatePopupId);
                    }
                }
            }
            ImGuiUtil.HoverTooltip(T("Duplicate Current Selection"));

            ImGui.SameLine();

            var canDelete = _currentIdx >= 0 && ImGui.GetIO().KeyCtrl;
            using (ImRaii.Disabled(!canDelete))
            {
                using (ImRaii.PushFont(UiBuilder.IconFont))
                {
                    if (ImGui.Button(FontAwesomeIcon.Trash.ToIconString(), Vector2.UnitX * buttonWidth) && DeleteCurrent())
                    {
                        if (_currentIdx >= Profiles.Count)
                            _currentIdx = Profiles.Count - 1;
                    }
                }
            }
            ImGuiUtil.HoverTooltip(T("Permanently delete selected profile\r\n(hold Ctrl to confirm)"));
        }

        private void DrawAddPopup()
        {
            if (!ImGui.BeginPopup(AddPopupId))
                return;

            if (_focusNamePopup)
            {
                ImGui.SetKeyboardFocusHere();
                _focusNamePopup = false;
            }

            ImGui.SetNextItemWidth(Math.Max(280f.Scale(), ImGui.GetContentRegionAvail().X));
            var submitted = ImGui.InputTextWithHint("##ExpertProfilesAddInput", T("Enter New Name..."), ref _newProfileInput, 100, ImGuiInputTextFlags.EnterReturnsTrue);
            var trimmedName = _newProfileInput.Trim();
            if (submitted && trimmedName.Length > 0 && AddProfile(trimmedName))
            {
                _newProfileInput = string.Empty;
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }

        private void DrawDuplicatePopup()
        {
            if (!ImGui.BeginPopup(DuplicatePopupId))
                return;

            if (_focusNamePopup)
            {
                ImGui.SetKeyboardFocusHere();
                _focusNamePopup = false;
            }

            ImGui.SetNextItemWidth(Math.Max(280f.Scale(), ImGui.GetContentRegionAvail().X));
            var submitted = ImGui.InputTextWithHint("##ExpertProfilesDuplicateInput", T("Enter New Name..."), ref _newProfileInput, 100, ImGuiInputTextFlags.EnterReturnsTrue);
            var trimmedName = _newProfileInput.Trim();
            if (submitted && trimmedName.Length > 0 && DuplicateCurrent(trimmedName))
            {
                _newProfileInput = string.Empty;
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }

        private void DrawProfileRow(int idx)
        {
            var profile = Profiles[idx];
            using var id = ImRaii.PushId(profile.ID);
            using var disabled = ImRaii.Disabled(ExpertProfilesUI.Processing && ExpertProfilesUI.selectedProfile.ID == profile.ID);

            var selected = ImGui.Selectable(T("{0} (ID: {1})", profile.Name, profile.ID), idx == _currentIdx);
            if (selected)
            {
                _currentIdx = idx;
                if (!ExpertProfilesUI.Processing)
                    ExpertProfilesUI.selectedProfile = profile;

                OpenEditor(profile.ID);
            }

            if (!ExpertProfilesUI.Processing && ImGui.IsItemClicked(ImGuiMouseButton.Right))
            {
                if (_currentIdx == idx)
                {
                    _currentIdx = -1;
                    ExpertProfilesUI.selectedProfile = new ExpertProfile();
                }
                else
                {
                    _currentIdx = idx;
                    ExpertProfilesUI.selectedProfile = profile;
                }
            }
        }

        private static void OpenEditor(int profileId)
        {
            if (!P.ws.Windows.Any(x => x.WindowName.Contains(profileId.ToString())))
            {
                Interface.SetupValues();
                _ = new ExpertProfileEditor(profileId);
                return;
            }

            P.ws.Windows.TryGetFirst(x => x.WindowName.Contains(profileId.ToString()), out var window);
            window?.BringToFront();
        }

        private bool AddProfile(string name)
        {
            try
            {
                var profile = new ExpertProfile { Name = name, Settings = new ExpertSolverSettings() };
                P.Config.ExpertSolverProfiles.AddNewExpertProfile(profile);
                P.Config.Save();
                _currentIdx = Profiles.Count - 1;
                if (!ExpertProfilesUI.Processing)
                    ExpertProfilesUI.selectedProfile = profile;
                return true;
            }
            catch (Exception ex)
            {
                ex.Log();
                return false;
            }
        }

        private bool DuplicateCurrent(string name)
        {
            if (_duplicateSourceIdx < 0 || _duplicateSourceIdx >= Profiles.Count)
                return false;

            var baseProfile = Profiles[_duplicateSourceIdx];
            var newProfile = baseProfile.JSONClone();
            newProfile.Name = name;
            P.Config.ExpertSolverProfiles.AddNewExpertProfile(newProfile);
            P.Config.Save();
            _currentIdx = Profiles.Count - 1;
            if (!ExpertProfilesUI.Processing)
                ExpertProfilesUI.selectedProfile = newProfile;
            return true;
        }

        private bool DeleteCurrent()
        {
            if (_currentIdx < 0 || _currentIdx >= Profiles.Count)
                return false;

            var profile = Profiles[_currentIdx];
            if (P.ws.Windows.TryGetFirst(x => x.WindowName.Contains(profile.ID.ToString()) && x.GetType() == typeof(ExpertProfileEditor), out var window))
                P.ws.RemoveWindow(window);

            Profiles.RemoveAt(_currentIdx);
            P.Config.Save();

            if (!ExpertProfilesUI.Processing)
                ExpertProfilesUI.selectedProfile = new ExpertProfile();

            return true;
        }
    }
}
