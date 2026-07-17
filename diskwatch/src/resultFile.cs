using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

[DataContract]
public class ResultFile
{
    [DataMember] public string LastRun { get; set; }
    [DataMember] public int ExitCode { get; set; }
    [DataMember] public string Output { get; set; }
}

public static class Results
{
    static string Dir()
    {
        string dir = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
            "..", "results");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static ResultFile Load(string name)
    {
        string path = Path.Combine(Dir(), name + ".json");
        if (!File.Exists(path)) return null;
        try
        {
            using (var s = File.OpenRead(path))
                return (ResultFile)new DataContractJsonSerializer(typeof(ResultFile)).ReadObject(s);
        }
        catch { return null; }
    }

    public static void Save(string name, ResultFile result)
    {
        string path = Path.Combine(Dir(), name + ".json");
        using (var s = File.Create(path))
            new DataContractJsonSerializer(typeof(ResultFile)).WriteObject(s, result);
    }

    public static void Save(string dir, string name, ResultFile result)
    {
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, name + ".json");
        using (var s = File.Create(path))
            new DataContractJsonSerializer(typeof(ResultFile)).WriteObject(s, result);
    }
}
