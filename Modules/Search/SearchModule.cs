using System;
using System.Collections;
using System.Security.Principal;

using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Service;
using Cuyahoga.Core.Search;
using Cuyahoga.Core.Communication;
using Cuyahoga.Core.Util;

namespace Cuyahoga.Modules.Search
{
	/// <summary>
	/// The searchmodule provides search capabilities on the DotLucene search index.
	/// <seealso cref="Cuyahoga.Core.Search"/>
	/// </summary>
	public class SearchModule : ModuleBase, IActionConsumer
	{
		private int _resultsPerPage = 10;
		private bool _showInputPanel = true;
		private ModuleActionCollection _inboundModuleActions;
		private ModuleAction _currentModuleAction;
		private string _searchQuery;

		/// <summary>
		/// The number of results to show at one page. Default is 10.
		/// </summary>
		public int ResultsPerPage
		{
			get { return this._resultsPerPage; }
		}

		/// <summary>
		/// Show the search input panel?
		/// </summary>
		public bool ShowInputPanel
		{
			get { return this._showInputPanel; }
		}

		/// <summary>
		/// 
		/// </summary>
		public ModuleAction CurrentModuleAction
		{
			get { return this._currentModuleAction; }
		}

		/// <summary>
		/// 
		/// </summary>
		public string SearchQuery
		{
			get { return this._searchQuery; }
			set { this._searchQuery = value; }
		}

		/// <summary>
		/// Default constructor.
		/// </summary>
		public SearchModule()
		{
			// Init inbound actions
			this._inboundModuleActions = new ModuleActionCollection();
			this._inboundModuleActions.Add(new ModuleAction("Search", new string[0]));
			this._currentModuleAction = this._inboundModuleActions[0];
		}

		public override void ReadSectionSettings()
		{
			base.ReadSectionSettings ();
			if (base.Section.Settings.GetValueOrDefault("RESULTS_PER_PAGE") != null)
			{
				this._resultsPerPage = Convert.ToInt32(base.Section.Settings.GetValueOrDefault("RESULTS_PER_PAGE"));
			}
			if (base.Section.Settings.GetValueOrDefault("SHOW_INPUT_PANEL") != null)
			{
				this._showInputPanel = Convert.ToBoolean(base.Section.Settings.GetValueOrDefault("SHOW_INPUT_PANEL"));
			}
		}


		/// <summary>
		/// Get paged search results.
		/// </summary>
		/// <param name="queryText">The query to search for.</param>
		/// <param name="pageIndex"></param>
		/// <param name="pageSize"></param>
		/// <param name="indexDir"></param>
		/// <param name="user">The current user: results in sections that the user can't view are left out.</param>
		/// <returns></returns>
		public SearchResultCollection GetSearchResults(string queryText, int pageIndex, int pageSize, string indexDir, IIdentity user)
		{
			IndexQuery query = new IndexQuery(indexDir);
			Hashtable keywordFilter = new Hashtable();
			keywordFilter.Add("site", this.Section.Node.Site.Name);
			SearchResultCollection nonFilteredResults = query.Find(queryText, keywordFilter, pageIndex, pageSize);
			// Filter results where the current user doesn't have access to.
			return FilterResults(nonFilteredResults, user);
		}

		/// <summary>
		/// Get all search results with a maximum of 200.
		/// </summary>
		/// <param name="queryText"></param>
		/// <param name="indexDir"></param>
		/// <param name="user">The current user: results in sections that the user can't view are left out.</param>
		/// <returns></returns>
		public SearchResultCollection GetSearchResults(string queryText, string indexDir, IIdentity user)
		{
			return GetSearchResults(queryText, 0, 200, indexDir, user);
		}

		protected override void ParsePathInfo()
		{
			base.ParsePathInfo ();
			if (base.ModuleParams != null)
			{
				if (base.ModuleParams.Length == 1)
				{
					// First argument is the module action
					this._currentModuleAction = this._inboundModuleActions.FindByName(base.ModuleParams[0]);
					// For the Search action, the control sets SearchQuery from the query string.
					if (this._currentModuleAction == null)
					{
						throw new Exception("Error when parsing module action: " + base.ModuleParams[0]);
					}
				}
			}
		}

		/// <summary>
		/// A searchresult contains a SectionId propery that indicates to which section the 
		/// result belongs. We need to get a real Section to determine if the current user 
		/// has view access to that Section.
		/// </summary>
		/// <param name="nonFilteredResults"></param>
		/// <returns></returns>
		private SearchResultCollection FilterResults(SearchResultCollection nonFilteredResults, IIdentity user)
		{
			long filterStartTicks = DateTime.Now.Ticks;
			SearchResultCollection filteredResults = new SearchResultCollection();
			foreach (SearchResult result in nonFilteredResults)
			{
				Section section = base.NHSession.Load<Section>(result.SectionId);
				if (section.ViewAllowed(user))
				{
					filteredResults.Add(result);
				}
			}
			filteredResults.ExecutionTime = DateTime.Now.Ticks - filterStartTicks + nonFilteredResults.ExecutionTime;
			return filteredResults;
		}


		#region IActionConsumer Members

		public ModuleActionCollection GetInboundActions()
		{
			return this._inboundModuleActions;
		}

		#endregion
	}
}
