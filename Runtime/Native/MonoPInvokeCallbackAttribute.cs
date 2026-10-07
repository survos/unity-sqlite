using System;

namespace Survos.Sqlite.Native
{
    // IL2CPP recognizes this attribute by its simple name. Keeping a local definition
    // avoids taking a UnityEngine dependency solely for the AOT marker.
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class MonoPInvokeCallbackAttribute : Attribute
    {
        public MonoPInvokeCallbackAttribute(Type delegateType) { }
    }
}
