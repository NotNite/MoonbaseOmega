using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using MoonbaseOmega.Windows;
using Dalamud.Game.Command;
using Dalamud.Game.Config;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Lumina.Text.Payloads;
using Lumina.Text.ReadOnly;

namespace MoonbaseOmega;

public class Plugin : IDalamudPlugin {
    private const ushort Sinus_Ardorum = 1237; // ffxiv/cos_c1/hou/c1w1/level/c1w1
    private const ushort Phaenna = 1291;
    private const string CommandName = "/moonbaseomega";

    private readonly WindowSystem windowSystem = new("MoonbaseOmega");
    private readonly Configuration configuration;
    private readonly ConfigWindow configWindow;
    private readonly SpeechManager speechManager;

    public Plugin(IDalamudPluginInterface pluginInterface) {
        pluginInterface.Create<Services>();

        this.configuration = Services.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        // for some reason the defaults duplicate, wtf newtonsoft?
        this.configuration.ChatTypes ??= [
            XivChatType.Say,
            XivChatType.Yell,
            XivChatType.Shout
        ];

        var outputDir = Path.Combine(
            Services.PluginInterface.ConfigDirectory.FullName,
            "DECtalk"
        );
        if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

        this.speechManager = new SpeechManager(outputDir, this.ComputeVolume(), this.configuration.MaxInstances);

        this.configWindow = new ConfigWindow(this.speechManager, this.configuration);
        this.windowSystem.AddWindow(this.configWindow);

        Services.PluginInterface.UiBuilder.Draw += this.DrawUi;
        Services.PluginInterface.UiBuilder.OpenConfigUi += this.ToggleConfigUi;
        Services.CommandManager.AddHandler(CommandName, new CommandInfo(this.OnCommand) {
            HelpMessage = "Open the config window"
        });

        Services.ChatGui.ChatMessage += this.ChatMessage;
        Services.GameConfig.SystemChanged += this.SystemChanged;

        this.configuration.OnConfigurationSaved += this.OnConfigurationSaved;
    }

    public void Dispose() {
        if (this.configuration.AutoDeleteLogFile) {
            try {
                TextToSpeech.TextToSpeech.DeleteLogFile();
                Services.PluginLog.Debug("Deleted log file");
            } catch (Exception e) {
                Services.PluginLog.Warning(e, "Failed to delete log file");
            }
        }

        this.configuration.OnConfigurationSaved -= this.OnConfigurationSaved;

        Services.GameConfig.SystemChanged -= this.SystemChanged;
        Services.ChatGui.ChatMessage -= this.ChatMessage;

        Services.CommandManager.RemoveHandler(CommandName);
        Services.PluginInterface.UiBuilder.Draw -= this.DrawUi;
        Services.PluginInterface.UiBuilder.OpenConfigUi -= this.ToggleConfigUi;

        this.windowSystem.RemoveAllWindows();
        this.configWindow.Dispose();

        this.speechManager.Dispose();
        this.configuration.Save();

        GC.SuppressFinalize(this);
    }

    private void OnConfigurationSaved() {
        var maxInstances = this.configuration.MaxInstances;
        var volume = this.ComputeVolume();

        Task.Run(async () => {
            await this.speechManager.SetMaxInstances(maxInstances);
            await this.speechManager.SetVolume(volume);
        });
    }

    private int ComputeVolume() {
        if (!Services.GameConfig.TryGet(SystemConfigOption.SoundMaster, out uint masterVolume)) masterVolume = 100;
        var volume = this.configuration.Volume * (masterVolume / 100f);
        return Math.Min(Math.Max((int) volume, 0), 100);
    }

    private void ChatMessage(
        XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled
    ) {
        if (Services.ClientState.TerritoryType != Sinus_Ardorum && Services.ClientState.TerritoryType != Phaenna) return;
        if (!this.configuration.ChatTypes!.Contains(type)) return;

        try {
            var text = ExtractTextButGoated(message);
            if (string.IsNullOrWhiteSpace(text)) return;
            Task.Run(() => this.speechManager.TrySpeak(text));
        } catch (Exception e) {
            Services.PluginLog.Error(e, "Failed to extract text");
        }
    }

    private static string? ExtractTextButGoated(SeString message) {
        // Lumina SeString has better parsing
        var lumina = new Lumina.Text.SeString(message.Encode()).AsReadOnly();
        var linkDepth = 0;
        var str = new StringBuilder();

        foreach (var payload in lumina.AsEnumerable()) {
            if (payload.Type is ReadOnlySePayloadType.Macro) {
                // https://github.com/NotAdam/Lumina/blob/71b351f57e662ad1eb8379c6b04cb031b2dc8142/src/Lumina/Text/ReadOnly/ReadOnlySeStringSpan.cs#L254
                switch (payload.MacroCode) {
                    case MacroCode.NewLine: {
                        str.Append('\n');
                        break;
                    }

                    case MacroCode.NonBreakingSpace: {
                        str.Append('\u00A0');
                        break;
                    }

                    case MacroCode.Hyphen: {
                        str.Append('-');
                        break;
                    }

                    case MacroCode.SoftHyphen: {
                        str.Append('\u00AD');
                        break;
                    }

                    case MacroCode.Link: {
                        // Ignore links until their terminator
                        if (
                            linkDepth > 0
                            && payload.TryGetExpression(out var expression)
                            && expression.TryGetInt(out var linkTypeInt)
                            && ((LinkMacroPayloadType) linkTypeInt) is LinkMacroPayloadType.Terminator
                        ) {
                            linkDepth--;
                        } else {
                            linkDepth++;
                        }

                        continue;
                    }
                }
            }

            if (linkDepth > 0) continue;
            if (payload.Type is not ReadOnlySePayloadType.Text) continue;
            str.Append(payload.ToString());
        }

        return str.ToString();
    }

    private void SystemChanged(object? sender, ConfigChangeEvent e) {
        if (e.Option.Equals(SystemConfigOption.SoundMaster)) {
            var volume = this.ComputeVolume();
            Task.Run(() => this.speechManager.SetVolume(volume));
        }
    }

    private void DrawUi() => this.windowSystem.Draw();
    private void ToggleConfigUi() => this.configWindow.Toggle();

    private void OnCommand(string command, string args) {
        const string debug = "debug ";
        if (args.StartsWith(debug)) {
            var str = args[(debug.Length)..];
            Task.Run(() => this.speechManager.TrySpeak(str));
        } else {
            this.ToggleConfigUi();
        }
    }
}
