namespace Cuyahoga.Web.Mvc
{
	/// <summary>
	/// The output of the module view is never cached, also when the section has a cache duration: the view does more
	/// than showing content (it can redirect, or set cookies).
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, Inherited = true)]
	public sealed class NoSectionCacheAttribute : Attribute
	{
	}
}
