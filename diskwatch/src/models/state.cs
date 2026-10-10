using System.Collections.Generic;

public class SmartState
{
    public string Model;
    public string Serial;
    public string Firmware;
    public string Health;
    public int Endurance;
    public Dictionary<string, long> ImportantAttrs;
    public Dictionary<string, long> ExtraAttrs;
}

public class MasterState
{
    public string Timestamp;
    public Dictionary<string, SmartState> Smart;
}
