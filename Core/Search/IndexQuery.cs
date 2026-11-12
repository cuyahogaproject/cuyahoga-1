using System;
using System.Collections;

using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Store;

namespace Cuyahoga.Core.Search
{
	/// <summary>
	/// The IndexQuery class provides functionality to search the Full-Text index.
	/// </summary>
	public class IndexQuery
	{
		private readonly string _physicalIndexDir;

		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="physicalIndexDir">The physical directory where the index is located.</param>
		public IndexQuery(string physicalIndexDir)
		{
			// The index directory isn't part of the source tree, create it on first use.
			System.IO.Directory.CreateDirectory(physicalIndexDir);
			this._physicalIndexDir = physicalIndexDir;
		}

		/// <summary>
		/// Search in the full-text index.
		/// </summary>
		/// <param name="queryText">query text</param>
		/// <param name="keywordFilter">Keyword filter (keyword, value).</param>
		/// <param name="pageIndex">Index of the page with results (zero based).</param>
		/// <param name="pageSize">The number of results on a page.</param>
		/// <returns></returns>
		public SearchResultCollection Find(string queryText, Hashtable keywordFilter, int pageIndex, int pageSize)
		{
			long startTicks = DateTime.Now.Ticks;

			try
			{
				SearchResultCollection results = new SearchResultCollection();
				results.PageIndex = pageIndex;

				using (FSDirectory indexDirectory = FSDirectory.Open(this._physicalIndexDir))
				{
					if (DirectoryReader.IndexExists(indexDirectory))
					{
						using (DirectoryReader reader = DirectoryReader.Open(indexDirectory))
						{
							IndexSearcher searcher = new IndexSearcher(reader);
							MultiFieldQueryParser parser = new MultiFieldQueryParser(IndexBuilder.MatchVersion
								, new string[] { "title", "contents" }, new StandardAnalyzer(IndexBuilder.MatchVersion));
							Query query = parser.Parse(queryText);
							Filter filter = (keywordFilter != null && keywordFilter.Count > 0)
								? BuildFilterFromKeywordFilter(keywordFilter)
								: null;

							int start = pageIndex * pageSize;
							int end = (pageIndex + 1) * pageSize;
							TopDocs topDocs = searcher.Search(query, filter, Math.Max(end, 1));
							results.TotalCount = topDocs.TotalHits;
							end = Math.Min(end, topDocs.ScoreDocs.Length);

							for (int i = start; i < end; i++)
							{
								ScoreDoc scoreDoc = topDocs.ScoreDocs[i];
								Document doc = searcher.Doc(scoreDoc.Doc);
								SearchResult result = new SearchResult();
								result.Title = doc.Get("title");
								result.Summary = doc.Get("summary");
								result.Author = doc.Get("author");
								result.ModuleType = doc.Get("moduletype");
								result.Path = doc.Get("path");
								result.Category = doc.Get("category");
								result.DateCreated = DateTime.Parse(doc.Get("datecreated"));
								result.Score = scoreDoc.Score;
								// Lucene doesn't store document boosts anymore (they were always 1.0 in search results).
								result.Boost = 1.0f;
								result.SectionId = Int32.Parse(doc.Get("sectionid"));
								results.Add(result);
							}
						}
					}
				}
				results.ExecutionTime = DateTime.Now.Ticks - startTicks;

				return results;
			}
			catch (IndexFormatTooOldException ex)
			{
				throw new SearchException("The search index was created with an older version of Lucene. Rebuild the index in the site administration.", ex);
			}
			catch (Exception ex)
			{
				throw new SearchException("Error while performing full-text search.", ex);
			}
		}

		private Filter BuildFilterFromKeywordFilter(Hashtable keywordFilter)
		{
			BooleanQuery bQuery = new BooleanQuery();
			foreach(DictionaryEntry keywordFilterTerm in keywordFilter)
			{
				string field = keywordFilterTerm.Key.ToString();
				string keyword = keywordFilterTerm.Value.ToString();
				bQuery.Add(new TermQuery(new Term(field, keyword)), Occur.MUST);
			}

			return new QueryWrapperFilter(bQuery);
		}
	}
}
