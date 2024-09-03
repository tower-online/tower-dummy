using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;

namespace Tower.System;

public static class Settings
{
    public static string RemoteHost { get; }
    public static ushort RemotePort { get; }
    public static uint NumClients { get; }
    
    static Settings()
    {
        var content = File.ReadAllText("settings.yaml");
        var deserializer = new Deserializer();

        var settings = deserializer.Deserialize<Dictionary<string, object>>(content);
        RemoteHost = Convert.ToString(settings["remote_host"])!;
        RemotePort = Convert.ToUInt16(settings["remote_port"]);
        NumClients = Convert.ToUInt32(settings["num_clients"]);
    }
}