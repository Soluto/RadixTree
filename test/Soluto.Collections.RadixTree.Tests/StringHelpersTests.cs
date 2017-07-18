using Xunit;

namespace Soluto.Collections.RadixTree.Tests
{
    public class StringHelpersTests
    {
        [Theory(DisplayName = "should find longest prefix")]
        [InlineData("abcdef", "abcdef", 6)]
        [InlineData("abcdef", "abcxyz", 3)]
        [InlineData("abc", "abcxyz", 3)]
        [InlineData("abcxyz", "abc", 3)]
        [InlineData("abc", "xyz", 0)]
        [InlineData("abc", "xyzabc", 0)]
        [InlineData("xyzabc", "abc", 0)]
        public void TestFindLongestPrefix(string str1, string str2, int expected)
        {
            Assert.Equal(expected, StringHelpers.FindLongestPrefix(str1, str2));
        }
    }
}
