Run this check on native Windows with the built BeastsOfBurden DLL, the actual Valheim Managed directory, the BepInEx core directory, and a report path:

```powershell
dotnet run --project tools\NativeOwnershipRegression\NativeOwnership.csproj --configuration Release -- "C:\Build\BeastsOfBurden.dll" "C:\Valheim\valheim_Data\Managed" "C:\BepInEx\core" "C:\Build\ownership-result.json"
```

The check executes the compiled cart update prefix with native managed cart, requester, network-view and ZDO types. It verifies that missing, invalid and non-owning views leave a real pending request intact. It does not initialize a Unity scene, activate Harmony patches, or modify a world. Multiplayer ownership transfer, attachment and detachment still require a real game check.