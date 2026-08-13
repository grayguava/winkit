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

The nature/tech distinction is purely about which images are in the folder - the tool doesn't need to know or care. So the two binaries were merged into one, with a single `assets/` directory:

- **A shuffle queue** instead of pure random - every image is shown exactly once before any repeats, so a varied rotation is guaranteed.
- **A persistent `state` file** - the cycle survives reboots.
- **Image sync** - added images get merged into the queue, removed ones are dropped silently, with no manual cleanup.
- **A `build.bat`** - recompiling is one command.

The result is a single always-available hotkey that cycles through a guaranteed-varied wallpaper rotation, with zero maintenance for the pools.
