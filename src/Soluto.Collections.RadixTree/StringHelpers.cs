namespace Soluto.Collections
{
    public static class StringHelpers
    {
        /// <summary>
        /// finds the length of the shared prefix of two strings
        /// </summary>
        /// <param name="str1">first string to compare</param>
        /// <param name="str2">secont string to compare</param>
        /// <returns>length of shared prefix</returns>
        public static int FindLongestPrefix(string str1, string str2)
        {
            var max = str1.Length > str2.Length ? str2.Length : str1.Length;

            for (var i = 0; i < max; i++)
                if (str1[i] != str2[i])
                    return i;

            return max;
        }
    }
}
