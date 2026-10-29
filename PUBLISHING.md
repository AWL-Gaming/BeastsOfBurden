# Publishing BeastsOfBurden

## Public identities

- GitHub: https://github.com/AWL-Gaming/BeastsOfBurden
- Thunderstore: AWLGaming/BeastsOfBurden in the Valheim community
- Hexium: https://valheim.hexium.gg/mods/AWLGaming/BeastsOfBurden
- AWL Gaming: https://awlgaming.net
- Optional AWL maintenance support: https://patreon.awlgaming.net

This is an AWL-maintained compatibility release of the original Beasts of Burden by jcleveland. Keep original-author attribution, upstream links, NOTICE.md, and license/rights status in every public package.

## Current version

1.0.8

## Package requirements

The package root must contain manifest.json, README.md, icon.png, CHANGELOG.md, NOTICE.md, and BeastsOfBurden.dll. Do not add a license that upstream did not grant.

Thunderstore namespace: AWLGaming. Hexium namespace: AWLGaming. Install scope: Client & Server.

Before publication, verify the rendered README, dependency versions, package version, icon, source link, AWL website/support links, attribution, NOTICE/license status, and public download.

Thunderstore and Hexium versions are immutable. Bump the package/plugin version for any later binary or package-content update after a version has been published there.
## Building release packages

Build the portable project first, then run:

~~~powershell
.\scripts\Package-Release.ps1
~~~

The default DLL path is BeastsOfBurden/bin/Release/net472/BeastsOfBurden.dll.
-BinaryPath can select a separately validated build; -ExpectedBinarySha256
guards its identity.

The script creates three ZIPs in artifacts/<version>/:
- GitHub/manual: extract into the Valheim or dedicated-server root; the DLL is under BepInEx/plugins/BeastsOfBurden/.
- Thunderstore: package metadata and DLL at the root.
- Hexium: package metadata and DLL at the root, with the maintained FAQ. The direct BepInEx dependency is omitted because Hexium provides it.

ZIP entry paths use forward slashes. Never publish temporary test helpers,
game assemblies, credentials, logs, or saves.

Hexium uses the AWLGaming team at https://valheim.hexium.gg/mods/AWLGaming/BeastsOfBurden.
Use the current documented API at https://valheim.hexium.gg/api/docs/;
the old hexium.gg experimental submission endpoint is obsolete.