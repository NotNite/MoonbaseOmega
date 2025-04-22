using Dalamud.Configuration;
using System;
using System.Collections.Generic;
using Dalamud.Game.Text;
using Newtonsoft.Json;

namespace MoonbaseOmega;

[Serializable]
public class Configuration : IPluginConfiguration {
    public int Version { get; set; } = 0;

    [JsonProperty] public int Volume = 50;

    [JsonProperty]
    public List<XivChatType>? ChatTypes;

    public void Save() {
        Services.PluginInterface.SavePluginConfig(this);
    }
}
