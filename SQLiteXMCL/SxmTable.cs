using LinqToDB;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

/// <summary>
/// Lightweight wrapper around <see cref="IQueryable{T}"/> that exposes an instance
/// <see cref="LoadWith{TProperty}(Expression{Func{T, TProperty}})"/> API so callers in
/// the main app don't need to reference LinqToDB directly.
/// </summary>
/// <typeparam name="T">The entity type for the queryable table.</typeparam>
public sealed class SxmTable<T> : IQueryable<T> where T : class
{
    private readonly IQueryable<T> inner;

    /// <summary>
    /// Create a new <see cref="SxmTable{T}"/> that wraps the provided queryable.
    /// </summary>
    /// <param name="inner">The underlying queryable to wrap. Must not be <c>null</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="inner"/> is <c>null</c>.</exception>
    public SxmTable(IQueryable<T> inner)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    /// <summary>
    /// Return a new <see cref="SxmTable{T}"/> that will eagerly load the specified navigation
    /// property when executed against a LinqToDB <see cref="ITable{T}"/>.
    /// </summary>
    /// <typeparam name="TProperty">The navigation property type.</typeparam>
    /// <param name="navigationProperty">Expression selecting the navigation property to load.</param>
    /// <returns>
    /// A new <see cref="SxmTable{T}"/> wrapping the updated query when the underlying
    /// source is an <see cref="ITable{T}"/>; otherwise the original instance (no-op).
    /// </returns>
    public SxmTable<T> LoadWith<TProperty>(Expression<Func<T, TProperty>> navigationProperty)
    {
        if (inner is ITable<T> table)
        {
            // This resolves LinqToDB's LoadWith extension for ITable<T>
            var newQuery = table.LoadWith(navigationProperty);
            return new SxmTable<T>(newQuery);
        }

        // fallback: no-op (query stays unchanged)
        return this;
    }

    /// <summary>
    /// Overload of <see cref="LoadWith{TProperty}(Expression{Func{T, TProperty}})"/> that
    /// accepts multiple navigation property expressions. Each provided expression will be
    /// applied in order when the underlying query is an <see cref="ITable{T}"/>.
    /// </summary>
    /// <param name="navigationProperties">One or more navigation property expressions to load.</param>
    /// <returns>
    /// A new <see cref="SxmTable{T}"/> wrapping the updated query when the underlying
    /// source is an <see cref="ITable{T}"/>; otherwise the original instance (no-op).
    /// </returns>
    public SxmTable<T> LoadWith(params Expression<Func<T, object>>[] navigationProperties)
    {
        if (inner is ITable<T> table)
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

    /// <summary>
    /// Return the inner query as LinqToDB's <see cref="ITable{T}"/> when available.
    /// </summary>
    /// <remarks>
    /// Callers that require LinqToDB-specific extensions can call this and check for <c>null</c>.
    /// </remarks>
    /// <returns>The underlying <see cref="ITable{T}"/> if the inner query implements it; otherwise <c>null</c>.</returns>
    internal ITable<T>? AsITable() => inner as ITable<T>;

    /// <summary>
    /// Try to obtain the underlying LinqToDB <see cref="ITable{T}"/>.
    /// </summary>
    /// <param name="table">When this method returns, contains the <see cref="ITable{T}"/> instance if available; otherwise <c>null</c>.</param>
    /// <returns><c>true</c> if the underlying query implements <see cref="ITable{T}"/>; otherwise <c>false</c>.</returns>
    internal bool TryGetITable([NotNullWhen(true)] out ITable<T>? table)
    {
        table = inner as ITable<T>;
        return table != null;
    }

    // IQueryable<T> implementation - delegate to the underlying query

    /// <summary>
    /// Gets the element type of the underlying query.
    /// </summary>
    public Type ElementType => inner.ElementType;

    /// <summary>
    /// Gets the expression tree that is associated with the instance of <see cref="IQueryable{T}"/>.
    /// </summary>
    public Expression Expression => inner.Expression;

    /// <summary>
    /// Gets the query provider used to execute the underlying query.
    /// </summary>
    public IQueryProvider Provider => inner.Provider;

    /// <summary>
    /// Get an enumerator that iterates through the results of the underlying query.
    /// </summary>
    /// <returns>An enumerator for the query results.</returns>
    public IEnumerator<T> GetEnumerator() => inner.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)inner).GetEnumerator();

    /// <summary>
    /// Returns the string representation of the underlying query (for debugging and diagnostics).
    /// </summary>
    public override string? ToString() => inner.ToString();
}