using LinqToDB;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

namespace SQLiteXM;

/// <summary>
/// Lightweight wrapper around <see cref="IQueryable{T}"/> that exposes an instance
/// <see cref="LoadWith{TProperty}(Expression{Func{T, TProperty}})"/> API so callers in
/// the main app don't need to reference LinqToDB directly.
/// </summary>
/// <typeparam name="T">The entity type for the queryable table.</typeparam>
public sealed class SxmTable<T> : IQueryable<T> where T : class
{
    private readonly IQueryable<T> _inner;
    private readonly SxmTransaction? _context;

    /// <summary>
    /// Create a new <see cref="SxmTable{T}"/> that wraps the provided queryable.
    /// </summary>
    /// <param name="inner">The underlying queryable to wrap. Must not be <c>null</c>.</param>
    /// <param name="context">Optional SxmTransaction for deferred bulk operations.</param>
    /// <exception cref="ArgumentNullException"><paramref name="inner"/> is <c>null</c>.</exception>
    public SxmTable(IQueryable<T> inner, SxmTransaction? context = null)
    {
        this._inner = inner ?? throw new ArgumentNullException(nameof(inner));
        this._context = context;
    }

    /// <summary>
    /// Gets the SxmTransaction associated with this table, if any.
    /// </summary>
    internal SxmTransaction? DataContext => _context;

    /// <summary>
    /// Return a new <see cref="SxmTable{T}"/> that will eagerly load the specified navigation
    /// property when the query is executed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// LIMITATION: LoadWith is supported only when an entity has at most ONE foreign key pointing
    /// at a given target table. The association registry matches a foreign key to a navigation
    /// property using the property's CLR type alone, so two properties of the same type cannot be
    /// told apart.
    /// </para>
    /// <para>
    /// This is enforced: if the entity declares more than one <c>[ForeignKey]</c> referencing the
    /// target table, LoadWith throws <see cref="ArgumentException"/> rather than risk binding the
    /// navigation property to the wrong column and silently loading the wrong row. The check counts
    /// <c>[ForeignKey]</c> declarations only, so it never assumes that a property of the target type
    /// was intended as a navigation property.
    /// </para>
    /// <para>
    /// The foreign keys themselves are unaffected: all of them are created, enforced, and stored
    /// normally, and all can be queried with an explicit join. Only eager loading through
    /// LoadWith is restricted.
    /// </para>
    /// <para>
    /// WORKAROUND: join on the foreign-key column instead of using LoadWith. A join names the
    /// joining column explicitly, so every foreign key remains usable:
    /// <code>
    /// List&lt;Paper&gt; papers = await ctx.GetTable&lt;Paper&gt;().ToListAsync();
    /// List&lt;Person&gt; people = await ctx.GetTable&lt;Person&gt;().ToListAsync();
    ///
    /// string? authorName   = people.Single(x =&gt; x.id == paper.AuthorId).PersonName;
    /// string? coAuthorName = people.Single(x =&gt; x.id == paper.CoAuthorId).PersonName;
    /// </code>
    /// </para>
    /// </remarks>
    /// <typeparam name="TProperty">The navigation property type.</typeparam>
    /// <param name="navigationProperty">Expression selecting the navigation property to load.</param>
    /// <returns>A new <see cref="SxmTable{T}"/> wrapping the updated query.</returns>
    /// <exception cref="ArgumentNullException">The expression is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// The expression does not select a registered navigation property, or the entity declares
    /// more than one foreign key referencing the navigation property's target table.
    /// </exception>
    public SxmTable<T> LoadWith<TProperty>(Expression<Func<T, TProperty?>> navigationProperty)
    {
        ValidateNavigationExpression(navigationProperty);

        // LinqToDB exposes LoadWith for IQueryable<T>, not just ITable<T>. Using the IQueryable
        // overload lets LoadWith calls be chained: the first call returns a query that is no
        // longer an ITable<T>, and requiring ITable<T> here would silently drop every later call.
        IQueryable<T> newQuery = _inner.LoadWith(navigationProperty);
        return new SxmTable<T>(newQuery, _context);
    }

    /// <summary>
    /// Overload of <see cref="LoadWith{TProperty}(Expression{Func{T, TProperty}})"/> that
    /// accepts multiple navigation property expressions, applied in order.
    /// </summary>
    /// <remarks>
    /// The same restriction applies as for the single-property overload: LoadWith is supported only
    /// when the entity has at most ONE foreign key referencing a given target table. If any supplied
    /// expression selects a navigation property whose target table is referenced by more than one
    /// <c>[ForeignKey]</c> on the entity, this method throws <see cref="ArgumentException"/>. Use an
    /// explicit join for those properties instead. See
    /// <see cref="LoadWith{TProperty}(Expression{Func{T, TProperty}})"/> for the full explanation
    /// and an example.
    /// </remarks>
    /// <param name="navigationProperties">One or more navigation property expressions to load.</param>
    /// <returns>A new <see cref="SxmTable{T}"/> wrapping the updated query.</returns>
    /// <exception cref="ArgumentNullException">The array, or any element in it, is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// An expression does not select a registered navigation property, or the entity declares more
    /// than one foreign key referencing that navigation property's target table.
    /// </exception>
    public SxmTable<T> LoadWith(params Expression<Func<T, object?>>[] navigationProperties)
    {
        if (navigationProperties == null)
            throw new ArgumentNullException(nameof(navigationProperties));

        foreach (Expression<Func<T, object?>> prop in navigationProperties)
        {
            ValidateNavigationExpression(prop);
        }

        IQueryable<T> q = _inner;
        foreach (Expression<Func<T, object?>> prop in navigationProperties)
        {
            q = q.LoadWith(prop);
        }

        return new SxmTable<T>(q, _context);
    }

    /// <summary>
    /// Verifies that a LoadWith expression selects a genuine navigation property with a registered
    /// association, rather than an ordinary mapped column or an arbitrary expression.
    /// </summary>
    /// <remarks>
    /// The compiler already guarantees the selected member exists on <typeparamref name="T"/>, but it
    /// cannot tell a navigation property from a scalar column. Without this check, a call such as
    /// <c>LoadWith(o =&gt; o.CustomerId)</c> compiles and then fails obscurely (or silently does
    /// nothing) at query-execution time, far from the offending call site.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The expression is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The expression does not select a registered navigation property.</exception>
    private static void ValidateNavigationExpression(LambdaExpression navigationProperty)
    {
        if (navigationProperty == null)
            throw new ArgumentNullException(nameof(navigationProperty));

        // Unwrap the boxing conversion the compiler inserts for the object-returning params overload.
        Expression body = navigationProperty.Body;
        if (body is UnaryExpression unary &&
            (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked))
        {
            body = unary.Operand;
        }

        MemberExpression? memberExpression = body as MemberExpression;
        if (memberExpression == null)
        {
            throw new ArgumentException(
                $"LOADWITH ERROR on entity '{typeof(T).Name}': The expression is not a navigation property selector.\n" +
                $"SOLUTION: Pass a simple property access, for example: LoadWith(x => x.RelatedEntity).",
                nameof(navigationProperty));
        }

        // Only validate the first hop; nested paths such as x => x.Customer.Region are resolved by
        // walking back to the member whose declaring expression is the lambda parameter.
        MemberExpression firstHop = memberExpression;
        while (firstHop.Expression is MemberExpression parentMember)
        {
            firstHop = parentMember;
        }

        if (firstHop.Expression is not ParameterExpression)
        {
            throw new ArgumentException(
                $"LOADWITH ERROR on entity '{typeof(T).Name}': The expression must select a navigation property of '{typeof(T).Name}'.\n" +
                $"SOLUTION: Pass a simple property access, for example: LoadWith(x => x.RelatedEntity).",
                nameof(navigationProperty));
        }

        string memberName = firstHop.Member.Name;

        // Reject the ambiguous case BEFORE checking registration. When an entity declares more than
        // one [ForeignKey] to the same target table, association wiring cannot tell the navigation
        // properties apart (it matches on CLR type alone) and may bind this property to the other
        // foreign key's column, silently loading the wrong row. Counting [ForeignKey] declarations
        // is unambiguous and never assumes a property of the target type is a navigation property.
        if (firstHop.Member is PropertyInfo navProperty)
        {
            Type targetType = Nullable.GetUnderlyingType(navProperty.PropertyType) ?? navProperty.PropertyType;
            List<string> foreignKeys = SxmHelpers.GetForeignKeyColumnsTargeting(typeof(T), targetType.Name);

            if (foreignKeys.Count > 1)
            {
                throw new ArgumentException(
                    $"LOADWITH ERROR on entity '{typeof(T).Name}': '{memberName}' cannot be eagerly loaded because " +
                    $"'{typeof(T).Name}' declares {foreignKeys.Count} foreign keys referencing '{targetType.Name}' " +
                    $"({string.Join(", ", foreignKeys)}).\n" +
                    $"CAUSE: LoadWith matches a navigation property to a foreign key by the property's type alone, " +
                    $"so when several foreign keys point at the same table the correct column cannot be determined. " +
                    $"Allowing it could silently load the row from the wrong foreign key.\n" +
                    $"SOLUTION: Use an explicit join on the foreign-key column you want, for example:\n" +
                    $"  List<{typeof(T).Name}> rows = await ctx.GetTable<{typeof(T).Name}>().ToListAsync();\n" +
                    $"  List<{targetType.Name}> related = await ctx.GetTable<{targetType.Name}>().ToListAsync();\n" +
                    $"  {targetType.Name} match = related.Single(x => x.id == row.{foreignKeys[0]});\n" +
                    $"LoadWith is supported when an entity has at most one foreign key per target table.",
                    nameof(navigationProperty));
            }
        }

        if (!SxmHelpers.IsRegisteredAssociation(typeof(T), memberName))
        {
            List<string> available = SxmHelpers.GetRegisteredAssociationNames(typeof(T));
            string availableText = available.Count == 0
                ? $"'{typeof(T).Name}' has no navigation properties."
                : $"Available navigation properties: {string.Join(", ", available)}.";

            throw new ArgumentException(
                $"LOADWITH ERROR on entity '{typeof(T).Name}': '{memberName}' is not a navigation property.\n" +
                $"CAUSE: LoadWith eagerly loads a related entity through a registered association. " +
                $"'{memberName}' is either an ordinary mapped column or has no association configured.\n" +
                $"SOLUTION: Declare a navigation property marked [NotColumn] whose type is the related entity, " +
                $"backed by a [ForeignKey] property, and ensure both entity types are registered before querying.\n" +
                $"{availableText}",
                nameof(navigationProperty));
        }
    }

    /// <summary>
    /// Return the inner query as LinqToDB's <see cref="ITable{T}"/> when available.
    /// </summary>
    /// <remarks>
    /// Callers that require LinqToDB-specific extensions can call this and check for <c>null</c>.
    /// </remarks>
    /// <returns>The underlying <see cref="ITable{T}"/> if the inner query implements it; otherwise <c>null</c>.</returns>
    internal ITable<T>? AsITable() => _inner as ITable<T>;

    /// <summary>
    /// Return the underlying query as an <see cref="IQueryable{T}"/>.
    /// </summary>
    /// <remarks>
    /// Operators such as <see cref="LoadWith{TProperty}(Expression{Func{T, TProperty}})"/> produce an
    /// <see cref="IQueryable{T}"/> that is no longer an <see cref="ITable{T}"/>. Materializers must use
    /// this rather than <see cref="AsITable"/> so composed queries remain executable.
    /// </remarks>
    /// <returns>The underlying queryable. Never <c>null</c>.</returns>
    internal IQueryable<T> AsInnerQueryable() => _inner;

    /// <summary>
    /// Try to obtain the underlying LinqToDB <see cref="ITable{T}"/>.
    /// </summary>
    /// <param name="table">When this method returns, contains the <see cref="ITable{T}"/> instance if available; otherwise <c>null</c>.</param>
    /// <returns><c>true</c> if the underlying query implements <see cref="ITable{T}"/>; otherwise <c>false</c>.</returns>
    internal bool TryGetITable([NotNullWhen(true)] out ITable<T>? table)
    {
        table = _inner as ITable<T>;
        return table != null;
    }

    // IQueryable<T> implementation - delegate to the underlying query

    /// <summary>
    /// Gets the element type of the underlying query.
    /// </summary>
    public Type ElementType => _inner.ElementType;

    /// <summary>
    /// Gets the expression tree that is associated with the instance of <see cref="IQueryable{T}"/>.
    /// </summary>
    public Expression Expression => _inner.Expression;

    /// <summary>
    /// Gets the query provider used to execute the underlying query.
    /// </summary>
    public IQueryProvider Provider => _inner.Provider;

    /// <summary>
    /// Get an enumerator that iterates through the results of the underlying query.
    /// </summary>
    /// <returns>An enumerator for the query results.</returns>
    public IEnumerator<T> GetEnumerator() => _inner.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_inner).GetEnumerator();

    /// <summary>
    /// Returns the string representation of the underlying query (for debugging and diagnostics).
    /// </summary>
    public override string? ToString() => _inner.ToString();
}