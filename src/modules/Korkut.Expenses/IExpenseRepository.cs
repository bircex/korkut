using Korkut.Core;

namespace Korkut.Expenses;

/// <summary>Persistence port for expenses; listings are most-recently-spent first.</summary>
public interface IExpenseRepository : IRepository<Expense>;
