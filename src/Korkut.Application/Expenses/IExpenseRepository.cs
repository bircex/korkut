using Korkut.Application.Common;
using Korkut.Domain.Expenses;

namespace Korkut.Application.Expenses;

/// <summary>Persistence port for expenses; listings are most-recently-spent first.</summary>
public interface IExpenseRepository : IRepository<Expense>;
