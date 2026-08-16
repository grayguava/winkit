using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

class Program
{
    static string ExeDir
    {
        get { return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location); }
    }

    static int Main()
    {
        string exeDir = ExeDir;
        Config cfg = Config.Load(Path.Combine(exeDir, ".conf"));
        BatteryData data = BatteryReader.Read(cfg);

        string statePath = Path.Combine(exeDir, ".cyclestate");
        CycleState state = CycleState.Load(statePath);

        if (data.Remaining.HasValue && data.Charging.HasValue)
        {
            state.Update(data.Remaining.Value, data.Charging.Value);
            state.Save(statePath);
        }

        string line = BuildLine(cfg, data, state);

        string logDir = Path.GetFullPath(Path.Combine(exeDir, "..", "logs"));
        Directory.CreateDirectory(logDir);
        File.AppendAllText(Path.Combine(logDir, "batcap.log"), line + Environment.NewLine);

        Console.WriteLine(line);
        return 0;
    }

    static string BuildLine(Config cfg, BatteryData data, CycleState state)
    {
        var sb = new StringBuilder();
        sb.Append('[').Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append(']');

        if (cfg.Design)
            sb.Append(" Design=").Append(cfg.DesignCapacity).Append("mWh");

        if (cfg.Full && data.Full.HasValue)
            sb.Append(" Full=").Append(data.Full.Value).Append("mWh");

        if (cfg.Remaining && data.Remaining.HasValue)
            sb.Append(" Remaining=").Append(data.Remaining.Value).Append("mWh");

        if (cfg.Voltage && data.Voltage.HasValue)
            sb.Append(" Voltage=").Append(data.Voltage.Value).Append("mV");

        if (cfg.ChargeRate && data.ChargeRate.HasValue)
            sb.Append(" ChargeRate=").Append(data.ChargeRate.Value).Append("mW");

        if (cfg.DischargeRate && data.DischargeRate.HasValue)
            sb.Append(" DischargeRate=").Append(data.DischargeRate.Value).Append("mW");

        if (cfg.Charging && data.Charging.HasValue)
            sb.Append(" Charging=").Append(data.Charging.Value);

        if (cfg.PowerOnline && data.PowerOnline.HasValue)
            sb.Append(" PowerOnline=").Append(data.PowerOnline.Value);

        if (cfg.Critical && data.Critical.HasValue)
            sb.Append(" Critical=").Append(data.Critical.Value);

        if (cfg.Chemistry && data.Chemistry != null)
            sb.Append(" Chemistry=").Append(data.Chemistry);

        if (cfg.EstimatedChargeRemaining && data.EstimatedChargeRemaining.HasValue)
            sb.Append(" EstimatedChargeRemaining=").Append(data.EstimatedChargeRemaining.Value).Append('%');

        if (cfg.WearPercent && cfg.DesignCapacity > 0 && data.Full.HasValue)
        {
            double wear = (double)(cfg.DesignCapacity - data.Full.Value) / cfg.DesignCapacity * 100.0;
            sb.Append(" WearPercent=").Append(wear.ToString("0.0", CultureInfo.InvariantCulture)).Append('%');
        }

        if (cfg.EquivCycles && cfg.DesignCapacity > 0)
            sb.Append(" EquivCycles=").Append((state.Total / cfg.DesignCapacity).ToString("0.0", CultureInfo.InvariantCulture));

        return sb.ToString();
    }
}

class CycleState
{
    public int? LastRemaining;
    public double Total;

    public static CycleState Load(string path)
    {
        var state = new CycleState();
        if (!File.Exists(path)) return state;
        try
        {
            foreach (string rawLine in File.ReadAllLines(path))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();
                if (key.Equals("LastRemaining", StringComparison.OrdinalIgnoreCase))
                {
                    int v;
                    if (int.TryParse(value, out v)) state.LastRemaining = v;
                }
                else if (key.Equals("Total", StringComparison.OrdinalIgnoreCase))
                {
                    double v;
                    if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) state.Total = v;
                }
            }
        }
        catch { }
        return state;
    }

    public void Update(int remaining, bool charging)
    {
        if (LastRemaining.HasValue && !charging && remaining < LastRemaining.Value)
            Total += LastRemaining.Value - remaining;
        LastRemaining = remaining;
    }

    public void Save(string path)
    {
        try
        {
            File.WriteAllText(path,
                "LastRemaining=" + (LastRemaining.HasValue ? LastRemaining.Value.ToString() : "") + Environment.NewLine +
                "Total=" + Total.ToString("0.0", CultureInfo.InvariantCulture) + Environment.NewLine);
        }
        catch { }
    }
}