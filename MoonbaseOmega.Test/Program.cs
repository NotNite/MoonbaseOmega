using MoonbaseOmega.TextToSpeech;

var xl = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "XIVLauncher"
);
var dir = Path.Combine(xl, "pluginConfigs", "MoonbaseOmega", "DECtalk");

Native.SetupResolver(Path.Combine(dir, "DECtalk.dll"));
var dictionary = Path.Combine(dir, "dtalk_us.dic");

/*var tasks = new List<Task>();
for (var i = 0; i < 5; i++) {
    var index = i;
    tasks.Add(Task.Run(async () => {
        try {
            var tts = new TextToSpeech(dictionary);
            tts.SetVolume(100);

            await Task.Delay(index * 100);

            Console.WriteLine(tts.IsBusy());
            tts.Speak("aeiou");

            await Task.Delay(100);
            Console.WriteLine(tts.IsBusy());

            await Task.Delay(1000);
        } catch (Exception e) {
            Console.WriteLine(e);
        }
    }));
}
await Task.WhenAll(tasks);*/

var tts = new TextToSpeech(dictionary);
tts.SetVolume(100);

tts.Speak("aeiou");
await Task.Delay(100);
tts.Dispose();

await Task.Delay(1000);
