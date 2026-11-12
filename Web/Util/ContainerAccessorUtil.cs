using Cuyahoga.Core.Util;
using Cuyahoga.Web.Components;

namespace Cuyahoga.Web.Util
{
	public class ContainerAccessorUtil
	{
		private ContainerAccessorUtil()
		{
		}

		/// <summary>
		/// Returns the application container (initialized in Global.Application_Start).
		/// </summary>
		public static CuyahogaContainer GetContainer()
		{
			return (CuyahogaContainer)IoC.Container;
		}
	}
}
