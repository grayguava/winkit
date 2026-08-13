using System.Collections.Generic;
using System.IO;

class Pool
{
    static readonly List<string> Extensions = new List<string> { "*.jpg", "*.jpeg", "*.png", "*.bmp", "*.webp" };

    public PoolConfig Config;
    public int HotkeyId;
    public PoolState State;

    public string Name { get { return Config.Name; } }
    public bool IsSync { get { return Config.IsSync; } }

    public bool Validate()
    {
        if (!Directory.Exists(Config.Dir)) return false;
        foreach (string ext in Extensions)
            if (Directory.GetFiles(Config.Dir, ext, SearchOption.TopDirectoryOnly).Length > 0)
                return true;
        return false;
    }

    public void Activate(List<string> targetNames)
    {
        if (targetNames.Count == 0) return;

        if (IsSync)
        {
            string img = State.NextFromPool(Extensions);
            if (img != null)
                foreach (string name in targetNames)
                    TargetRegistry.Apply(name, img);
            return;
        }

        var assigned = new HashSet<string>();
        foreach (string name in targetNames)
        {
            string img = State.NextFromPool(Extensions);
            if (img == null) break;
            if (!assigned.Contains(img))
            {
                assigned.Add(img);
                TargetRegistry.Apply(name, img);
            }
        }
    }
}
