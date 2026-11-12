using System;
using Castle.Core;
using Castle.DynamicProxy;
using Castle.MicroKernel;
using Castle.MicroKernel.ModelBuilder;

namespace Cuyahoga.Core.Persistence
{
	/// <summary>
	/// Runs methods with a <see cref="TransactionAttribute"/> in a transaction. The outermost transactional
	/// method starts the transaction and commits it, or rolls it back when an exception occurs.
	/// </summary>
	public class TransactionInterceptor : IInterceptor
	{
		private readonly ISessionManager _sessionManager;

		public TransactionInterceptor(ISessionManager sessionManager)
		{
			this._sessionManager = sessionManager;
		}

		public void Intercept(IInvocation invocation)
		{
			TransactionAttribute transactionAttribute = (TransactionAttribute)Attribute.GetCustomAttribute(
				invocation.MethodInvocationTarget, typeof(TransactionAttribute));

			bool startsTransaction = transactionAttribute != null
				&& (transactionAttribute.Mode == TransactionMode.Requires || transactionAttribute.Mode == TransactionMode.RequiresNew)
				&& TransactionContext.Current == null;
			if (! startsTransaction)
			{
				invocation.Proceed();
				return;
			}

			TransactionContext transaction = TransactionContext.Begin(this._sessionManager.OpenSession());
			try
			{
				invocation.Proceed();
				transaction.Commit();
			}
			catch
			{
				transaction.Rollback();
				throw;
			}
			finally
			{
				transaction.End();
			}
		}
	}

	/// <summary>
	/// Adds the <see cref="TransactionInterceptor"/> to components with a <see cref="TransactionalAttribute"/>.
	/// </summary>
	public class TransactionalComponentInspector : IContributeComponentModelConstruction
	{
		public void ProcessModel(IKernel kernel, ComponentModel model)
		{
			if (model.Implementation != null && model.Implementation.IsDefined(typeof(TransactionalAttribute), true))
			{
				model.Interceptors.Add(new InterceptorReference(typeof(TransactionInterceptor)));
			}
		}
	}
}
