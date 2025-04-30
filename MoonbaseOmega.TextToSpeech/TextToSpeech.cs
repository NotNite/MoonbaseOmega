namespace MoonbaseOmega.TextToSpeech;

public class TextToSpeech : IDisposable {
    private readonly nint handle;

    public TextToSpeech(string dictionary) {
        AssertCall(
            Native.TextToSpeechStartupExFonix(ref this.handle, Native.WaveMapper, 0,
                nint.Zero, 0,
                dictionary
            ),
            "TextToSpeechStartup"
        );
    }

    public void Dispose() {
        AssertCall(
            Native.TextToSpeechShutdown(this.handle),
            "TextToSpeechShutdown"
        );

        GC.SuppressFinalize(this);
    }

    public void Speak(string text) => AssertCall(
        Native.TextToSpeechSpeak(this.handle, text, Native.SpeechFlags.Force),
        "TextToSpeechSpeak"
    );

    public void SetVolume(int volume) => AssertCall(
        Native.TextToSpeechSetVolume(this.handle, Native.VolumeType.Main, volume),
        "TextToSpeechSetVolume"
    );

    public bool IsBusy() {
        uint[] identifiers = [Native.StatusSpeaking];
        uint[] statuses = [0];

        AssertCall(
            Native.TextToSpeechGetStatus(this.handle, identifiers, statuses, 1),
            "TextToSpeechGetStatus"
        );

        return statuses[0] != 0;
    }

    private static void AssertCall(uint value, string method) {
        if (value != 0) throw new Exception($"Calling {method} returned error code {value}");
    }
}
