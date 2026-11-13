using System.Collections;
using System.Threading;

namespace Cuyahoga.Core.Persistence
{
	/// <summary>
	/// Stores the objects of the current unit of work per async flow: they follow the code across awaits and
	/// into tasks that it starts. Objects stored inside a started task are not visible to the code that
	/// started it, so begin a unit of work in the code that also ends it.
	/// </summary>
	public class AsyncLocalContextStorage : IContextStorage
	{
		private readonly AsyncLocal<Hashtable> _items = new AsyncLocal<Hashtable>();

		public object Get(string key)
		{
			Hashtable items = _items.Value;
			return items != null ? items[key] : null;
		}

		public void Set(string key, object value)
		{
			Hashtable items = _items.Value;
			if (value == null)
			{
				if (items != null)
				{
					items.Remove(key);
				}
				return;
			}
			if (items == null)
			{
				items = new Hashtable();
				_items.Value = items;
			}
			items[key] = value;
		}
	}
}
