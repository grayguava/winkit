using System;
using System.Management;

class BatteryData
{
    public int? Full;
    public int? Remaining;
    public int? Voltage;
    public int? ChargeRate;
    public int? DischargeRate;
    public bool? Charging;
    public bool? PowerOnline;
    public bool? Critical;
    public string Chemistry;
    public int? EstimatedChargeRemaining;
}

static class BatteryReader
{
    public static BatteryData Read(Config cfg)
    {
        var data = new BatteryData();

        if (cfg.Full || cfg.WearPercent)
            ReadFullChargedCapacity(data);

        if (cfg.Remaining || cfg.Voltage || cfg.ChargeRate || cfg.DischargeRate || cfg.Charging || cfg.PowerOnline || cfg.Critical || cfg.EquivCycles)
            ReadBatteryStatus(data);

        if (cfg.Chemistry || cfg.EstimatedChargeRemaining)
            ReadWin32Battery(data);

        return data;
    }

    static void ReadFullChargedCapacity(BatteryData data)
    {
        try
        {
            using (var searcher = new ManagementObjectSearcher(
                @"root\WMI", "SELECT * FROM BatteryFullChargedCapacity"))
            {
                foreach (ManagementObject o in searcher.Get())
                {
                    data.Full = Convert.ToInt32(o["FullChargedCapacity"]);
                    break;
                }
            }
        }
        catch { }
    }

    static void ReadBatteryStatus(BatteryData data)
    {
        try
        {
            using (var searcher = new ManagementObjectSearcher(
                @"root\WMI", "SELECT * FROM BatteryStatus"))
            {
                foreach (ManagementObject o in searcher.Get())
                {
                    object rc = o["RemainingCapacity"];
                    if (rc != null)
                    {
                        int remaining = Convert.ToInt32(rc);
                        if (remaining > 0) data.Remaining = remaining;
                    }
                    data.Voltage = Convert.ToInt32(o["Voltage"]);
                    data.ChargeRate = Convert.ToInt32(o["ChargeRate"]);
                    data.DischargeRate = Convert.ToInt32(o["DischargeRate"]);
                    data.Charging = Convert.ToBoolean(o["Charging"]);
                    data.PowerOnline = Convert.ToBoolean(o["PowerOnline"]);
                    data.Critical = Convert.ToBoolean(o["Critical"]);
                    break;
                }
            }
        }
        catch { }
    }

    static void ReadWin32Battery(BatteryData data)
    {
        try
        {
            using (var searcher = new ManagementObjectSearcher(
                @"root\cimv2", "SELECT * FROM Win32_Battery"))
            {
                foreach (ManagementObject o in searcher.Get())
                {
                    object chem = o["Chemistry"];
                    if (chem != null)
                        data.Chemistry = ChemistryName(Convert.ToInt32(chem));
                    object ecr = o["EstimatedChargeRemaining"];
                    if (ecr != null)
                        data.EstimatedChargeRemaining = Convert.ToInt32(ecr);
                    break;
                }
            }
        }
        catch { }
    }

    static string ChemistryName(int code)
    {
        switch (code)
        {
            case 1: return "Other";
            case 2: return "Unknown";
            case 3: return "LeadAcid";
            case 4: return "NiCd";
            case 5: return "NiMH";
            case 6: return "Li-ion";
            case 7: return "ZincAir";
            case 8: return "LiPo";
            default: return code.ToString();
        }
    }
}