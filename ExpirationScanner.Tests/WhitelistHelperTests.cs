using ExpirationScanner.Logic;
using FluentAssertions;
using NUnit.Framework;

namespace ExpirationScanner.Tests
{
    public class WhitelistHelperTests
    {
        [TestCase("Foo", "Foo", true)]
        [TestCase("Foo-bar", "Foo-*", true)]
        [TestCase("Foo", "Foo-*", false)]
        [TestCase("Foo", "Foo*", false)]
        [TestCase("Foo1", "Foo*", true)]
        public void MatchingSingleValueShouldWorkAsExpected(string input, string pattern, bool expected)
        {
            WhitelistHelper.Matches(input, new[] { pattern }).Should().Be(expected);
        }

        [TestCase("Foo", new[] { "Foo", "Bar" }, true)]
        [TestCase("Foo", new[] { "F*", "Bar" }, true)]
        [TestCase("Foo", new[] { "Foo*", "Bar" }, false)]
        [TestCase("App-DEV", new[] { "*-DEV", "Foo" }, true)]
        [TestCase("App-DEV", new[] { "*-dev", "Foo" }, false)]
        public void MatchingMultipleValuesShouldWorkAsExpected(string input, string[] pattern, bool expected)
        {
            WhitelistHelper.Matches(input, pattern).Should().Be(expected);
        }

        [TestCase("Foo", "foo")]
        [TestCase("Foo", "FOO")]
        [TestCase("Foo", "f*")]
        [TestCase("Foo", "*OO")]
        public void ShouldNotMatchWhenCaseIsWrong(string input, string pattern)
        {
            WhitelistHelper.Matches(input, new[] { pattern }).Should().Be(false);
        }

        [TestCase("Foo", "foo")]
        [TestCase("Foo", "f*")]
        [TestCase("Foo", "FOO")]
        public void ShouldMatchWhenCaseIsIgnored(string input, string pattern)
        {
            WhitelistHelper.Matches(input, new[] { pattern }, true).Should().Be(true);
        }

        [TestCase("Foo", new[] { "*-dev", "Foo" })]
        [TestCase("App-DEV", new[] { "*-dev", "Foo" })]
        [TestCase("App-PRD", new[] { "*-dev", "*-prd" })]
        public void ShouldMatchWhenCaseIsIgnoredForMultiple(string input, string[] pattern)
        {
            WhitelistHelper.Matches(input, pattern, true).Should().Be(true);
        }

        [TestCase("App-PRD", new[] { "*-dev", "Foo" })]
        [TestCase("App-DEV", new[] { "*-dev", "Foo" })]
        [TestCase("App-PRD", new[] { "*-dev", "PRD" })]
        public void ShouldNotMatchWhenCaseIsWrongForMultiple(string input, string[] pattern)
        {
            WhitelistHelper.Matches(input, pattern).Should().Be(false);
        }
    }
}
