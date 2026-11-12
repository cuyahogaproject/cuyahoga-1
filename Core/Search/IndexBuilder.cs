using System;
using System.Globalization;
using System.IO;

using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Store;
using Lucene.Net.Util;
using log4net;

using Cuyahoga.Core.Domain;

namespace Cuyahoga.Core.Search
{
	/// <summary>
	/// The IndexBuilder class takes care of maintaining the fulltext index.
	/// </summary>
	public class IndexBuilder : IDisposable
	{
		private static readonly ILog log = LogManager.GetLogger(typeof(IndexBuilder));

		/// <summary>
		/// The Lucene version for the analyzers and the index (the same for writing and querying).
		/// </summary>
		internal const LuceneVersion MatchVersion = LuceneVersion.LUCENE_48;

		private readonly string _physicalIndexDir;
		private Lucene.Net.Store.Directory _indexDirectory;
		private IndexWriter _indexWriter;
		private bool _isClosed = false;
		private bool _rebuildIndex;

		/// <summary>
		/// Default constructor.
		/// </summary>
		/// <param name="physicalIndexDir">Location of the index files.</param>
		/// <param name="rebuildIndex">Flag to indicate if the index should be rebuilt.
		/// NOTE: you can not update or delete content when rebuilding the index.
		/// </param>
		public IndexBuilder(string physicalIndexDir, bool rebuildIndex)
		{
			this._physicalIndexDir = physicalIndexDir;
			this._rebuildIndex = rebuildIndex;
			// The index directory isn't part of the source tree, create it on first use.
			System.IO.Directory.CreateDirectory(physicalIndexDir);
			if (rebuildIndex)
			{
				// Start with an empty directory, also when it contains an index of an older Lucene version.
				DeleteIndexFiles();
			}
			this._indexDirectory = FSDirectory.Open(physicalIndexDir);

			InitIndexWriter();

			log.Info("IndexBuilder created.");
		}

		/// <summary>
		/// Add content to be indexed.
		/// </summary>
		/// <param name="searchContent"></param>
		public void AddContent(SearchContent searchContent)
		{
			if (this._indexWriter == null)
			{
				InitIndexWriter();
			}
			this._indexWriter.AddDocument(BuildDocumentFromSearchContent(searchContent));
		}

		/// <summary>
		/// Update existing content in the search index.
		/// </summary>
		/// <param name="searchContent"></param>
		public void UpdateContent(SearchContent searchContent)
		{
			if (this._rebuildIndex)
			{
				throw new InvalidOperationException("Cannot update documents when rebuilding the index.");
			}
			// The path uniquely identifies a document in the index.
			this._indexWriter.UpdateDocument(new Term("path", searchContent.Path), BuildDocumentFromSearchContent(searchContent));
		}

		/// <summary>
		/// Delete existing content from the search index.
		/// </summary>
		/// <param name="searchContent"></param>
		public void DeleteContent(SearchContent searchContent)
		{
			if (this._rebuildIndex)
			{
				throw new InvalidOperationException("Cannot delete documents when rebuilding the index.");
			}
			// The path uniquely identifies a document in the index.
			this._indexWriter.DeleteDocuments(new Term("path", searchContent.Path));
		}

		/// <summary>
		/// Rebuild the full-text index.
		/// </summary>
		/// <param name="module"></param>
		public void UpdateContentFromModule(ModuleBase module)
		{
			ISearchable searchableModule = module as ISearchable;
			if (searchableModule != null)
			{
				SearchContent[] searchContentList = searchableModule.GetAllSearchableContent();
				foreach (SearchContent searchContent in searchContentList)
				{
					UpdateContent(searchContent);
				}
			}
		}

		/// <summary>
		/// Close the IndexWriter (saves the index).
		/// </summary>
		public void Close()
		{
			if (! this._isClosed && this._indexWriter != null)
			{
				if (this._rebuildIndex)
				{
					this._indexWriter.ForceMerge(1);
				}
				this._indexWriter.Dispose();
				this._indexDirectory.Dispose();
				this._isClosed = true;
				log.Info("New or updated search index written to disk.");
			}
		}

		private void InitIndexWriter()
		{
			try
			{
				this._indexWriter = CreateIndexWriter(this._rebuildIndex ? OpenMode.CREATE : OpenMode.CREATE_OR_APPEND);
			}
			catch (IndexFormatTooOldException ex)
			{
				// An index of an older Lucene version can't be updated. Start with an empty index; the content
				// returns after rebuilding the index in the site administration.
				log.Warn("The search index was created with an older version of Lucene and is replaced with an empty index. Rebuild the index in the site administration.", ex);
				this._indexDirectory.Dispose();
				DeleteIndexFiles();
				this._indexDirectory = FSDirectory.Open(this._physicalIndexDir);
				this._indexWriter = CreateIndexWriter(OpenMode.CREATE);
			}
		}

		private IndexWriter CreateIndexWriter(OpenMode openMode)
		{
			IndexWriterConfig config = new IndexWriterConfig(MatchVersion, new StandardAnalyzer(MatchVersion));
			config.OpenMode = openMode;
			return new IndexWriter(this._indexDirectory, config);
		}

		private void DeleteIndexFiles()
		{
			foreach (string file in System.IO.Directory.GetFiles(this._physicalIndexDir))
			{
				File.Delete(file);
			}
		}

		private Document BuildDocumentFromSearchContent(SearchContent searchContent)
		{
			Document doc = new Document();
			doc.Add(new TextField("title", searchContent.Title ?? String.Empty, Field.Store.YES));
			doc.Add(new TextField("summary", searchContent.Summary ?? String.Empty, Field.Store.YES));
			doc.Add(new TextField("contents", searchContent.Contents ?? String.Empty, Field.Store.NO));
			doc.Add(new TextField("author", searchContent.Author ?? String.Empty, Field.Store.YES));
			doc.Add(new StringField("moduletype", searchContent.ModuleType ?? String.Empty, Field.Store.YES));
			doc.Add(new StringField("path", searchContent.Path ?? String.Empty, Field.Store.YES));
			doc.Add(new StringField("category", searchContent.Category ?? String.Empty, Field.Store.YES));
			doc.Add(new StringField("site", searchContent.Site ?? String.Empty, Field.Store.YES));
			// Sortable local time without a time zone ("u" adds a 'Z' without converting to UTC, which shifted the dates).
			doc.Add(new StringField("datecreated", searchContent.DateCreated.ToString("s", CultureInfo.InvariantCulture), Field.Store.YES));
			doc.Add(new StringField("datemodified", searchContent.DateModified.ToString("s", CultureInfo.InvariantCulture), Field.Store.YES));
			doc.Add(new StoredField("sectionid", searchContent.SectionId.ToString()));

			return doc;
		}

		#region IDisposable Members

		public void Dispose()
		{
			Close();
		}

		#endregion
	}
}
