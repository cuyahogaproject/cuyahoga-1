using System.Collections.Generic;
using NHibernate;

namespace Cuyahoga.Core.Persistence
{
	/// <summary>
	/// A resource (for example files) that is committed or rolled back together with the database transaction.
	/// </summary>
	public interface ITransactionResource
	{
		void Commit();
		void Rollback();
	}

	/// <summary>
	/// The transaction of the current unit of work: the NHibernate transaction plus enlisted resources.
	/// </summary>
	public sealed class TransactionContext
	{
		private const string TransactionKey = "Cuyahoga.Persistence.Transaction";

		private readonly ITransaction _transaction;
		private readonly List<ITransactionResource> _resources = new List<ITransactionResource>();

		private TransactionContext(ITransaction transaction)
		{
			this._transaction = transaction;
		}

		/// <summary>
		/// The transaction of the current unit of work, or null when no transaction is active.
		/// </summary>
		public static TransactionContext Current
		{
			get { return ContextStorage.Get(TransactionKey) as TransactionContext; }
		}

		/// <summary>
		/// Commits or rolls back the resource together with the transaction.
		/// </summary>
		public void Enlist(ITransactionResource resource)
		{
			this._resources.Add(resource);
		}

		internal static TransactionContext Begin(ISession session)
		{
			TransactionContext context = new TransactionContext(session.BeginTransaction());
			ContextStorage.Set(TransactionKey, context);
			return context;
		}

		/// <summary>
		/// Commits the database transaction first, so the resources are only committed when that succeeds.
		/// </summary>
		internal void Commit()
		{
			// Legacy code may have committed the session transaction itself already.
			if (this._transaction.IsActive)
			{
				this._transaction.Commit();
			}
			foreach (ITransactionResource resource in this._resources)
			{
				resource.Commit();
			}
		}

		internal void Rollback()
		{
			try
			{
				if (this._transaction.IsActive)
				{
					this._transaction.Rollback();
				}
			}
			finally
			{
				foreach (ITransactionResource resource in this._resources)
				{
					resource.Rollback();
				}
			}
		}

		internal void End()
		{
			ContextStorage.Set(TransactionKey, null);
			this._transaction.Dispose();
		}
	}
}
