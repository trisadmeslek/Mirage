using System.Collections.Generic;
using Mono.Cecil;

namespace Mirage.Weaver
{
    internal class FieldReferenceComparator : IEqualityComparer<FieldReference>
    {
        public static bool Fast;
        public bool Equals(FieldReference x, FieldReference y)
        {
            if (Fast)
            {
                return x.Name == y.Name && x.DeclaringType.FullName == y.DeclaringType.FullName;
            }
            else
            {
                return x.DeclaringType.FullName == y.DeclaringType.FullName && x.Name == y.Name;
            }
        }

        public int GetHashCode(FieldReference obj)
        {
            if (Fast)
            {
                return HashCodeHelper.GetCombineHash(obj.Name, obj.DeclaringType.FullName);
            }
            else
            {
                return (obj.DeclaringType.FullName + "." + obj.Name).GetHashCode();
            }
        }
    }
}
