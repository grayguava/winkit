# batcap configuration

`bin/.conf` - plain text, one key per line. Keys are case-insensitive.
Lines starting with `#` or `;` are comments; blank lines are skipped.

Each boolean flag toggles a field in the log line. A disabled field is
omitted from the line entirely, and its WMI class is not queried at all
(unless a computed field below needs that data).

## Keys

| Key | Type | Default | Description |
|---|---|---|---|
| `DesignCapacity` | int (mWh) | `44021` | Battery design capacity. Read from config, **not** WMI - `BatteryStaticData.DesignedCapacity` intermittently throws `Generic failure` / `WBEM_E_PROVIDER_FAILURE` on this hardware, so the value is hardcoded instead. A bare integer on its own line (old format) still works. |
| `Design` | bool | `true` | Include `Design=` in the line. |
| `Full` | bool | `true` | Include `Full=` (from `BatteryFullChargedCapacity`, `root\WMI`). Stable, reliable. |
| `Remaining` | bool | `true` | Include `Remaining=` (from `BatteryStatus`, `root\WMI`). |
| `Voltage` | bool | `true` | Include `Voltage=` (from `BatteryStatus`). |
| `ChargeRate` | bool | `true` | Include `ChargeRate=` (from `BatteryStatus`). |
| `DischargeRate` | bool | `true` | Include `DischargeRate=` (from `BatteryStatus`). |
| `Charging` | bool | `true` | Include `Charging=` (from `BatteryStatus`). |
| `PowerOnline` | bool | `false` | Include `PowerOnline=` (from the **same** `BatteryStatus` object as Remaining/Voltage/etc. - enabling it adds no query). |
| `Critical` | bool | `false` | Include `Critical=` (same `BatteryStatus` object). |
| `Chemistry` | bool | `false` | Include `Chemistry=` (from `Win32_Battery`, `root\cimv2` - a **separate** namespace/query). WMI reports a numeric code, mapped to a name (e.g. `Li-ion`). |
| `EstimatedChargeRemaining` | bool | `false` | Include `EstimatedChargeRemaining=` as a percentage (from `Win32_Battery`, `root\cimv2`). |
| `WearPercent` | bool | `false` | Include `WearPercent=`, computed as `(Design - Full) / Design * 100`. Only needs `DesignCapacity` and `Full`; enables the `BatteryFullChargedCapacity` query if needed. |
| `EquivCycles` | bool | `false` | Include `EquivCycles=`, computed as cumulative discharged mWh / `DesignCapacity`. Discharge is summed whenever `Remaining` decreases while not charging. Enables the `BatteryStatus` query if needed. |

## EquivCycles state

`bin/.cyclestate` holds the running discharge total and the last-seen
`Remaining` value. It is read at the start of each run and updated at the
end, and keeps accumulating **regardless** of the `EquivCycles` toggle - so
toggling the field off and on later never loses history.

```
LastRemaining=21870
Total=4546.0
```

## Not supported

Confirmed absent or unreliable on my hardware, so no config keys exist:

- `CycleCount` / `Cycles` - the `_BIF` firmware has no cycle-count field.
- `BatteryTemperature` - unpopulated by this battery.
- `BatteryStaticData` (`DesignedCapacity`, `DesignVoltage`, `ManufactureDate`, `SerialNumber`) - `WBEM_E_PROVIDER_FAILURE`.
