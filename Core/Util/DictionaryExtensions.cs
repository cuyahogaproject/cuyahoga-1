using System.Collections.Generic;

namespace Cuyahoga.Core.Util
{
	public static class DictionaryExtensions
	{
		/// <summary>
		/// Returns the value for the key, or the default value (null for reference types) when the key doesn't exist.
		/// The indexer of a generic dictionary throws instead (the non-generic IDictionary returned null).
		/// </summary>
		public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
		{
			TValue value;
			return dictionary.TryGetValue(key, out value) ? value : default(TValue);
		}
	}
}
