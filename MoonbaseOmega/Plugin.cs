using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using MoonbaseOmega.Windows;
using Dalamud.Game.Command;
using Dalamud.Game.Config;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.ImGuiNotification;

namespace MoonbaseOmega;

public class Plugin : IDalamudPlugin {
    private const ushort TerritoryType = 1237; // ffxiv/cos_c1/hou/c1w1/level/c1w1
    private const string CommandName = "/moonbaseomega";

    private readonly WindowSystem windowSystem = new("MoonbaseOmega");
    private readonly Configuration configuration;
    private readonly ConfigWindow configWindow;
    private SpeechManager? speechManager;

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

        Task.Run(() => DownloadDecTalk(outputDir)).ContinueWith(t => {
            if (t.IsFaulted) {
                Services.PluginLog.Error(t.Exception.InnerException, "Failed to download DECtalk");
                Services.NotificationManager.AddNotification(new Notification() {
                    Title = "Moonbase Omega",
                    Content = "Failed to download DECtalk. Please report this error.",
                    Type = NotificationType.Error
                });
            } else {
                this.speechManager = new SpeechManager(outputDir);
                this.UpdateVolume();
                Services.PluginLog.Debug("DECtalk initialized :3");
            }
        });

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

        this.speechManager?.Dispose();
        this.configuration.Save();

        GC.SuppressFinalize(this);
    }

    private static async Task DownloadDecTalk(string outputDir) {
        const string url = "https://github.com/dectalk/dectalk/releases/download/2023-10-30/vs2022.zip";
        const string expectedHash = "4a778056c109b37f95ade4b3d3e308b9396b22a4b0629f9756ec0e5051b9636d";
        string[] zipFiles = ["AMD64/DECtalk.dll", "AMD64/dtalk_us.dic"];

        // Already installed
        if (zipFiles.All(entry => {
                var name = Path.GetFileName(entry);
                var path = Path.Combine(outputDir, name);
                return File.Exists(path);
            })) {
            return;
        }

        Services.PluginLog.Debug("Downloading DECtalk...");
        using var http = new HttpClient();
        var data = await http.GetByteArrayAsync(url);
        var hash = Convert.ToHexStringLower(SHA256.HashData(data));
        if (hash != expectedHash) throw new Exception($"Mismatched hash (expected {expectedHash}, got {hash})");

        using var ms = new MemoryStream(data);
        using var zip = new ZipArchive(ms);

        foreach (var file in zipFiles) {
            var entry = zip.GetEntry(file)!;
            var outputPath = Path.Combine(outputDir, Path.GetFileName(file));

            await using var bytes = entry.Open();
            await using var output = File.OpenWrite(outputPath);
            await bytes.CopyToAsync(output);
            await output.FlushAsync();
        }
    }

    private void UpdateVolume() {
        if (this.speechManager is not { } speech) return;

        if (!Services.GameConfig.TryGet(SystemConfigOption.SoundMaster, out uint masterVolume)) masterVolume = 100;
        var volume = this.configuration.Volume * (masterVolume / 100f);
        var volumeInt = Math.Min(Math.Max((int) volume, 0), 100);

        Services.PluginLog.Debug("Setting volume: {Volume}", volumeInt);
        Services.Framework.RunOnTick(() => speech.SetVolume(volumeInt));
    }

    private void ChatMessage(
        XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled
    ) {
        if (Services.ClientState.TerritoryType != TerritoryType) return;
        if (!this.configuration.ChatTypes!.Contains(type)) return;
        this.Speak(message.TextValue);
    }

    private void Speak(string message) => Services.Framework.RunOnTick(() => {
        if (this.speechManager is not { } speech) return;

        try {
            var spoke = speech.TrySpeak(message);
            if (!spoke) Services.PluginLog.Warning("Failed to speak message (probably out of slots?)");
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
