using LinqToDB;
using System.Collections;
using System.Linq.Expressions;

/// <summary>
/// Lightweight wrapper around IQueryable<T> that exposes an instance LoadWith(...) API
/// so callers in the main app don't need to reference LinqToDB.
/// </summary>
public sealed class SxmTable<T> : IQueryable<T>
    where T : class
{
    private readonly IQueryable<T> _inner;

    public SxmTable(IQueryable<T> inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    // Instance LoadWith that forwards to LinqToDB when possible.
    public SxmTable<T> LoadWith<TProperty>(Expression<Func<T, TProperty>> navigationProperty)
    {
        if (_inner is ITable<T> table)
        {
            // This resolves LinqToDB's LoadWith extension for ITable<T>
            var newQuery = table.LoadWith(navigationProperty);
            return new SxmTable<T>(newQuery);
        }

        // fallback: no-op (query stays unchanged)
        return this;
    }

    // Overload for multiple navigation properties
    public SxmTable<T> LoadWith(params Expression<Func<T, object>>[] navigationProperties)
    {
        if (_inner is ITable<T> table)
        {
            IQueryable<T> q = table;
            foreach (var prop in navigationProperties)
            {
                q = ((ITable<T>)q).LoadWith(prop);
            }
            return new SxmTable<T>(q);
        }
        return this;
    }

    /// Return the inner query as LinqToDB's ITable<T> when available.
    /// Callers who need LinqToDB-specific extensions can use this.
    public ITable<T>? AsITable() => _inner as ITable<T>;

    // IQueryable<T> implementation - delegate to the underlying query
    public Type ElementType => _inner.ElementType;
    public Expression Expression => _inner.Expression;
    public IQueryProvider Provider => _inner.Provider;

    public IEnumerator<T> GetEnumerator() => _inner.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_inner).GetEnumerator();

    public override string? ToString() => _inner.ToString();
}
