using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace MoonbaseOmega.Windows;

public class ConfigWindow : Window, IDisposable {
    private static readonly XivChatType[] ChatTypeValues = Enum.GetValues<XivChatType>();
    private static readonly string[] ChatTypeNames = Enum.GetNames<XivChatType>();

    private readonly SpeechManager speechManager;
    private readonly Configuration configuration;

    private int maxInstances;
    private int volume;
    private List<XivChatType> chatTypes;
    private bool changed;

    private int chatTypeSelection = -1;

    public ConfigWindow(SpeechManager speechManager, Configuration configuration) :
        base("Moonbase Omega Config", ImGuiWindowFlags.AlwaysAutoResize) {
        this.speechManager = speechManager;
        this.configuration = configuration;

        this.maxInstances = this.configuration.MaxInstances;
        this.volume = this.configuration.Volume;
        this.chatTypes = this.configuration.ChatTypes!;

        this.configuration.OnConfigurationSaved += this.OnConfigurationSaved;
    }

    public void Dispose() {
        this.configuration.OnConfigurationSaved -= this.OnConfigurationSaved;
        GC.SuppressFinalize(this);
    }

    public override void Draw() {
        if (this.speechManager.DownloadFailed) {
            using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudOrange)) {
                ImGuiComponents.IconButton(FontAwesomeIcon.ExclamationTriangle);
                ImGui.TextWrapped("""
                                  Required files failed to download. This may be an issue with your network connection.
                                  Please try again later (e.g. restart your game in a few hours). If this warning persists, please report this!
                                  """);
            }

            ImGui.Separator();
        }

        if (ImGui.SliderInt("TTS Volume", ref this.volume, 0, 100)) {
            this.RecalculateChanged();
        }

        if (ImGui.SliderInt("Max Instances", ref this.maxInstances, 1, 20)) {
            this.RecalculateChanged();
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(
            "This controls how many messages can play at once. More instances will use more RAM.");

        ImGui.TextUnformatted("Chat channels to play in:");
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

        var disabled = !this.changed;

        using (ImRaii.Disabled(disabled)) {
            if (ImGui.Button("Save")) {
                this.ApplyChanges();
            }
        }

        ImGui.SameLine();

        using (ImRaii.Disabled(disabled)) {
            if (ImGui.Button("Reset")) {
                this.DiscardChanges();
            }
        }
    }

    private void RecalculateChanged() {
        this.changed = this.maxInstances != this.configuration.MaxInstances
                       || this.volume != this.configuration.Volume
                       || !this.chatTypes.SequenceEqual(this.configuration.ChatTypes!);
    }

    private void ApplyChanges() {
        this.configuration.MaxInstances = this.maxInstances;
        this.configuration.Volume = this.volume;
        this.configuration.ChatTypes = this.chatTypes;
        this.configuration.Save();
        this.changed = false;
    }

    private void DiscardChanges() {
        this.maxInstances = this.configuration.MaxInstances;
        this.volume = this.configuration.Volume;
        this.chatTypes = this.configuration.ChatTypes!;
        this.changed = false;
    }

    // Just in case the config is updated somewhere else in the future
    private void OnConfigurationSaved() => this.DiscardChanges();
}
