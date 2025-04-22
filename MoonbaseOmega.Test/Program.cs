using MoonbaseOmega.TextToSpeech;

const string dir = "C:/Users/Julian/AppData/Roaming/XIVLauncher/pluginConfigs/MoonbaseOmega/DECtalk";
Native.SetupResolver(Path.Combine(dir, "DECTALK.dll"));
var dictionary = Path.Combine(dir, "dtalk_us.dic");

var tasks = new List<Task>();
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

await Task.WhenAll(tasks);
