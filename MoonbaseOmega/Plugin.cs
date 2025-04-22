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
    private readonly SpeechManager speechManager;
    private readonly Configuration configuration;
    private readonly ConfigWindow configWindow;

    public Plugin(IDalamudPluginInterface pluginInterface) {
        pluginInterface.Create<Services>();

        this.configuration = Services.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        // for some reason the defaults duplicate, wtf newtonsoft?
        this.configuration.ChatTypes ??= [
            XivChatType.Say,
            XivChatType.Yell,
            XivChatType.Shout
        ];

        this.speechManager = new SpeechManager(Path.Combine(
            Services.PluginInterface.AssemblyLocation.DirectoryName!,
            "DECtalk"
        ));
        this.UpdateVolume();

        this.configWindow = new ConfigWindow(this.configuration, this.UpdateVolume);
        this.windowSystem.AddWindow(this.configWindow);

        Services.PluginInterface.UiBuilder.Draw += this.DrawUi;
        Services.PluginInterface.UiBuilder.OpenConfigUi += this.ToggleConfigUi;
        Services.CommandManager.AddHandler(CommandName, new CommandInfo(this.OnCommand) {
            HelpMessage = "Open the config window"
        });

        Services.ChatGui.ChatMessage += this.ChatMessage;
        Services.GameConfig.SystemChanged += this.SystemChanged;
    }

    public void Dispose() {
        Services.GameConfig.SystemChanged -= this.SystemChanged;
        Services.ChatGui.ChatMessage -= this.ChatMessage;

        Services.CommandManager.RemoveHandler(CommandName);
        Services.PluginInterface.UiBuilder.Draw -= this.DrawUi;
        Services.PluginInterface.UiBuilder.OpenConfigUi -= this.ToggleConfigUi;

        this.windowSystem.RemoveAllWindows();

        this.speechManager.Dispose();
        this.configuration.Save();

        GC.SuppressFinalize(this);
    }

    private void UpdateVolume() {
        if (!Services.GameConfig.TryGet(SystemConfigOption.SoundMaster, out uint masterVolume)) masterVolume = 100;
        var volume = this.configuration.Volume * (masterVolume / 100f);
        var volumeInt = Math.Min(Math.Max((int) volume, 0), 100);

        Services.PluginLog.Debug("Setting volume: {Volume}", volumeInt);

        Services.Framework.RunOnTick(() => {
            this.speechManager.SetVolume(volumeInt);
        });
    }

    private void ChatMessage(
        XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled
    ) {
        if (Services.ClientState.TerritoryType != TerritoryType) return;
        if (!this.configuration.ChatTypes!.Contains(type)) return;
        this.Speak(message.TextValue);
    }

    private void Speak(string message) => Services.Framework.RunOnTick(() => {
        try {
            var spoke = this.speechManager.TrySpeak(message);
            if (!spoke) Services.PluginLog.Warning("Failed to speak message (probably out of slots)");
        } catch (Exception e) {
            Services.PluginLog.Warning(e, "Error when speaking message");
        }
    });

    private void SystemChanged(object? sender, ConfigChangeEvent e) {
        if (e.Option.Equals(SystemConfigOption.SoundMaster)) this.UpdateVolume();
    }

    private void DrawUi() => this.windowSystem.Draw();
    private void ToggleConfigUi() => this.configWindow.Toggle();

    private void OnCommand(string command, string args) {
        const string debug = "debug ";
        if (args.StartsWith(debug)) {
            var str = args[(debug.Length)..];
            this.Speak(str);
        } else {
            this.ToggleConfigUi();
        }
    }
}
