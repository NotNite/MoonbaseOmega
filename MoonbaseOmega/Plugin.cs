using System;
using System.IO;
using System.Threading.Tasks;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using MoonbaseOmega.Windows;
using Dalamud.Game.Command;
using Dalamud.Game.Config;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;

namespace MoonbaseOmega;

public class Plugin : IDalamudPlugin {
    private const ushort TerritoryType = 1237; // ffxiv/cos_c1/hou/c1w1/level/c1w1
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
        if (Services.ClientState.TerritoryType != TerritoryType) return;
        if (!this.configuration.ChatTypes!.Contains(type)) return;

        var text = message.TextValue;
        Task.Run(() => this.speechManager.TrySpeak(text));
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
