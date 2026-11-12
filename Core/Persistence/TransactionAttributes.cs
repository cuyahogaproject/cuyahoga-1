using System;

namespace Cuyahoga.Core.Persistence
{
	/// <summary>
	/// Marks a component whose methods with a <see cref="TransactionAttribute"/> run in a transaction.
	/// Methods of a component that is registered by class (not by interface) must be virtual.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, Inherited = true)]
	public sealed class TransactionalAttribute : Attribute
	{
	}

	/// <summary>
	/// Runs the method in a transaction of the current unit of work. Requires a <see cref="TransactionalAttribute"/>
	/// on the class.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method)]
	public sealed class TransactionAttribute : Attribute
	{
		public TransactionAttribute() : this(TransactionMode.Requires)
		{
		}

		public TransactionAttribute(TransactionMode mode)
		{
			this.Mode = mode;
		}

		public TransactionMode Mode { get; private set; }
	}

	public enum TransactionMode
	{
		/// <summary>Join the current transaction or start one.</summary>
		Requires,
		/// <summary>
		/// Join the current transaction or start one. A unit of work has a single NHibernate session, and a
		/// session can only have one transaction, so this is the same as Requires (also with the Castle facility).
		/// </summary>
		RequiresNew,
		/// <summary>Join the current transaction if there is one, but don't start one.</summary>
		Supported,
		/// <summary>Don't start a transaction.</summary>
		NotSupported
	}
}
