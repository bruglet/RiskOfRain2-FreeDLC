using BepInEx;
using MonoMod.RuntimeDetour;
using RoR2;
using RoR2.ContentManagement;
using RoR2.EntitlementManagement;
using RoR2.Networking;
using System;
using System.Collections.Generic;
using UnityEngine.Networking;

namespace RiskOfRain2_FreeDLC {
    [BepInPlugin(MODUID, MODNAME, MODVERSION)]
    public sealed class RiskOfRain2_FreeDLC : BaseUnityPlugin,
        IUserEntitlementResolver<LocalUser>, IUserEntitlementResolver<NetworkUser>
    {
        public const string MODUID = "com.Snoresville.RiskOfRain2_FreeDLC";
        public const string MODNAME = "Risk of Rain 2 - Free DLC";
        public const string MODVERSION = "0.0.2";

        private bool resolverActive;
        private Hook clientAuthHook;

        public event Action onEntitlementsChanged;

        private void Awake() {
            clientAuthHook = new Hook(typeof(ClientAuthData).GetMethod(nameof(ClientAuthData.Serialize)),
                (Action<Action<ClientAuthData, NetworkWriter>, ClientAuthData, NetworkWriter>)SerializeClientAuth);
            EntitlementManager.collectLocalUserEntitlementResolvers += AddLocalResolver;
            EntitlementManager.collectNetworkUserEntitlementResolvers += AddNetworkResolver;
            Logger.LogInfo("Registered expansion entitlement resolvers.");
        }

        private void OnEnable() {
            resolverActive = true;
            onEntitlementsChanged?.Invoke();
        }

        private void OnDisable() {
            resolverActive = false;
            // Trackers retain collected resolvers. Refresh their caches when disabled.
            onEntitlementsChanged?.Invoke();
        }

        private void OnDestroy() {
            clientAuthHook?.Dispose();
            EntitlementManager.collectLocalUserEntitlementResolvers -= AddLocalResolver;
            EntitlementManager.collectNetworkUserEntitlementResolvers -= AddNetworkResolver;
        }

        private void AddLocalResolver(Action<IUserEntitlementResolver<LocalUser>> add) {
            add(this);
        }

        private void AddNetworkResolver(Action<IUserEntitlementResolver<NetworkUser>> add) {
            add(this);
        }

        private void SerializeClientAuth(Action<ClientAuthData, NetworkWriter> orig, ClientAuthData authData, NetworkWriter writer) {
            if (resolverActive) {
                // Use the existing auth message so vanilla hosts receive the same DLC names as for an owner.
                var entitlements = new List<string>(authData.entitlements ?? Array.Empty<string>());
                foreach (var expansion in ContentManager.expansionDefs) {
                    if (expansion && expansion.requiredEntitlement) {
                        var name = expansion.requiredEntitlement.name;
                        if (!entitlements.Contains(name)) {
                            entitlements.Add(name);
                        }
                    }
                }
                authData.entitlements = entitlements.ToArray();
            }

            orig(authData, writer);
        }

        bool IUserEntitlementResolver<LocalUser>.CheckUserHasEntitlement(LocalUser user, EntitlementDef entitlement) {
            return user != null && HasExpansionEntitlement(entitlement);
        }

        bool IUserEntitlementResolver<NetworkUser>.CheckUserHasEntitlement(NetworkUser user, EntitlementDef entitlement) {
            return user && HasExpansionEntitlement(entitlement);
        }

        private bool HasExpansionEntitlement(EntitlementDef entitlement) {
            if (!resolverActive || !entitlement) {
                return false;
            }

            // Content loads before resolver collection. Avoid catalog initializer order.
            foreach (var expansion in ContentManager.expansionDefs) {
                if (expansion && expansion.requiredEntitlement == entitlement) {
                    return true;
                }
            }

            return false;
        }
    }
}
