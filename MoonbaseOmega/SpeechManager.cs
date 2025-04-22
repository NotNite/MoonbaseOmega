using System;
using System.Collections.Generic;
using System.IO;
using MoonbaseOmega.TextToSpeech;

namespace MoonbaseOmega;

// Create multiple DECTalk instances so they can talk over each other for maximum comedic effect
// This is interacted with on the framework thread so there's no possibility for race conditions
public class SpeechManager : IDisposable {
    private const int MaxInstances = 5;

    private readonly string dictionaryPath;
    private readonly List<TextToSpeech.TextToSpeech> instances = [];
    private int volume = 100;

    public SpeechManager(string dectalkDir) {
        Native.SetupResolver(Path.Combine(dectalkDir, "DECtalk.dll"));
        this.dictionaryPath = Path.Combine(dectalkDir, "dtalk_us.dic");
    }

    public bool TrySpeak(string text) {
        // Try and play on an existing instance if possible
        foreach (var instance in this.instances) {
            if (instance.IsBusy()) continue;
            instance.Speak(text);
            return true;
        }

        // Used up all instances :(
        if (this.instances.Count >= MaxInstances) return false;

        // There's room to make a new instance
        var newInstance = new TextToSpeech.TextToSpeech(this.dictionaryPath);
        newInstance.SetVolume(this.volume);
        newInstance.Speak(text);
        this.instances.Add(newInstance);

        return true;
    }

    public void SetVolume(int newVolume) {
        this.volume = newVolume;
        foreach (var instance in this.instances) instance.SetVolume(this.volume);
    }

    public void Dispose() {
        foreach (var instance in this.instances) instance.Dispose();
        this.instances.Clear();

        GC.SuppressFinalize(this);
    }
}
