using System.Collections.Concurrent;
using Cuyahoga.Core.Domain;
using Cuyahoga.Web.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace Cuyahoga.Web.Core.PublicSite
{
	/// <summary>
	/// The cached output of the sections, in the memory cache of this site (the WebForms site has its own).
	/// </summary>
	public class SectionOutputCache : ISectionOutputCache
	{
		private readonly IMemoryCache _cache;
		// Per section: cancelled to remove all its cached output (one entry per path info).
		private readonly ConcurrentDictionary<int, CancellationTokenSource> _sectionTokens =
			new ConcurrentDictionary<int, CancellationTokenSource>();

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
			CancellationTokenSource sectionToken = this._sectionTokens.GetOrAdd(module.Section.Id, _ => new CancellationTokenSource());
			var options = new MemoryCacheEntryOptions { AbsoluteExpiration = DateTimeOffset.Now.AddSeconds(durationInSeconds) }
				.AddExpirationToken(new CancellationChangeToken(sectionToken.Token));
			this._cache.Set(GetKey(module), section, options);
		}

		public void Remove(ModuleBase module)
		{
			if (module.Section != null && this._sectionTokens.TryRemove(module.Section.Id, out CancellationTokenSource sectionToken))
			{
				sectionToken.Cancel();
				sectionToken.Dispose();
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
