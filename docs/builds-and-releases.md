# Builds and releases

[Docs](README.md) › Builds and releases

## Contents

- [Building the game](#building-the-game)
- [Platform notes](#platform-notes)
- [The build number](#the-build-number)
- [Releases](#releases)

## Building the game

Menu **Gwent > Build > All platforms** (or one platform) builds the game for Linux, Windows, macOS (Apple Silicon), Android and the
web, each from the same editor, into `GwentCardGame/Builds/<version>/` with archives ready to hand out (`GwentLegacy-Linux.tar.gz`,
`-Windows.zip`, `-macOS.zip`, `-Android.apk`, `-Web.zip`) and `build-report.txt`. A platform whose build support is not installed
(Unity Hub > Installs > Add modules) is skipped. The run goes on with the editor in the background, starts with the platform the
editor is on and switches back to it at the end (each platform's imports are cached, so a switch takes seconds); all five take
about 4 minutes once they have been built once. From the command line, with the editor closed:

```bash
Unity -batchmode -projectPath GwentCardGame -executeMethod Gwent.EditorTools.GwentBuild.BuildAllBatch -buildTargets linux,windows
```

## Platform notes

- **Android**: a 64-bit (ARM64) `.apk` to install directly. It is signed with a release key when the environment has
  `GWENT_KEYSTORE` (the keystore file), `GWENT_KEYSTORE_PASS`, `GWENT_KEY_ALIAS` and `GWENT_KEY_PASS`; otherwise with Unity's
  debug key, and a build from another machine then only installs after the old one is removed (which deletes its decks).
  The keystore path and version code it sets are put back in the project's settings after the build.
- **macOS and Windows**: the macOS app is ad-hoc signed by Unity, not notarized: macOS asks once to allow it (System Settings >
  Privacy & Security > Open Anyway). Windows shows "Windows protected your PC" once (More info > Run anyway).
- **Content on demand**: the cards' full art and the music are not in a build's data: they are Addressables bundles beside it
  (`StreamingAssets/aa`, built with each player), loaded the first time they are shown or played. The web build starts once the
  rest is downloaded; a battle fetches its two decks' art as it begins, the deck builder the cards it shows, and the browser caches
  them.
- **Web**: compressed with a fallback, so any static host serves it. In a browser Hard thinks a few milliseconds a frame (there is
  no other thread), Quit is gone, the display settings are a Fullscreen switch, decks are stored in the browser, and there is no
  LAN play.
- **Images** are kept to sizes that are multiples of 4 so they compress on every platform (a card at 380x591 would ship
  uncompressed, four times larger).
- **Shaders**: one graphics API per platform (Windows Direct3D 11, Linux OpenGL Core, macOS Metal, Android OpenGL ES 3, the web
  WebGL 2) and no post-processing in the renderer (the game uses none): shaders are compiled once per platform, not for every effect
  and API.

## The build number

Every build carries the source it was built from: the commit (`1a2b3c4d`), and when the working tree had changes not committed yet,
a hash of them too (`1a2b3c4d.5e6f70`), so that two builds match only when built from the same files. `Assets/Editor/BuildStamp.cs`
writes it to `Assets/Resources/BuildStamp.txt` (not kept in git) as each build starts and as the editor loads its scripts; the game
reads it through `BuildInfo` ("1.0.0+1a2b3c4d"), shows it on the main menu and in the LAN lobby, and writes it in the game log's
`session_start`.

A stamp given on the command line, `-buildStamp 1a2b3c4d`, is taken as it is: the release workflow gives every platform's build
its commit, since those builds run in a container where git may not work (the checkout belongs to another user) and where a
platform's own files (Android's keystore) would count as changes.

Two devices play each other on LAN only with the same build ([Play on LAN](lan.md#the-same-build)). Unity's own
`Application.buildGUID` would not do: it differs between platforms built from the same source.

## Releases

**Every push to `main` is a release.** `.github/workflows/release.yml` runs the EditMode tests, builds the five platforms on
GitHub's machines ([GameCI](https://game.ci), Unity 6000.6.5f1, `Gwent.EditorTools.GwentBuild.BuildAllBatch` for each) and replaces
the one release, tagged `latest`, with the new archives; its notes are the newest section of [`CHANGELOG.md`](../CHANGELOG.md),
followed by the downloads and their checksums. Nothing is published unless the tests and all five builds pass, and a push whose
`CHANGELOG.md` has no new section since the last release is not released (its run checks the changelog, skips the rest and
passes): add a section, headed by the date, before pushing. Work on other branches and merge into `main` only to release.

The download links stay the same from one release to the next:
`https://github.com/usersina/GwentUnityClone/releases/latest/download/GwentLegacy-<Windows.zip|macOS.zip|Linux.tar.gz|Android.apk|Web.zip>`.

The workflow needs the Unity license and the Android release key as repository secrets; `tools/release-secrets.sh` sets them once
(it reads the license Unity Hub keeps on the machine, asks for the Unity account, and makes the Android key in
`~/.config/gwentlegacy/`, to back up: a new key would sign APKs that no longer install over the ones already out).
