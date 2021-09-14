namespace Mirage.Weaver
{
    public static class HashCodeHelper
    {
        /// <summary>
        /// Use this to get a hash of 2 objects.
        /// <para>This should be more efficient than concatenating 2 strings</para>
        /// </summary>
        /// <param name="obj1"></param>
        /// <param name="obj2"></param>
        /// <returns></returns>
        public static int GetCombineHash<T1, T2>(T1 obj1, T2 obj2)
        {
            // https://stackoverflow.com/a/263416/8479976
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + obj1.GetHashCode();
                hash = hash * 23 + obj2.GetHashCode();
                return hash;
            }
        }
    }
}
