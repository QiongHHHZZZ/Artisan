namespace Artisan.UI;

using CraftingLogic.Solvers;
using CraftingLists;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using ECommons;
using ECommons.ImGuiMethods;
using OtterGui;
using OtterGui.Filesystem;
using PunishLib.ImGuiMethods;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using UI;
using static global::Artisan.CraftingLogic.Solvers.ExpertSolverProfiles;

internal class ExpertProfileEditor : Window, IDisposable
{
    private static string T(string key) => L10n.Tr(key);
    private static string T(string key, params object[] args) => L10n.Tr(key, args);

    public bool Minimized = false;

    public readonly ExpertProfile profile;
    private string profileName = "";

    public ExpertProfileEditor(int profileId) : base($"{L10n.Tr("Expert Settings Profile Editor")}###{profileId}")
    {
        profile = P.Config.ExpertSolverProfiles.ExpertProfiles.First(x => x.ID == profileId);

        IsOpen = true;
        P.ws.AddWindow(this);
        Size = new Vector2(1000, 600);
        SizeCondition = ImGuiCond.Appearing;
        ShowCloseButton = true;
        RespectCloseHotkey = false;
    }

    public override void PreDraw()
    {
        if (!P.Config.DisableTheme)
        {
            P.Style.Push();
            P.StylePushed = true;
        }
    }

    public override void PostDraw()
    {
        if (P.StylePushed)
        {
            P.Style.Pop();
            P.StylePushed = false;
        }
    }

    public async override void Draw()
    {
        bool changed = false;
        try
        {
            ImGuiEx.TextV(T("Name:"));
            ImGui.SameLine(50f.Scale());

            profileName = profile.Name!;
            if (ImGui.InputText("", ref profileName, 64, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                profile.Name = profileName;
                changed = true;
            }

            ImGui.Dummy(new Vector2(0, 5f));
            if (ImGui.Button(T("{0} (Hold CTRL)", T("Reset to Global Expert Settings"))) && ImGui.GetIO().KeyCtrl)
            {
                profile.Settings = P.Config.ExpertSolverConfig.JSONClone();
                changed = true;
            }
            ImGui.SameLine();
            if (ImGui.Button(T("{0} (Hold CTRL)", T("Reset to Artisan Default Expert Settings"))) && ImGui.GetIO().KeyCtrl)
            {
                profile.Settings = new ExpertSolverSettings();
                changed = true;
            }

            ImGui.Dummy(new Vector2(0, 5f));
            ImGui.Separator();
            ImGui.Dummy(new Vector2(0, 5f));

            changed |= P.PluginUi.ExpertSettingsUI.DrawAllSettings(profile.Settings, true);
        }
        catch (Exception ex)
        {
            ex.Log();
        }

        if (changed)
            P.Config.Save();
    }

    public override void OnClose()
    {
        P.ws.RemoveWindow(this);
    }

    public void Dispose()
    {

    }
}
