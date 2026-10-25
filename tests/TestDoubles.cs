// Minimal API doubles for resolver regression tests. No game code or assets.
namespace UnityEngine {
    public class Object {
        public string name;
        public bool destroyed;
        public static implicit operator bool(Object value) => value != null && !value.destroyed;
    }
}

namespace UnityEngine.Networking {
    public class NetworkWriter {
        public string[] entitlements;
        public string password;
        public string version;
        public string modHash;
        public object platformId;
        public byte[] authTicket;
        public int calls;
    }
}

namespace RoR2.Networking {
    public class ClientAuthData {
        public string[] entitlements;
        public string password;
        public string version;
        public string modHash;
        public object platformId;
        public byte[] authTicket;

        public void Serialize(UnityEngine.Networking.NetworkWriter writer) {
            writer.entitlements = entitlements?.ToArray();
            writer.password = password;
            writer.version = version;
            writer.modHash = modHash;
            writer.platformId = platformId;
            writer.authTicket = authTicket;
            writer.calls++;
        }
    }
}

namespace MonoMod.RuntimeDetour {
    // Records the production detour; it does not patch methods outside Unity.
    public class Hook : IDisposable {
        public static Hook Current;
        public System.Reflection.MethodBase Target;
        public Delegate Detour;
        public bool Disposed;

        public Hook(System.Reflection.MethodBase target, Delegate detour) {
            Target = target;
            Detour = detour;
            Current = this;
        }

        public void Dispose() => Disposed = true;
    }
}

namespace BepInEx {
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class BepInPlugin : Attribute {
        public BepInPlugin(string id, string name, string version) { }
    }
    public class BaseUnityPlugin {
        public TestLogger Logger { get; } = new TestLogger();
    }
    public class TestLogger {
        public void LogInfo(object value) { }
    }
}

namespace RoR2 {
    public class LocalUser { }
    public class NetworkUser : UnityEngine.Object { }
}

namespace RoR2.ExpansionManagement {
    public class ExpansionDef : UnityEngine.Object {
        public EntitlementManagement.EntitlementDef requiredEntitlement;
    }
}

namespace RoR2.ContentManagement {
    public static class ContentManager {
        public static ExpansionManagement.ExpansionDef[] expansionDefs = [];
    }
}

namespace RoR2.EntitlementManagement {
    public class EntitlementDef : UnityEngine.Object { }
    public interface IUserEntitlementResolver<T> where T : class {
        event Action onEntitlementsChanged;
        bool CheckUserHasEntitlement(T user, EntitlementDef entitlement);
    }
    public static class EntitlementManager {
        public static event Action<Action<IUserEntitlementResolver<LocalUser>>> collectLocalUserEntitlementResolvers;
        public static event Action<Action<IUserEntitlementResolver<NetworkUser>>> collectNetworkUserEntitlementResolvers;

        public static List<IUserEntitlementResolver<LocalUser>> CollectLocal() {
            var result = new List<IUserEntitlementResolver<LocalUser>>();
            collectLocalUserEntitlementResolvers?.Invoke(result.Add);
            return result;
        }
        public static List<IUserEntitlementResolver<NetworkUser>> CollectNetwork() {
            var result = new List<IUserEntitlementResolver<NetworkUser>>();
            collectNetworkUserEntitlementResolvers?.Invoke(result.Add);
            return result;
        }
    }
}
