using System.Collections.Generic;

public class DriveState
{
    public bool? Dirty;
    public string Filesystem;
    public long BadSectorsKb;
}

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
    public Dictionary<string, DriveState> Drives;
    public Dictionary<string, SmartState> Smart;
}
