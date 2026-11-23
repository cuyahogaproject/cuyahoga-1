using Cuyahoga.Web.Mvc;
using Xunit;

namespace Cuyahoga.Web.Core.Tests.PublicSite
{
	public class UrlPagerTests
	{
		private const string SectionUrl = "/12/section.aspx";

		private static string Describe(UrlPager pager)
		{
			return String.Join(" ", pager.GetLinks().Select(link => link.Url == null ? $"[{link.Text}]" : link.Text));
		}

		[Fact]
		public void First_page_of_the_node_url()
		{
			var pager = new UrlPager("/page1.aspx", SectionUrl, 12, 5, hideWhenOnePage: true);

			Assert.Equal(0, pager.CurrentPageIndex);
			Assert.Equal(3, pager.PageCount);
			Assert.Equal("[1] 2 3 > >>", Describe(pager));
			Assert.Equal("/12/section.aspx/page/2", pager.GetLinks()[1].Url);
			Assert.Equal(new[] { 1, 2, 3, 4, 5 }, pager.GetPage(Enumerable.Range(1, 12)));
		}

		[Fact]
		public void Page_from_the_path_info()
		{
			var pager = new UrlPager("/12/section.aspx/category/3/page/3", SectionUrl, 12, 5, hideWhenOnePage: true);

			Assert.Equal(2, pager.CurrentPageIndex);
			Assert.Equal("<< < 1 2 [3]", Describe(pager));
			// The links keep the path info of the request (the category).
			Assert.Equal("/12/section.aspx/category/3/page/1", pager.GetLinks()[0].Url);
			Assert.Equal(new[] { 11, 12 }, pager.GetPage(Enumerable.Range(1, 12)));
		}

		[Fact]
		public void Page_from_the_query_string()
		{
			var pager = new UrlPager("/12/section.aspx/Search?q=a%20b&page=2", SectionUrl, 12, 5, hideWhenOnePage: false,
				PagerUrlMode.QueryString);

			Assert.Equal(1, pager.CurrentPageIndex);
			Assert.Equal("<< < 1 [2] 3 > >>", Describe(pager));
			Assert.Equal("/12/section.aspx/Search?q=a%20b&page=3", pager.GetLinks()[5].Url);
			Assert.Equal("/12/section.aspx/Search?page=2",
				new UrlPager("/12/section.aspx/Search", SectionUrl, 12, 5, false, PagerUrlMode.QueryString).GetPageUrl(1));
		}

		[Fact]
		public void One_page_hides_its_number()
		{
			Assert.Equal("", Describe(new UrlPager(SectionUrl, SectionUrl, 3, 5, hideWhenOnePage: true)));
			Assert.Equal("", Describe(new UrlPager(SectionUrl, SectionUrl, 0, 5, hideWhenOnePage: true)));
			Assert.Equal("[1]", Describe(new UrlPager(SectionUrl, SectionUrl, 3, 5, hideWhenOnePage: false)));
		}

		[Fact]
		public void Page_size_zero_shows_everything()
		{
			var pager = new UrlPager(SectionUrl, SectionUrl, 12, 0, hideWhenOnePage: true);

			Assert.Equal("", Describe(pager));
			Assert.Equal(12, pager.GetPage(Enumerable.Range(1, 12)).Count());
		}

		[Fact]
		public void Page_groups_of_ten_numbers()
		{
			var pager = new UrlPager(SectionUrl + "/page/12", SectionUrl, 250, 10, hideWhenOnePage: true);

			Assert.Equal("<< < ... 11 [12] 13 14 15 16 17 18 19 20 ... > >>", Describe(pager));
		}

		[Fact]
		public void The_last_group_shows_one_number_more()
		{
			// Like the Pager control of the WebForms site.
			var pager = new UrlPager(SectionUrl + "/page/24", SectionUrl, 250, 10, hideWhenOnePage: true);

			Assert.Equal("<< < ... 15 16 17 18 19 20 21 22 23 [24] 25 > >>", Describe(pager));
		}

		[Fact]
		public void A_page_past_the_last_one_is_empty()
		{
			var pager = new UrlPager(SectionUrl + "/page/9", SectionUrl, 12, 5, hideWhenOnePage: true);

			Assert.Empty(pager.GetPage(Enumerable.Range(1, 12)));
		}

		[Fact]
		public void Page_zero_is_the_first_page()
		{
			Assert.Equal(0, new UrlPager(SectionUrl + "/page/0", SectionUrl, 12, 5, hideWhenOnePage: true).CurrentPageIndex);
		}
	}
}
