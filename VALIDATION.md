# Client DLC reporting

The implementation adds one detour to `ClientAuthData.Serialize`.
Steam and crossplay both use this message to report client entitlements.
The detour adds expansion entitlement names and calls the original serializer.
It preserves existing entitlements and all other authentication fields.

The mod uses `MonoMod.RuntimeDetour.dll` from BepInEx 5.
The package does not include another copy of this library.

## Automated checks

Run the regression harness with the .NET 9 SDK:

```sh
dotnet run --project tests/ResolverTests.csproj -c Release
```

The harness compiles the production source against API doubles.
It compares outgoing entitlements for a client without DLC and a vanilla DLC owner.
It also checks existing entitlements, authentication fields, additional expansions, duplicate entries, resolver behavior, and plugin lifecycle events.
The hook double records the detour. It does not install a runtime detour.
These checks do not prove packet compatibility or gameplay behavior inside Unity.

Build against the installed game and BepInEx assemblies:

```sh
dotnet build csproj/RiskOfRain2_FreeDLC.csproj -c Release \
  -p:GameManagedPath="/path/to/Risk of Rain 2_Data/Managed" \
  -p:BepInExCorePath="/path/to/BepInEx/core"
```

The Release build creates `artifacts/RiskOfRain2_FreeDLC-0.0.2.zip`.

## Live multiplayer checks

Live multiplayer checks remain pending. The implementation does not establish full equivalence until these checks pass.

Use the same game version, profile unlocks, and lobby rules for each comparison.
Use a vanilla DLC owner as the reference client.
Compare the reference client against a client without DLC that runs this mod.

| Environment | Required result |
| --- | --- |
| Vanilla host without DLC, Steam connection | Both clients expose the same expansion choices and can use the same unlocked DLC survivors. |
| Vanilla host with DLC, Steam connection | Both clients can join, spawn, change stages, die, and respawn with the same DLC access. |
| Vanilla host, crossplay connection | Both clients have the same DLC access through the crossplay authentication message. |
| Host and client both run this mod | The session retains the existing local and host entitlement behavior. |
| Host and client run other compatible mods | The normal mod compatibility checks pass without changes to their mod hashes. |
| Client disconnects and reconnects | The host receives the same expansion entitlements on the new connection. |
| Client disables the mod before a new connection | The client reports only its original platform entitlements. |

The host retains control of expansion rules. Entitlements do not override survivor unlock requirements or rules for joining an active run.
Existing connections retain the entitlements from their authentication message. Reconnect after a change to the plugin state.
