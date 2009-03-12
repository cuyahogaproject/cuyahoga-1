using System;

namespace Cuyahoga.Core.Search
{
	public class SearchException : ApplicationException
	{
		public SearchException(string message, Exception innerException)
			: base(message, innerException)
		{
		}
	}
}
