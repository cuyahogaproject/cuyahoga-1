using Cuyahoga.Core.Domain;
using Cuyahoga.Web.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Cuyahoga.Web.Core.PublicSite
{
	/// <summary>
	/// The cached output of the sections, in the memory cache of this site (the WebForms site has its own).
	/// </summary>
	public class SectionOutputCache : ISectionOutputCache
	{
		private readonly IMemoryCache _cache;

		public SectionOutputCache(IMemoryCache cache)
		{
			this._cache = cache;
		}

		/// <summary>
		/// The cached output of the module, or null.
		/// </summary>
		public CachedSection Get(ModuleBase module)
		{
			return this._cache.TryGetValue(GetKey(module), out CachedSection cached) ? cached : null;
		}

		public void Set(ModuleBase module, CachedSection section, int durationInSeconds)
		{
			this._cache.Set(GetKey(module), section, DateTimeOffset.Now.AddSeconds(durationInSeconds));
		}

		public void Remove(ModuleBase module)
		{
			if (module.CacheKey != null)
			{
				this._cache.Remove(GetKey(module));
			}
		}

		private static string GetKey(ModuleBase module)
		{
			return "Cuyahoga.Section." + module.CacheKey;
		}
	}

	/// <summary>
	/// The output of a module view and what it gave back to the page, so a cached section looks the same.
	/// </summary>
	public record CachedSection(string Html, string DisplayTitle, bool DisplaySyndicationIcon,
		IReadOnlyList<KeyValuePair<string, string>> Stylesheets, IReadOnlyList<KeyValuePair<string, string>> JavaScripts);
}
