namespace Cuyahoga.Core.Persistence
{
	/// <summary>
	/// Stores the objects of the current unit of work (the NHibernate session and transaction), for example per
	/// web request. The host sets <see cref="ContextStorage.Current"/> at application start.
	/// </summary>
	public interface IContextStorage
	{
		/// <summary>
		/// Gets the object stored under the key in the current context, or null.
		/// </summary>
		object Get(string key);

		/// <summary>
		/// Stores the object under the key in the current context. A null value removes the key.
		/// </summary>
		void Set(string key, object value);
	}
}
