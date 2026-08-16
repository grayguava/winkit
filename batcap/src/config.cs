using System;
using System.Collections.Generic;
using System.IO;

class Config
{
    public int DesignCapacity = 44021;

    public bool Design = true;
    public bool Full = true;
    public bool Remaining = true;
    public bool Voltage = true;
    public bool ChargeRate = true;
    public bool DischargeRate = true;
    public bool Charging = true;
    public bool PowerOnline = false;
    public bool Critical = false;
    public bool Chemistry = false;
    public bool EstimatedChargeRemaining = false;
    public bool WearPercent = false;
    public bool EquivCycles = false;

    public static Config Load(string path)
    {
        var cfg = new Config();
        if (!File.Exists(path)) return cfg;
        try
        {
            foreach (string rawLine in File.ReadAllLines(path))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;

                string key, value;
                if (KeyValue(line, out key, out value))
                    cfg.Apply(key, value);
                else
                {
                    int val;
                    if (int.TryParse(line, out val)) cfg.DesignCapacity = val;
                }
            }
        }
        catch { }
        return cfg;
    }

    static bool KeyValue(string line, out string key, out string value)
    {
        key = null;
        value = null;
        int eq = line.IndexOf('=');
        if (eq <= 0) return false;
        key = line.Substring(0, eq).Trim();
        value = line.Substring(eq + 1).Trim();
        return key.Length > 0;
    }

    void Apply(string key, string value)
    {
        switch (key.ToLowerInvariant())
        {
            case "designcapacity":
            {
                int v;
                if (int.TryParse(value, out v)) DesignCapacity = v;
                break;
            }
            case "design": Design = ParseBool(value, Design); break;
            case "full": Full = ParseBool(value, Full); break;
            case "remaining": Remaining = ParseBool(value, Remaining); break;
            case "voltage": Voltage = ParseBool(value, Voltage); break;
            case "chargerate": ChargeRate = ParseBool(value, ChargeRate); break;
            case "dischargerate": DischargeRate = ParseBool(value, DischargeRate); break;
            case "charging": Charging = ParseBool(value, Charging); break;
            case "poweronline": PowerOnline = ParseBool(value, PowerOnline); break;
            case "critical": Critical = ParseBool(value, Critical); break;
            case "chemistry": Chemistry = ParseBool(value, Chemistry); break;
            case "estimatedchargeremaining": EstimatedChargeRemaining = ParseBool(value, EstimatedChargeRemaining); break;
            case "wearpercent": WearPercent = ParseBool(value, WearPercent); break;
            case "equivcycles": EquivCycles = ParseBool(value, EquivCycles); break;
        }
    }

    static bool ParseBool(string value, bool defaultValue)
    {
        bool v;
        if (bool.TryParse(value, out v)) return v;
        if (value == "1") return true;
        if (value == "0") return false;
        return defaultValue;
    }
}