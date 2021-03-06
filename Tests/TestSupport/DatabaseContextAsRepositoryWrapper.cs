﻿using System;
using VocaDb.Model.Database.Repositories;

namespace VocaDb.Tests.TestSupport {

	public class DatabaseContextAsRepositoryWrapper : IRepository {

		private readonly IDatabaseContext dbContext;

		public DatabaseContextAsRepositoryWrapper(IDatabaseContext dbContext) {
			this.dbContext = dbContext;
		}

		public TResult HandleQuery<TResult>(Func<IDatabaseContext, TResult> func, string failMsg = "Unexpected database error") {
			return func(dbContext);
		}

		public void HandleTransaction(Action<IDatabaseContext> func, string failMsg = "Unexpected database error") {
			func(dbContext);
		}

		public TResult HandleTransaction<TResult>(Func<IDatabaseContext, TResult> func, string failMsg = "Unexpected database error") {
			return func(dbContext);
		}

	}

	public class DatabaseContextAsRepositoryWrapper<TRepo> : IRepository<TRepo> {

		private readonly IDatabaseContext<TRepo> dbContext;

		public DatabaseContextAsRepositoryWrapper(IDatabaseContext<TRepo> dbContext) {
			this.dbContext = dbContext;
		}

		public TResult HandleQuery<TResult>(Func<IDatabaseContext<TRepo>, TResult> func, string failMsg = "Unexpected database error") {
			return func(dbContext);
		}

		public void HandleTransaction(Action<IDatabaseContext<TRepo>> func, string failMsg = "Unexpected database error") {
			func(dbContext);
		}

		public TResult HandleTransaction<TResult>(Func<IDatabaseContext<TRepo>, TResult> func, string failMsg = "Unexpected database error") {
			return func(dbContext);
		}

	}
}
