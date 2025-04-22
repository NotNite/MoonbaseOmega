using System;
using System.Linq;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace MoonbaseOmega.Windows;

public class ConfigWindow(Configuration config, Action updateVolume)
    : Window("Moonbase Omega Config", ImGuiWindowFlags.AlwaysAutoResize) {
    private readonly XivChatType[] entries = Enum.GetValues<XivChatType>();
    private readonly string[] names = Enum.GetNames<XivChatType>();
    private int current = -1;

    public override void Draw() {
        var changed = false;

        if (ImGui.SliderInt("TTS volume", ref config.Volume, 0, 100)) {
            updateVolume();
            changed = true;
        }

        ImGui.TextUnformatted("Chat types to play in:");
        ImGui.Combo("##ChatType", ref this.current, this.names, this.names.Length);
        ImGui.SameLine();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Plus)) {
            var entry = this.entries[this.current];
            if (!config.ChatTypes!.Contains(entry)) {
                config.ChatTypes.Add(entry);
                changed = true;
            }
            this.current = -1;
        }

        using (ImRaii.Table("Chat Types List", 2, ImGuiTableFlags.SizingFixedFit)) {
            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.NoHeaderLabel);
            ImGui.TableSetupColumn("Type");
            ImGui.TableHeadersRow();

            foreach (var chatType in config.ChatTypes!.ToList()) {
                using (ImRaii.PushId((int) chatType)) {
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash)) {
                        config.ChatTypes!.Remove(chatType);
                        changed = true;
                    }

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(Enum.GetName(chatType));
                }
            }
        }

        if (changed) config.Save();
    }
}
