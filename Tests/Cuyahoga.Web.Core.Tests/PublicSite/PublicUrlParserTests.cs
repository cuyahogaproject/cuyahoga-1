using Cuyahoga.Web.Core.PublicSite;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;

namespace Cuyahoga.Web.Core.Tests.PublicSite
{
	public class PublicUrlParserTests
	{
		private static PageRequest Parse(string url)
		{
			int queryStart = url.IndexOf('?');
			string path = queryStart < 0 ? url : url.Substring(0, queryStart);
			string queryString = queryStart < 0 ? String.Empty : url.Substring(queryStart);
			return PublicUrlParser.Parse(path, queryString, QueryHelpers.ParseQuery(queryString));
		}

		[Theory]
		[InlineData("/")]
		[InlineData("/Default.aspx")]
		[InlineData("/default.aspx")]
		public void The_home_page_has_no_parameters(string url)
		{
			Assert.Equal(new PageRequest(null, null, null, ""), Parse(url));
		}

		[Fact]
		public void Default_aspx_takes_the_parameters_from_the_query_string()
		{
			Assert.Equal(new PageRequest(5, 3, "about", ""), Parse("/Default.aspx?NodeId=5&SectionId=3&ShortDescription=about"));
		}

		[Fact]
		public void Node_urls()
		{
			Assert.Equal(new PageRequest(7, null, null, ""), Parse("/7/view.aspx"));
			Assert.Equal(new PageRequest(7, null, null, "/2"), Parse("/7/view.aspx/2"));
		}

		[Fact]
		public void Section_urls_keep_the_path_info_for_the_module()
		{
			Assert.Equal(new PageRequest(null, 12, null, ""), Parse("/12/section.aspx"));
			Assert.Equal(new PageRequest(null, 12, null, "/archive/2"), Parse("/12/Section.aspx/archive/2"));
			Assert.Equal(new PageRequest(null, 12, null, "/download/45"), Parse("/12/section.aspx/download/45?x=1"));
		}

		[Fact]
		public void Friendly_urls_have_the_short_description_of_the_node()
		{
			Assert.Equal(new PageRequest(null, null, "page2", ""), Parse("/page2.aspx"));
			Assert.Equal(new PageRequest(null, null, "about/our-team", ""), Parse("/about/our-team.aspx"));
		}

		[Fact]
		public void Query_string_parameters_are_added_to_the_ones_of_the_path()
		{
			Assert.Equal(new PageRequest(null, 3, "page2", ""), Parse("/page2.aspx?SectionId=3"));
		}

		[Theory]
		// Not public pages, or handled by the WebForms site itself.
		[InlineData("/Login.aspx")]
		[InlineData("/Profile.aspx")]
		[InlineData("/Rss.aspx?SectionId=3")]
		[InlineData("/Error.aspx")]
		[InlineData("/Admin/Modules.aspx")]
		[InlineData("/Modules/StaticHtml/EditHtml.aspx?NodeId=1&SectionId=2")]
		[InlineData("/Install/Install.aspx")]
		[InlineData("/3/feed.aspx")]
		[InlineData("/Templates/AnotherRed/Css/red.css")]
		[InlineData("/sitemap.xml")]
		[InlineData("/CurrentUser.axd")]
		// The WebForms site doesn't rewrite these (or rewrites them into a broken url); it keeps handling them.
		[InlineData("/modules-overview.aspx")]
		[InlineData("/page2.aspx?return=/Admin")]
		[InlineData("/5/12/view.aspx")]
		[InlineData("/a.b/page.aspx")]
		// Invalid or ambiguous parameters (an error page on the WebForms site).
		[InlineData("/Default.aspx?NodeId=abc")]
		[InlineData("/Default.aspx?NodeId=")]
		[InlineData("/7/view.aspx?NodeId=8")]
		[InlineData("/Default.aspx?SectionId=1&SectionId=2")]
		public void Other_urls_are_left_to_the_WebForms_site(string url)
		{
			Assert.Null(Parse(url));
		}
	}
}
