using System.Diagnostics;
using MoonbaseOmega.TextToSpeech;

var xl = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "XIVLauncher"
);
var dir = Path.Combine(xl, "pluginConfigs", "MoonbaseOmega", "DECtalk");

Native.SetupResolver(Path.Combine(dir, "DECtalk.dll"));
var dictionary = Path.Combine(dir, "dtalk_us.dic");

var tts = new TextToSpeech(dictionary);
tts.SetVolume(100);

tts.Speak("john madden");
Console.WriteLine("sent speak");

await Task.Delay(500);
tts.Reset();
Console.WriteLine("sent reset");

await Task.Delay(500);
tts.Speak("aeiou");
Console.WriteLine("sent speak 2");

await Task.Delay(1000);
Console.WriteLine(tts.IsBusy());
