using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace MoonbaseOmega.Windows;

public class ConfigWindow : Window, IDisposable {
    private static readonly XivChatType[] ChatTypeValues = Enum.GetValues<XivChatType>();
    private static readonly string[] ChatTypeNames = Enum.GetNames<XivChatType>();

    private readonly SpeechManager speechManager;
    private readonly Configuration configuration;

    // Handles "dirty" config state so it's possible to apply/discard changes
    private int maxInstances;
    private int volume;
    private List<XivChatType> chatTypes;
    private bool autoDeleteLogFile;
    private bool changed;

    private int chatTypeSelection = -1;

    public ConfigWindow(SpeechManager speechManager, Configuration configuration) :
        base("Moonbase Omega Config", ImGuiWindowFlags.AlwaysAutoResize) {
        this.speechManager = speechManager;
        this.configuration = configuration;

        this.maxInstances = this.configuration.MaxInstances;
        this.volume = this.configuration.Volume;
        this.chatTypes = this.configuration.ChatTypes!.ToList();
        this.autoDeleteLogFile = this.configuration.AutoDeleteLogFile;

        this.configuration.OnConfigurationSaved += this.OnConfigurationSaved;
    }

    public void Dispose() {
        this.configuration.OnConfigurationSaved -= this.OnConfigurationSaved;
        GC.SuppressFinalize(this);
    }

    public override void Draw() {
        if (this.speechManager.DownloadFailed) {
            WarningText("""
                        Required files failed to download. This may be an issue with your network connection.
                        Please try again later (e.g. restart your game in a few hours). If this warning persists, please report this!
                        """);

            ImGui.Separator();
        }

        if (ImGui.SliderInt("TTS Volume", ref this.volume, 0, 100)) {
            this.RecalculateChanged();
        }
        ImGuiComponents.HelpMarker(
            "This will not update the volume of already speaking instances, so make sure to hit the stop button after saving.");

        if (ImGui.SliderInt("Max Instances", ref this.maxInstances, 1, 20)) {
            this.RecalculateChanged();
        }
        ImGuiComponents.HelpMarker(
            "This controls how many messages can play at once. More instances will use more RAM.");

        if (ImGui.Checkbox("Auto Delete Log File", ref this.autoDeleteLogFile)) {
            this.RecalculateChanged();
        }
        ImGuiComponents.HelpMarker("""
                                   DECtalk creates a log file on the root of your drive (named "dtdic.log").
                                   Unfortunately, it isn't possible to disable this log file, as DECtalk is ancient crusty software from the 80s.
                                   When enabled, Moonbase Omega will automatically delete this log file for you when the plugin unloads. Sorry about the mess!
                                   """);

        if (ImGui.Button("Force Stop")) {
            Task.Run(() => this.speechManager.ResetAll());
        }

        ImGui.Separator();
        ImGui.TextUnformatted("Chat channels:");

        ImGui.Combo("##ChatType", ref this.chatTypeSelection, ChatTypeNames, ChatTypeNames.Length);
        ImGui.SameLine();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Plus)) {
            var chatType = ChatTypeValues[this.chatTypeSelection];
            if (!this.chatTypes.Contains(chatType)) {
                this.chatTypes.Add(chatType);
                this.RecalculateChanged();
            }
        }

        using (ImRaii.Table("Chat Channels List", 2, ImGuiTableFlags.SizingFixedFit)) {
            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.NoHeaderLabel);
            ImGui.TableSetupColumn("Channel");
            ImGui.TableHeadersRow();

            foreach (var chatType in this.chatTypes.ToList()) {
                using (ImRaii.PushId((int) chatType)) {
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash)) {
                        this.chatTypes.Remove(chatType);
                        this.RecalculateChanged();
                    }

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(Enum.GetName(chatType));
                }
            }
        }

        ImGui.Separator();

        var configDisabled = !this.changed;
        if (!configDisabled) WarningText("You have unsaved changes.");

        using (ImRaii.Disabled(configDisabled)) {
            if (ImGui.Button("Save")) {
                this.ApplyChanges();
            }
        }

        ImGui.SameLine();

        using (ImRaii.Disabled(configDisabled)) {
            if (ImGui.Button("Reset")) {
                this.DiscardChanges();
            }
        }
    }

    private void RecalculateChanged() {
        this.changed = this.maxInstances != this.configuration.MaxInstances
                       || this.volume != this.configuration.Volume
                       || !this.chatTypes.SequenceEqual(this.configuration.ChatTypes!)
                       || this.autoDeleteLogFile != this.configuration.AutoDeleteLogFile;
    }

    private void ApplyChanges() {
        this.configuration.MaxInstances = this.maxInstances;
        this.configuration.Volume = this.volume;
        this.configuration.ChatTypes = this.chatTypes.ToList();
        this.configuration.AutoDeleteLogFile = this.autoDeleteLogFile;
        this.configuration.Save();
        this.changed = false;
    }

    private void DiscardChanges() {
        this.maxInstances = this.configuration.MaxInstances;
        this.volume = this.configuration.Volume;
        this.chatTypes = this.configuration.ChatTypes!.ToList();
        this.autoDeleteLogFile = this.configuration.AutoDeleteLogFile;
        this.changed = false;
    }

    // Just in case the config is updated somewhere else in the future
    private void OnConfigurationSaved() => this.DiscardChanges();

    private static void WarningText(string text) {
        using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudOrange)) {
            using (Services.PluginInterface.UiBuilder.IconFontHandle.Push()) {
                ImGui.TextUnformatted(FontAwesomeIcon.ExclamationTriangle.ToIconString());
            }
            ImGui.SameLine();
            ImGui.TextWrapped(text);
        }
    }
}
