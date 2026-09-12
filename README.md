# Beasts of Burden

AWL Gaming maintains this compatibility fork of jcleveland's Beasts of Burden for current Valheim versions.

The mod keeps the original gameplay purpose: compatible tamed animals can pull the vanilla cart, and supported tameables can be commanded and handled as beasts of burden. The original BepInEx plugin GUID is preserved for configuration and mod compatibility.

## AWL maintenance release

Version 1.0.5 updates the original 1.0.4 implementation for the current Valheim 1.0 API while preserving the existing cart and tameable behavior.

Validated on the current AWL Valheim 1.0 stack with BepInEx 5.4.23.5. The runtime functional test verified that an eligible tamed boar is accepted by `Vagon.CanAttach`, the cart creates a live joint to the boar, and the animal attachment is not treated as a player cart-in-use state.

## Build

Requirements:

- .NET SDK capable of targeting .NET Framework 4.7.2
- Current BepInEx core assemblies
- Current Valheim dedicated-server managed assemblies

Set either MSBuild properties or environment variables:

- `BepInExCoreDir` or `BEPINEX_CORE_DIR`: directory containing `BepInEx.dll` and `0Harmony.dll`
- `ValheimManagedDir` or `VALHEIM_MANAGED_DIR`: Valheim `valheim_server_Data\Managed` directory

Then run:

```powershell
dotnet build .\BeastsOfBurden\BeastsOfBurden.csproj -c Release
```

## Upstream and rights

Original project: https://github.com/jcleveland/clevels-valheim-mods/tree/master/BeastsOfBurden

The upstream repository did not contain a license file or explicit redistribution terms when this maintenance fork was prepared. See `NOTICE.md` for provenance and the exact rights notice. AWL Gaming does not claim authorship of the upstream implementation.
