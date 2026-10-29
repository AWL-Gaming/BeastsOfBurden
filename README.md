# Beasts of Burden

**AWL Gaming maintained compatibility fork for current Valheim releases.**

Beasts of Burden was originally created by **jcleveland**. AWL Gaming maintains this fork because the original 1.0.4 release no longer works with the current Valheim 1.0 API. AWL Gaming does not claim authorship of the upstream implementation or gameplay design.

## What it does

- Lets eligible tamed animals pull the vanilla cart.
- Supports the original tameable follow/command behavior.
- Keeps normal player cart attachment as the fallback.
- Preserves the original BepInEx plugin GUID and configuration compatibility.

## Cart controls

- Use the cart handles normally to hitch the closest eligible tame animal. Use again to unhitch.
- Hold your alternate placement key while using the handles to pull the cart yourself, even when a tame animal is nearby. The cart hover text shows the corresponding controls, including gamepad bindings.

## AWL 1.0.8 maintained release

- Restored each cart's original pulling position and detach distance when animals unhitch or a player takes over.
- Added manual pulling through alternate use without changing normal animal hitching.
- Preserved cart settings across repeated animal attachments and ownership changes.
## Installation

Install on both the server and every client that connects to it. The Thunderstore/Hexium package will install the DLL through your mod manager. For manual installation, place `BeastsOfBurden.dll` under `BepInEx/plugins/`.

## AWL maintenance and support

- AWL Gaming website: https://awlgaming.net
- Maintained source: https://github.com/AWL-Gaming/BeastsOfBurden
- Bug reports for this maintained build: https://github.com/AWL-Gaming/BeastsOfBurden/issues
- Optional support for AWL compatibility maintenance and testing: https://patreon.awlgaming.net

Support is optional and is for AWL's compatibility, testing, packaging, and maintenance work on this fork. The mod remains available regardless of support.

## Original project and attribution

- Original author: jcleveland
- Original source: https://github.com/jcleveland/clevels-valheim-mods/tree/master/BeastsOfBurden
- Original Thunderstore package: https://thunderstore.io/c/valheim/p/clevel/BeastsOfBurden/

The upstream repository did not contain a license file or an explicit redistribution/derivative-work grant when this maintenance fork was prepared. `NOTICE.md` records that status transparently. AWL Gaming does not claim that the absence of a license grants rights.

## Source and build

The maintained source is public in the AWL repository above. Build requirements are .NET Framework 4.7.2 targeting support, BepInEx core assemblies, and current Valheim managed assemblies.

```powershell
dotnet build .\BeastsOfBurden\BeastsOfBurden.csproj -c Release
```