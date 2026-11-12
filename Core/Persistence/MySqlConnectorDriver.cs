using System;
using NHibernate.Driver;
using NHibernate.Engine;

namespace Cuyahoga.Core.Persistence
{
	/// <summary>
	/// NHibernate driver for MySqlConnector (https://mysqlconnector.net), the MIT-licensed ADO.NET provider for
	/// MySQL. NHibernate only comes with a driver for Oracle's MySql.Data; this one has the same settings.
	/// Configure it with Cuyahoga.NHibernateDriver = "Cuyahoga.Core.Persistence.MySqlConnectorDriver, Cuyahoga.Core".
	/// </summary>
	public class MySqlConnectorDriver : ReflectionBasedDriver
	{
		public MySqlConnectorDriver()
			: base("MySqlConnector", "MySqlConnector", "MySqlConnector.MySqlConnection", "MySqlConnector.MySqlCommand")
		{
		}

		public override bool UseNamedPrefixInSql
		{
			get { return true; }
		}

		public override bool UseNamedPrefixInParameter
		{
			get { return true; }
		}

		public override string NamedPrefix
		{
			get { return "?"; }
		}

		public override bool SupportsMultipleOpenReaders
		{
			get { return false; }
		}

		protected override bool SupportsPreparingCommands
		{
			get { return false; }
		}

		public override bool SupportsMultipleQueries
		{
			get { return true; }
		}

		public override IResultSetsCommand GetResultSetsCommand(ISessionImplementor session)
		{
			return new BasicResultSetsCommand(session);
		}

		public override bool RequiresTimeSpanForTime
		{
			get { return true; }
		}

		public override DateTime MinDate
		{
			get { return new DateTime(1000, 1, 1); }
		}
	}
}
