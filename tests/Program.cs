using System.Reflection;
using MonoMod.RuntimeDetour;
using RoR2;
using RoR2.ContentManagement;
using RoR2.EntitlementManagement;
using RoR2.ExpansionManagement;
using RoR2.Networking;
using UnityEngine.Networking;
using Plugin = RiskOfRain2_FreeDLC.RiskOfRain2_FreeDLC;

// These tests exercise the production source against API doubles, outside Unity.
int checks = 0;
void Check(bool condition, string description) {
    if (!condition) throw new Exception(description);
    checks++;
    Console.WriteLine($"PASS {description}");
}
void Lifecycle(Plugin plugin, string method) => typeof(Plugin)
    .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(plugin, null);

var entitlement = new EntitlementDef { name = "ExpansionOne" };
var expansion = new ExpansionDef { requiredEntitlement = entitlement };
ContentManager.expansionDefs = [expansion];
var plugin = new Plugin();
Lifecycle(plugin, "Awake");
Lifecycle(plugin, "OnEnable");
Check(Hook.Current.Target.Equals(typeof(ClientAuthData).GetMethod(nameof(ClientAuthData.Serialize))), "detour targets the native client auth serializer");
var serializeAuth = (Action<Action<ClientAuthData, NetworkWriter>, ClientAuthData, NetworkWriter>)Hook.Current.Detour;
NetworkWriter SendAuth(ClientAuthData auth) {
    var writer = new NetworkWriter();
    serializeAuth((data, output) => data.Serialize(output), auth, writer);
    return writer;
}

var ownerAuth = new ClientAuthData { entitlements = [entitlement.name], version = "game", modHash = "vanilla" };
var ownerPacket = new NetworkWriter();
ownerAuth.Serialize(ownerPacket);
var clientAuth = new ClientAuthData {
    entitlements = [], version = ownerAuth.version, modHash = ownerAuth.modHash,
    password = "password", platformId = new object(), authTicket = [1, 2, 3]
};
var clientPacket = SendAuth(clientAuth);
Check(clientPacket.entitlements.SequenceEqual(ownerPacket.entitlements), "client without DLC reports the same expansion as a vanilla owner");
Check(clientPacket.calls == 1, "original auth serializer runs exactly once");
Check(clientPacket.version == clientAuth.version && clientPacket.modHash == clientAuth.modHash &&
    clientPacket.password == clientAuth.password && ReferenceEquals(clientPacket.platformId, clientAuth.platformId) &&
    ReferenceEquals(clientPacket.authTicket, clientAuth.authTicket), "identity, auth ticket, password, game version and mod hash are preserved");
Check(SendAuth(clientAuth).entitlements.SequenceEqual(ownerPacket.entitlements), "repeated serialization does not add duplicate DLC entries");
var platformOwned = new ClientAuthData { entitlements = ["OtherEntitlement", entitlement.name] };
var ownedArray = platformOwned.entitlements;
Check(SendAuth(platformOwned).entitlements.SequenceEqual(ownedArray), "existing platform and third-party entitlements are preserved");
Check(ownedArray.SequenceEqual(["OtherEntitlement", entitlement.name]), "the original platform-owned array is not mutated");
Check(SendAuth(new ClientAuthData()).entitlements.SequenceEqual(ownerPacket.entitlements), "a missing platform entitlement list is supported");
var locals = EntitlementManager.CollectLocal();
var networks = EntitlementManager.CollectNetwork();
Check(locals.Count == 1 && ReferenceEquals(locals[0], plugin), "local resolver registration");
Check(networks.Count == 1 && ReferenceEquals(networks[0], plugin), "network resolver registration");
var local = locals.Single();
var network = networks.Single();
var localUser = new LocalUser();
var networkUser = new NetworkUser();
Check(local.CheckUserHasEntitlement(localUser, entitlement), "expansion without platform ownership is available locally");
Check(network.CheckUserHasEntitlement(networkUser, entitlement), "expansion without platform ownership is available on the host");
Check(ReferenceEquals(expansion.requiredEntitlement, entitlement), "expansion entitlement stays intact");
Check(!local.CheckUserHasEntitlement(null, entitlement), "missing local user is rejected");
Check(!network.CheckUserHasEntitlement(null, entitlement), "missing network user is rejected");
Check(!network.CheckUserHasEntitlement(new NetworkUser { destroyed = true }, entitlement), "destroyed network user is rejected");
Check(!local.CheckUserHasEntitlement(localUser, null), "missing entitlement is rejected");
Check(!local.CheckUserHasEntitlement(localUser, new EntitlementDef()), "unrelated entitlement is rejected");
entitlement.destroyed = true;
Check(!local.CheckUserHasEntitlement(localUser, entitlement), "destroyed entitlement is rejected");
entitlement.destroyed = false;
ContentManager.expansionDefs = [];
Check(!local.CheckUserHasEntitlement(localUser, entitlement), "empty content does not grant entitlements");
Check(SendAuth(new ClientAuthData { entitlements = ["PlatformOwned"] }).entitlements.SequenceEqual(["PlatformOwned"]), "empty expansion content preserves platform entitlements");
ContentManager.expansionDefs = [null, new ExpansionDef(), new ExpansionDef { destroyed = true, requiredEntitlement = entitlement }];
Check(!local.CheckUserHasEntitlement(localUser, entitlement), "missing and destroyed expansions do not grant entitlements");
var additionalEntitlement = new EntitlementDef { name = "FutureExpansion" };
ContentManager.expansionDefs = [expansion, new ExpansionDef { requiredEntitlement = additionalEntitlement }];
Check(local.CheckUserHasEntitlement(localUser, additionalEntitlement), "additional expansion needs no hardcoded DLC identifier");
ContentManager.expansionDefs = [null, new ExpansionDef(), expansion, expansion,
    new ExpansionDef { destroyed = true, requiredEntitlement = new EntitlementDef { name = "InvalidExpansion" } },
    new ExpansionDef { requiredEntitlement = new EntitlementDef { destroyed = true, name = "InvalidEntitlement" } },
    new ExpansionDef { requiredEntitlement = additionalEntitlement }];
var futurePacket = SendAuth(new ClientAuthData { entitlements = ["OtherEntitlement"] });
Check(futurePacket.entitlements.SequenceEqual(["OtherEntitlement", entitlement.name, additionalEntitlement.name]), "outgoing list includes future expansions once and skips missing or destroyed definitions");
var moddedPacket = SendAuth(new ClientAuthData { entitlements = [], modHash = "other-compatible-mods" });
Check(moddedPacket.modHash == "other-compatible-mods" && moddedPacket.entitlements.Contains(entitlement.name), "modded host compatibility hash is preserved while reporting DLC");
int notifications = 0;
local.onEntitlementsChanged += () => notifications++;
Lifecycle(plugin, "OnDisable");
Check(notifications == 1, "disable notifies entitlement trackers");
Check(!local.CheckUserHasEntitlement(localUser, entitlement) && !network.CheckUserHasEntitlement(networkUser, entitlement), "disabled resolver grants no entitlements");
var disabledAuth = new ClientAuthData { entitlements = ["PlatformOwned"] };
var disabledArray = disabledAuth.entitlements;
Check(SendAuth(disabledAuth).entitlements.SequenceEqual(["PlatformOwned"]) &&
    ReferenceEquals(disabledAuth.entitlements, disabledArray), "disabled plugin leaves outgoing entitlements unchanged");
Lifecycle(plugin, "OnEnable");
Check(notifications == 2 && local.CheckUserHasEntitlement(localUser, entitlement), "enable notifies trackers and restores the resolver");
Check(SendAuth(new ClientAuthData { entitlements = [] }).entitlements.Contains(entitlement.name), "re-enabling restores outgoing DLC reporting");
Lifecycle(plugin, "OnDisable");
Lifecycle(plugin, "OnDestroy");
Check(EntitlementManager.CollectLocal().Count == 0 && EntitlementManager.CollectNetwork().Count == 0, "destroy removes collection callbacks");
Check(!network.CheckUserHasEntitlement(networkUser, entitlement), "retained resolver stays inactive after teardown");
Check(Hook.Current.Disposed, "destroy disposes the auth detour");
Console.WriteLine($"{checks} checks passed. Unity and multiplayer runtime behavior remain untested.");
