using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Dalamud.Interface.ImGuiNotification;
using MoonbaseOmega.TextToSpeech;

namespace MoonbaseOmega;

// Create multiple DECTalk instances so they can talk over each other for maximum comedic effect
// This is interacted with on the framework thread so there's no possibility for race conditions
public class SpeechManager : IDisposable {
    private readonly string dictionaryPath;
    private readonly Task downloadTask;
    private readonly List<TextToSpeech.TextToSpeech> instances = [];
    private int volume;
    private int maxInstances;

    public bool DownloadFailed => this.downloadTask is { IsCompleted: true, IsCompletedSuccessfully: false };

    public SpeechManager(string dectalkDir, int volume, int maxInstances) {
        Native.SetupResolver(Path.Combine(dectalkDir, "DECtalk.dll"));
        this.dictionaryPath = Path.Combine(dectalkDir, "dtalk_us.dic");
        this.volume = volume;
        this.maxInstances = maxInstances;

        this.downloadTask = Task.Run(() => DownloadDecTalk(dectalkDir)).ContinueWith(t => {
            if (t.IsFaulted) {
                Services.PluginLog.Error(t.Exception?.InnerException ?? t.Exception, "Failed to download DECtalk");

                Services.NotificationManager.AddNotification(new Notification() {
                    Title = "Moonbase Omega",
                    Content = "Failed to download DECtalk. See the config window for more information.",
                    Type = NotificationType.Error
                });
            }

            return t;
        }).Unwrap();
    }

    public void Dispose() {
        foreach (var instance in this.instances) instance.Dispose();
        this.instances.Clear();

        GC.SuppressFinalize(this);
    }

    public Task<bool> TrySpeak(string text) => Services.Framework.RunOnTick(() => {
        // This holds a lock for a concerning amount of time but w/e
        lock (this.instances) {
            // Try and play on an existing instance if possible
            var instance = this.instances.Find(instance => !instance.IsBusy());
            if (instance is not null) {
                instance.SetVolume(this.volume); // make sure to update the volume before speaking
                instance.Speak(text);
                return true;
            }

            if (this.instances.Count >= this.maxInstances) {
                Services.PluginLog.Verbose("Ran out of instances when playing message");
                return false;
            }

            if (!this.downloadTask.IsCompletedSuccessfully) {
                Services.PluginLog.Verbose("Tried to create instance before download task completed");
                return false;
            }

            // There's room to make a new instance
            var newInstance = new TextToSpeech.TextToSpeech(this.dictionaryPath);
            newInstance.SetVolume(this.volume);
            newInstance.Speak(text);
            this.instances.Add(newInstance);

            return true;
        }
    });

    public Task SetVolume(int newVolume) => Services.Framework.RunOnTick(() => {
        Services.PluginLog.Debug("Setting volume: {NewVolume}", newVolume);
        this.volume = newVolume;

        // NOTE: Updating the volume of speaking instances will crash the game
        // (heap corruption in ntdll, what the actual fuck lmfao)
    });

    public Task SetMaxInstances(int newMaxInstances) => Services.Framework.RunOnTick(() => {
        Services.PluginLog.Debug("Setting max instances: {NewMaxInstances}", newMaxInstances);

        lock (this.instances) {
            this.maxInstances = newMaxInstances;

            while (this.instances.Count > this.maxInstances) {
                var instance = this.instances.First();
                instance.Dispose();
                this.instances.Remove(instance);
            }
        }
    });

    public Task ResetAll() => Services.Framework.RunOnTick(() => {
        Services.PluginLog.Debug("Resetting all instances");

        lock (this.instances) {
            foreach (var instance in this.instances) {
                instance.Reset();
            }
        }
    });

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
}
