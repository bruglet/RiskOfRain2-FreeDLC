using BepInEx;
using RoR2;
using RoR2.ContentManagement;
using RoR2.EntitlementManagement;
using System;

namespace RiskOfRain2_FreeDLC {
    [BepInPlugin(MODUID, MODNAME, MODVERSION)]
    public sealed class RiskOfRain2_FreeDLC : BaseUnityPlugin,
        IUserEntitlementResolver<LocalUser>, IUserEntitlementResolver<NetworkUser>
    {
        public const string MODUID = "com.Snoresville.RiskOfRain2_FreeDLC";
        public const string MODNAME = "Risk of Rain 2 - Free DLC";
        public const string MODVERSION = "0.0.2";

        private bool resolverActive;

        public event Action onEntitlementsChanged;

        private void Awake() {
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
            EntitlementManager.collectLocalUserEntitlementResolvers -= AddLocalResolver;
            EntitlementManager.collectNetworkUserEntitlementResolvers -= AddNetworkResolver;
        }

        private void AddLocalResolver(Action<IUserEntitlementResolver<LocalUser>> add) {
            add(this);
        }

        private void AddNetworkResolver(Action<IUserEntitlementResolver<NetworkUser>> add) {
            add(this);
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
