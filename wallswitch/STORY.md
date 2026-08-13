# Why wallswitch exists

## The problem

I used to have two separate wallpaper tools - one for nature images, one for tech images. Each was its own binary with its own folder and its own hotkey mapping. That split was a maintenance headache: every wallpaper change to either pool meant touching two tools, and there was no way to guarantee a varied rotation.

## The old approach

The old tools (`archive/wallswitch/v1/`) worked but had real gaps:

- **Pure random selection** - the same image could repeat several times before others ever showed.
- **No state tracking** - nothing persisted across runs, so restarting the machine lost all rotation history.
- **No image-add detection** - adding a new image to the pool did nothing until a restart.
- **No build script** - recompiling meant remembering the right `csc` flags.

## The new approach

The nature/tech distinction is purely about which images are in the folder - the tool doesn't need to know or care. So the two binaries were merged into one, configurable in `.pools`:

- **Pools over folders** - each pool is its own section in `.pools` with its own directory, hotkey, and mode. Nature and tech are just two pool sections now; adding a third pool is a config edit, not a new tool.
- **Targets over "just the desktop"** - a pool can apply to the desktop, Windows Terminal's background, and the registry (for reboot persistence), in one press.
- **A shuffle queue** instead of pure random - every image is shown exactly once before any repeats, so a varied rotation is guaranteed.
- **A persistent `state` file per pool** - each pool's cycle survives reboots independently.
- **Live config reload** - pool `Mode`/`Dir` changes apply on the next press, no restart.
- **A `build.bat`** - recompiling is one command.

The result is one daemon with a hotkey per pool, each cycling a guaranteed-varied rotation across every target you enable, with zero maintenance.
