using System;
using System.Collections;
using System.IO;
using Cuyahoga.Core.Search;
using Xunit;

namespace Cuyahoga.Core.Tests.Search
{
	/// <summary>
	/// IndexBuilder and IndexQuery together, on a real Lucene index in a temporary directory.
	/// </summary>
	public sealed class SearchIndexTests : IDisposable
	{
		private readonly TempDirectory _indexDir = new TempDirectory();

		public void Dispose() => _indexDir.Dispose();

		private static SearchContent Content(string path, string title, string contents = "", string moduleType = "Articles") =>
			new SearchContent
			{
				Path = path,
				Title = title,
				Contents = contents,
				Summary = "Summary of " + title,
				ModuleType = moduleType,
				DateCreated = new DateTime(2026, 1, 2, 3, 4, 5),
				DateModified = new DateTime(2026, 1, 2, 3, 4, 5),
				SectionId = 7,
			};

		private void Index(bool rebuild, Action<IndexBuilder> action)
		{
			using (var builder = new IndexBuilder(_indexDir.Path, rebuild))
			{
				action(builder);
			}
		}

		private SearchResultCollection Find(string query, Hashtable filter = null, int pageIndex = 0, int pageSize = 10) =>
			new IndexQuery(_indexDir.Path).Find(query, filter, pageIndex, pageSize);

		[Fact]
		public void Finds_content_by_title_and_contents()
		{
			Index(true, b =>
			{
				b.AddContent(Content("/1", "Cuyahoga news", "A content management system"));
				b.AddContent(Content("/2", "Other page", "Nothing to see"));
			});

			var byTitle = Find("news");
			var byContents = Find("management");

			Assert.Equal(1, byTitle.TotalCount);
			Assert.Equal("Cuyahoga news", byTitle[0].Title);
			Assert.Equal("/1", byTitle[0].Path);
			Assert.Equal("Summary of Cuyahoga news", byTitle[0].Summary);
			Assert.Equal(7, byTitle[0].SectionId);
			Assert.Equal("/1", byContents[0].Path);
		}

		[Fact]
		public void Search_results_have_the_creation_date_of_the_content()
		{
			Index(true, b => b.AddContent(Content("/1", "Dated content")));

			Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5), Find("dated")[0].DateCreated);
		}

		[Fact]
		public void Update_replaces_and_delete_removes_content()
		{
			Index(true, b => b.AddContent(Content("/1", "First title")));

			Index(false, b => b.UpdateContent(Content("/1", "Second title")));
			Assert.Equal(0, Find("first").TotalCount);
			Assert.Equal(1, Find("second").TotalCount);

			Index(false, b => b.DeleteContent(Content("/1", "Second title")));
			Assert.Equal(0, Find("second").TotalCount);
		}

		[Fact]
		public void Rebuilding_starts_with_an_empty_index()
		{
			Index(true, b => b.AddContent(Content("/1", "Old content")));

			Index(true, b => b.AddContent(Content("/2", "New content")));

			Assert.Equal(0, Find("old").TotalCount);
			Assert.Equal(1, Find("new").TotalCount);
		}

		[Fact]
		public void Rebuilding_removes_files_of_an_old_index()
		{
			// Files of a Lucene 1.4 index.
			File.WriteAllText(_indexDir.File("segments"), "old");
			File.WriteAllText(_indexDir.File("_c.cfs"), "old");

			Index(true, b => b.AddContent(Content("/1", "Fresh content")));

			Assert.False(File.Exists(_indexDir.File("_c.cfs")));
			Assert.Equal(1, Find("fresh").TotalCount);
		}

		[Fact]
		public void Keyword_filter_limits_the_results()
		{
			Index(true, b =>
			{
				b.AddContent(Content("/1", "Shared word", moduleType: "Articles"));
				b.AddContent(Content("/2", "Shared word", moduleType: "StaticHtml"));
			});

			var results = Find("shared", new Hashtable { { "moduletype", "StaticHtml" } });

			Assert.Equal(1, results.TotalCount);
			Assert.Equal("/2", results[0].Path);
		}

		[Fact]
		public void Results_are_paged()
		{
			Index(true, b =>
			{
				for (int i = 1; i <= 3; i++)
				{
					b.AddContent(Content("/" + i, "Paged item " + i));
				}
			});

			var secondPage = Find("paged", pageIndex: 1, pageSize: 2);

			Assert.Equal(3, secondPage.TotalCount);
			Assert.Equal(1, secondPage.Count);
			Assert.Equal(1, secondPage.PageIndex);
		}

		[Fact]
		public void Search_without_an_index_finds_nothing()
		{
			Assert.Equal(0, Find("anything").TotalCount);
		}
	}
}
