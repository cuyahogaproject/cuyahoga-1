using Cuyahoga.Core.Util;
using Xunit;

namespace Cuyahoga.Core.Tests.Util
{
	public class TextTests
	{
		[Theory]
		[InlineData("http://example.com")]
		[InlineData("HTTPS://example.com/page?x=1")]
		[InlineData("mailto:someone@example.com")]
		[InlineData("ftp://example.com/file")]
		[InlineData("/home.aspx")]
		[InlineData("page.aspx")]
		[InlineData("?q=search")]
		[InlineData("#top")]
		[InlineData("folder/page:with-colon")]
		public void Safe_urls(string url)
		{
			Assert.True(Text.IsSafeUrl(url));
		}

		[Theory]
		[InlineData(null)]
		[InlineData("")]
		[InlineData("   ")]
		[InlineData("javascript:alert(1)")]
		[InlineData("JavaScript:alert(1)")]
		[InlineData(" javascript:alert(1)")]
		[InlineData("java\tscript:alert(1)")]
		[InlineData("java\nscript:alert(1)")]
		[InlineData("vbscript:msgbox(1)")]
		[InlineData("data:text/html,<script>alert(1)</script>")]
		public void Unsafe_urls(string url)
		{
			Assert.False(Text.IsSafeUrl(url));
		}

		[Theory]
		[InlineData("<p>Hello <b>world</b></p>", "Hello world")]
		[InlineData("<script>alert(1)</script>Hi<style>p{}</style>", "Hi")]
		[InlineData("before<!-- comment -->after", "before after")]
		[InlineData("&lt;b&gt; &amp; &quot;", "<b> & \"")]
		[InlineData("a < b and c > d", "a < b and c > d")]
		public void StripHtml(string html, string expected)
		{
			Assert.Equal(expected, Text.StripHtml(html));
		}

		[Fact]
		public void StripHtml_of_null_is_null()
		{
			Assert.Null(Text.StripHtml(null));
		}

		[Theory]
		[InlineData("one two three", 5, "one two...")]
		[InlineData("short", 10, "short")]
		[InlineData("nospacesafterthelimit", 5, "nospacesafterthelimit")]
		[InlineData("<b>bold</b> text", 50, " bold  text")]
		public void TruncateText(string text, int length, string expected)
		{
			Assert.Equal(expected, Text.TruncateText(text, length));
		}

		[Theory]
		[InlineData("/files", "/files/")]
		[InlineData("/files/", "/files/")]
		public void EnsureTrailingSlash(string path, string expected)
		{
			Assert.Equal(expected, Text.EnsureTrailingSlash(path));
		}
	}
}
