# Why batcap exists

## The problem

Windows' `powercfg /batteryreport` generates a battery report at `%USERPROFILE%\battery-report.html`. On my machine (EliteBook 840 G2), that report shows `-` for every battery capacity field - design capacity, full charge capacity, cycle count, all blank. The Windows "Battery capacity history" and "Battery life estimates" sections are entirely empty.

`powercfg /energy` was also tested as an alternative. It does run a valid 60-second trace, but it pulls Design Capacity from the same broken source - its report showed Design and Full Charge as identical values, meaning it was silently falling back to Full Charge for both rather than reading a true separate design figure.

## The root cause

The `BatteryStaticData` WMI class (which exposes `DesignedCapacity`) intermittently returns a `Generic failure` error on this hardware - likely a driver/ACPI quirk where that specific counter isn't reliably surfaced, even though every other battery counter works fine.

## The approach

Since design capacity never changes, this tool sidesteps the broken class entirely: the true nameplate value (confirmed once via the original working `powercfg /batteryreport`) is hardcoded in `bin/.conf` instead of queried each run.

`BatteryFullChargedCapacity`, `BatteryStatus`, and `BatteryCycleCount` all return valid data reliably, so the tool polls those directly each run and appends to a running log - the historical view `powercfg` was supposed to provide but doesn't on this machine.
