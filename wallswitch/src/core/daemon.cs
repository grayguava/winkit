using System.Collections.Generic;
using System.IO;

static class Daemon
{
    static string exeDir;
    static List<Pool> pools = new List<Pool>();
    static List<string> targetNames = new List<string>();

    public static List<string> Warnings = new List<string>();

    public static void Init(string dir)
    {
        exeDir = dir;
    }

    public static List<Pool> LoadAndValidate()
    {
        Warnings.Clear();

        var targetSections = TargetsConfig.Load(Path.Combine(exeDir, ".targets"));
        TargetRegistry.Load(targetSections, exeDir);
        targetNames = TargetRegistry.EnabledNames;

        var result = new List<Pool>();
        foreach (var cfg in PoolsConfig.Load(Path.Combine(exeDir, ".pools")))
        {
            if (!IsValidPoolName(cfg.Name))
            {
                Warnings.Add("Pool '" + cfg.Name + "' has an invalid name - disabled.");
                continue;
            }

            if (cfg.Key.Length == 0)
            {
                Warnings.Add("Pool '" + cfg.Name + "' has empty PoolKey - disabled.");
                continue;
            }

            uint mods, vk;
            if (!Hotkey.Parse(cfg.Key, out mods, out vk))
            {
                Warnings.Add("Pool '" + cfg.Name + "' has invalid PoolKey '" + cfg.Key + "' - disabled.");
                continue;
            }

            cfg.Dir = NormalizePath(cfg.Dir);

            if (!Directory.Exists(cfg.Dir))
            {
                Warnings.Add("Pool '" + cfg.Name + "' dir missing (" + cfg.Dir + ") - disabled.");
                continue;
            }

            var pool = new Pool { Config = cfg };
            if (!pool.Validate())
            {
                Warnings.Add("Pool '" + cfg.Name + "' has no supported images - disabled.");
                continue;
            }

            string stateDir = Path.Combine(exeDir, "state");
            if (!Directory.Exists(stateDir)) Directory.CreateDirectory(stateDir);
            pool.State = PoolState.Load(Path.Combine(stateDir, cfg.Name), cfg.Dir);
            result.Add(pool);
        }
        return result;
    }

    public static void RegisterHotkeys(HotkeyForm form, List<Pool> readyPools)
    {
        pools = readyPools;
        int id = 1;
        foreach (Pool pool in pools)
        {
            uint mods, vk;
            Hotkey.Parse(pool.Config.Key, out mods, out vk);
            pool.HotkeyId = id;
            form.AddHotkey(id, mods, vk);
            id++;
        }
    }

    public static void HandleHotkey(int id)
    {
        foreach (Pool pool in pools)
        {
            if (pool.HotkeyId == id)
            {
                RefreshPool(pool);
                pool.Activate(targetNames);
                return;
            }
        }
    }

    static void RefreshPool(Pool pool)
    {
        List<PoolConfig> configs;
        try
        {
            configs = PoolsConfig.Load(Path.Combine(exeDir, ".pools"));
        }
        catch
        {
            return;
        }

        foreach (var cfg in configs)
        {
            if (string.Equals(cfg.Name, pool.Name, System.StringComparison.OrdinalIgnoreCase))
            {
                if (!Directory.Exists(cfg.Dir)) break;
                pool.Config = cfg;
                pool.State.PoolDir = cfg.Dir;
                break;
            }
        }
    }

    static string NormalizePath(string p)
    {
        if (string.IsNullOrEmpty(p) || Path.IsPathRooted(p)) return p;
        return Path.GetFullPath(Path.Combine(exeDir, p));
    }

    static bool IsValidPoolName(string name)
    {
        if (name.Length == 0 || name == "." || name == "..") return false;
        foreach (char c in Path.GetInvalidFileNameChars())
            if (name.IndexOf(c) >= 0) return false;
        return true;
    }
}
