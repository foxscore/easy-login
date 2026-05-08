using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Foxscore.EasyLogin
{
    public static class Is
    {
        private static readonly object Lock = new();
        private static readonly ConcurrentDictionary<string, bool> Seen = new();

        public static bool FirstRun(object instance = null)
        {
            lock (Lock)
            {
                var method = new StackFrame(1, false).GetMethod();
                if (method is null) return true;

                var methodKey = $"{method.DeclaringType?.FullName}.{method.Name}("
                                + string.Join(",", method.GetParameters().Select(p => p.ParameterType.FullName))
                                + ")";

                // For instance methods, mix in the object's identity.
                // RuntimeHelpers.GetHashCode gives the original identity hash even if
                // GetHashCode() is overridden, and doesn't prevent GC (no strong ref stored).
                var key = instance is not null
                    ? $"{methodKey}@{RuntimeHelpers.GetHashCode(instance)}"
                    : methodKey;

                return Seen.TryAdd(key, true);
            }
        }
    }
}