using LinqToDB;
using LinqToDB.Linq;
using SQLiteXM.Internal.Threading;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;

namespace SQLiteXM
{
    /// <summary>
    /// Lightweight wrapper over LinqToDB's update builder so callers that import
    /// <see cref="SQLiteXM"/> don't need to import <c>LinqToDB</c>.
    /// Bulk updates are deferred and executed within SubmitChangesAsync transaction.
    /// </summary>
    /// <typeparam name="T">Entity type.</typeparam>
    public sealed class SxmUpdateSet<T> where T : class
    {
        private readonly IUpdatable<T> _inner;
        private readonly SxmLinqContext? _context;

        /// <summary>
        /// Creates a new wrapper around the LinqToDB update builder instance.
        /// </summary>
        /// <param name="inner">The LinqToDB update builder instance.</param>
        /// <param name="context">The SxmLinqContext to enqueue the operation into (null for immediate execution).</param>
        internal SxmUpdateSet(IUpdatable<T> inner, SxmLinqContext? context = null)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _context = context;
        }

        /// <summary>
        /// Applies a set operation to the underlying update builder and returns a new wrapper
        /// to continue fluent calls.
        /// </summary>
        /// <typeparam name="TProp">Property type.</typeparam>
        /// <param name="setter">Selector for the property to set.</param>
        /// <param name="value">Value to set.</param>
        /// <returns>New <see cref="SxmUpdateSet{T}"/> that wraps the updated LinqToDB builder.</returns>
        public SxmUpdateSet<T> Set<TProp>(Expression<Func<T, TProp>> setter, TProp value)
        {
            var next = _inner.Set(setter, value);
            return new SxmUpdateSet<T>(next, _context);
        }

        /// <summary>
        /// Applies a set operation using an expression as the value provider and returns
        /// a new wrapper that continues the fluent chain.
        /// </summary>
        /// <typeparam name="TProp">Property type.</typeparam>
        /// <param name="setter">Selector for the property to set.</param>
        /// <param name="expression">Expression used to compute the new value.</param>
        /// <returns>New <see cref="SxmUpdateSet{T}"/> that wraps the updated LinqToDB builder.</returns>
        public SxmUpdateSet<T> Set<TProp>(Expression<Func<T, TProp>> setter, Expression<Func<T, TProp>> expression)
        {
            var next = _inner.Set(setter, expression);
            return new SxmUpdateSet<T>(next, _context);
        }

        /// <summary>
        /// Enqueues the bulk update to be executed during SubmitChangesAsync within the transaction.
        /// All bulk updates participate in the same transaction as entity operations.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task that completes when the update is enqueued (actual execution happens in SubmitChangesAsync).</returns>
        /// <exception cref="InvalidOperationException">Thrown when no SxmLinqContext is available.</exception>
        public Task<int> UpdateAsync(CancellationToken cancellationToken = default)
        {
            if (_context == null)
                throw new InvalidOperationException(
                    "Bulk update operations require a SxmLinqContext. " +
                    "Use ctx.GetTable<T>() to obtain a context-aware table, then call Set().UpdateAsync(). " +
                    "Bulk updates must participate in the SubmitChangesAsync() transaction.");

            // Defer execution - enqueue into context's change set
            _context.EnqueueBulkUpdate(() => _inner.UpdateAsync(cancellationToken));
            return Task.FromResult(0); // Return placeholder; actual count available after SubmitChangesAsync
        }
    }

    /// <summary>
    /// Re-exported Linq helpers in the <c>SQLiteXM</c> namespace so library consumers
    /// don't need to add <c>using LinqToDB</c>.
    /// All canonical helpers target <see cref="SxmTable{T}"/>; forwarding overloads for <see cref="IQueryable{T}"/>
    /// are provided to enable fluent chaining after Where/Select.
    /// </summary>
    public static class SxmLinqExtensions
    {
        // Use DataConnection's assembly as a stable LinqToDB assembly reference.
        private static readonly Assembly _linqToDbAssembly = typeof(LinqToDB.Data.DataConnection).Assembly;

        /// <summary>
        /// Starts a LinqToDB update builder for the supplied <see cref="SxmTable{T}"/>.
        /// The update will be deferred and executed within SubmitChangesAsync transaction.
        /// For single-entity updates, prefer <see cref="SxmLinqContext.UpdateOnSubmit"/> + <see cref="SxmLinqContext.SubmitChangesAsync"/>.
        /// </summary>
        public static SxmUpdateSet<T> Set<T, TProp>(this SxmTable<T> query, Expression<Func<T, TProp>> setter, TProp value)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (setter == null) throw new ArgumentNullException(nameof(setter));

            // Prefer calling LinqToDB's strongly typed helper directly.
            var itable = query.AsITable() ?? throw new InvalidOperationException("Operation requires LinqToDB ITable<T>.");

            // ITable<T> implements IQueryable<T>, call LinqExtensions.Set directly to get the provider-updatable.
            var updatable = LinqToDB.LinqExtensions.Set<T, TProp>((IQueryable<T>)itable, setter, value);
            return new SxmUpdateSet<T>((IUpdatable<T>)updatable!, query.DataContext);
        }

        /// <summary>
        /// Starts a LinqToDB update builder for the supplied <see cref="SxmTable{T}"/> using expression value provider.
        /// The update will be deferred and executed within SubmitChangesAsync transaction.
        /// </summary>
        public static SxmUpdateSet<T> Set<T, TProp>(this SxmTable<T> query, Expression<Func<T, TProp>> setter, Expression<Func<T, TProp>> expression)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (setter == null) throw new ArgumentNullException(nameof(setter));
            if (expression == null) throw new ArgumentNullException(nameof(expression));

            var itable = query.AsITable() ?? throw new InvalidOperationException("Operation requires LinqToDB ITable<T>.");

            var updatable = LinqToDB.LinqExtensions.Set<T, TProp>((IQueryable<T>)itable, setter, expression);
            return new SxmUpdateSet<T>((IUpdatable<T>)updatable!, query.DataContext);
        }

        /// <summary>
        /// Starts a LinqToDB update builder for the supplied <see cref="IQueryable{T}"/>.
        /// Automatically recovers SxmLinqContext from LINQ chains for transactional bulk updates.
        /// </summary>
        public static SxmUpdateSet<T> Set<T, TProp>(this IQueryable<T> query, Expression<Func<T, TProp>> setter, TProp value)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (setter == null) throw new ArgumentNullException(nameof(setter));

            // Try to recover context from the query (works for SxmTable and LinqToDB query chains)
            var context = SxmLinqContext.TryGetContextFromQuery(query);

            // Call LinqToDB helper directly using the IQueryable overload.
            var updatable = LinqToDB.LinqExtensions.Set<T, TProp>(query, setter, value);
            return new SxmUpdateSet<T>((IUpdatable<T>)updatable!, context);
        }

        /// <summary>
        /// Starts a LinqToDB update builder for the supplied <see cref="IQueryable{T}"/> using expression value provider.
        /// Automatically recovers SxmLinqContext from LINQ chains for transactional bulk updates.
        /// </summary>
        public static SxmUpdateSet<T> Set<T, TProp>(this IQueryable<T> query, Expression<Func<T, TProp>> setter, Expression<Func<T, TProp>> expression)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (setter == null) throw new ArgumentNullException(nameof(setter));
            if (expression == null) throw new ArgumentNullException(nameof(expression));

            // Try to recover context from the query (works for SxmTable and LinqToDB query chains)
            var context = SxmLinqContext.TryGetContextFromQuery(query);

            var updatable = LinqToDB.LinqExtensions.Set<T, TProp>(query, setter, expression);
            return new SxmUpdateSet<T>((IUpdatable<T>)updatable!, context);
        }



        /// <summary>
        /// Asynchronously materializes the rows from the provided <see cref="SxmTable{T}"/> to a list.
        /// </summary>
        public static Task<List<T>> ToListAsync<T>(this SxmTable<T> table, CancellationToken cancellationToken = default)
            where T : class
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            var itable = table.AsITable() ?? throw new InvalidOperationException("Operation requires LinqToDB ITable<T>.");
            return LinqToDB.AsyncExtensions.ToListAsync((IQueryable<T>)itable, cancellationToken);
        }

        /// <summary>
        /// Asynchronously materializes the rows from the provided <see cref="SxmTable{T}"/> to an array.
        /// </summary>
        public static Task<T[]> ToArrayAsync<T>(this SxmTable<T> table, CancellationToken cancellationToken = default)
            where T : class
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            var itable = table.AsITable() ?? throw new InvalidOperationException("Operation requires LinqToDB ITable<T>.");
            return LinqToDB.AsyncExtensions.ToArrayAsync((IQueryable<T>)itable, cancellationToken);
        }

        /// <summary>
        /// Asynchronously returns the first element from the provided <see cref="SxmTable{T}"/>.
        /// </summary>
        public static Task<T> FirstAsync<T>(this SxmTable<T> table, CancellationToken cancellationToken = default)
            where T : class
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            var itable = table.AsITable() ?? throw new InvalidOperationException("Operation requires LinqToDB ITable<T>.");
            return LinqToDB.AsyncExtensions.FirstAsync((IQueryable<T>)itable, cancellationToken);
        }

        /// <summary>
        /// Asynchronously returns the first element or default from the provided <see cref="SxmTable{T}"/>.
        /// </summary>
        public static Task<T?> FirstOrDefaultAsync<T>(this SxmTable<T> table, CancellationToken cancellationToken = default)
            where T : class
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            var itable = table.AsITable() ?? throw new InvalidOperationException("Operation requires LinqToDB ITable<T>.");
            return LinqToDB.AsyncExtensions.FirstOrDefaultAsync((IQueryable<T>)itable, cancellationToken);
        }

        /// <summary>
        /// Asynchronously returns the single element from the provided <see cref="SxmTable{T}"/>.
        /// </summary>
        public static Task<T> SingleAsync<T>(this SxmTable<T> table, CancellationToken cancellationToken = default)
            where T : class
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            var itable = table.AsITable() ?? throw new InvalidOperationException("Operation requires LinqToDB ITable<T>.");
            return LinqToDB.AsyncExtensions.SingleAsync((IQueryable<T>)itable, cancellationToken);
        }

        /// <summary>
        /// Asynchronously returns the single element or default from the provided <see cref="SxmTable{T}"/>.
        /// </summary>
        public static Task<T?> SingleOrDefaultAsync<T>(this SxmTable<T> table, CancellationToken cancellationToken = default)
            where T : class
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            var itable = table.AsITable() ?? throw new InvalidOperationException("Operation requires LinqToDB ITable<T>.");
            return LinqToDB.AsyncExtensions.SingleOrDefaultAsync((IQueryable<T>)itable, cancellationToken);
        }

        /// <summary>
        /// Asynchronously counts elements from the provided <see cref="SxmTable{T}"/>.
        /// </summary>
        public static Task<int> CountAsync<T>(this SxmTable<T> table, CancellationToken cancellationToken = default)
            where T : class
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            var itable = table.AsITable() ?? throw new InvalidOperationException("Operation requires LinqToDB ITable<T>.");
            return LinqToDB.AsyncExtensions.CountAsync((IQueryable<T>)itable, cancellationToken);
        }

        /// <summary>
        /// Asynchronously determines whether any element exists in the provided <see cref="SxmTable{T}"/>.
        /// </summary>
        public static Task<bool> AnyAsync<T>(this SxmTable<T> table, CancellationToken cancellationToken = default)
            where T : class
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            var itable = table.AsITable() ?? throw new InvalidOperationException("Operation requires LinqToDB ITable<T>.");
            return LinqToDB.AsyncExtensions.AnyAsync((IQueryable<T>)itable, cancellationToken);
        }

        /// <summary>
        /// Asynchronously determines whether all elements satisfy the predicate for the provided <see cref="SxmTable{T}"/>.
        /// </summary>
        public static Task<bool> AllAsync<T>(this SxmTable<T> table, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            where T : class
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            var itable = table.AsITable() ?? throw new InvalidOperationException("Operation requires LinqToDB ITable<T>.");
            return LinqToDB.AsyncExtensions.AllAsync((IQueryable<T>)itable, predicate, cancellationToken);
        }

        /// <summary>
        /// Asynchronously returns the maximum value from the provided <see cref="SxmTable{T}"/> using the specified selector.
        /// </summary>
        public static Task<TResult> MaxAsync<T, TResult>(this SxmTable<T> table, Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default)
            where T : class
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            var itable = table.AsITable() ?? throw new InvalidOperationException("Operation requires LinqToDB ITable<T>.");
            return LinqToDB.AsyncExtensions.MaxAsync((IQueryable<T>)itable, selector, cancellationToken);
        }

        /// <summary>
        /// Asynchronously returns the minimum value from the provided <see cref="SxmTable{T}"/> using the specified selector.
        /// </summary>
        public static Task<TResult> MinAsync<T, TResult>(this SxmTable<T> table, Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default)
            where T : class
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            var itable = table.AsITable() ?? throw new InvalidOperationException("Operation requires LinqToDB ITable<T>.");
            return LinqToDB.AsyncExtensions.MinAsync((IQueryable<T>)itable, selector, cancellationToken);
        }

        /// <summary>
        /// Asynchronously returns the number of elements in the provided <see cref="SxmTable{T}"/> as a 64-bit integer.
        /// </summary>
        public static Task<long> LongCountAsync<T>(this SxmTable<T> table, CancellationToken cancellationToken = default)
            where T : class
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            var itable = table.AsITable() ?? throw new InvalidOperationException("Operation requires LinqToDB ITable<T>.");
            return LinqToDB.AsyncExtensions.LongCountAsync((IQueryable<T>)itable, cancellationToken);
        }

        /// <summary>
        /// Asynchronously determines whether the sequence contains the specified element.
        /// </summary>
        public static Task<bool> ContainsAsync<T>(this SxmTable<T> table, T item, CancellationToken cancellationToken = default)
            where T : class
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            var itable = table.AsITable() ?? throw new InvalidOperationException("Operation requires LinqToDB ITable<T>.");
            return LinqToDB.AsyncExtensions.ContainsAsync((IQueryable<T>)itable, item, cancellationToken);
        }

        /// <summary>
        /// Enqueues a bulk delete operation to be executed during SubmitChangesAsync within the transaction.
        /// All bulk deletes participate in the same transaction as entity operations.
        /// For single-entity deletes, prefer <see cref="SxmLinqContext.DeleteOnSubmit"/> + <see cref="SxmLinqContext.SubmitChangesAsync"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when no SxmLinqContext is available.</exception>
        public static Task<int> DeleteAsync<T>(this SxmTable<T> table, CancellationToken cancellationToken = default)
            where T : class
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            var itable = table.AsITable() ?? throw new InvalidOperationException("Operation requires LinqToDB ITable<T>.");

            if (table.DataContext == null)
                throw new InvalidOperationException(
                    "Bulk delete operations require a SxmLinqContext. " +
                    "Use ctx.GetTable<T>() to obtain a context-aware table, then call DeleteAsync(). " +
                    "Bulk deletes must participate in the SubmitChangesAsync() transaction.");

            // Defer execution - enqueue into context's change set
            table.DataContext.EnqueueBulkDelete(() => Task.FromResult(LinqToDB.LinqExtensions.Delete<T>((IQueryable<T>)itable)));
            return Task.FromResult(0); // Return placeholder; actual count available after SubmitChangesAsync
        }

        // ---------- Forwarding overloads for IQueryable<T> (keeps fluent chaining) ----------

        /// <summary>
        /// Dump candidate static LinqToDB helper methods with the given name to Debug output.
        /// Use this to diagnose why reflection resolution fails (for example "Set").
        /// </summary>
        public static void DumpProviderCandidates(string methodName)
        {
            if (string.IsNullOrEmpty(methodName)) throw new ArgumentNullException(nameof(methodName));
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a != null && (a.GetName().Name?.StartsWith("LinqToDB", StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();

            // prefer the DataConnection assembly
            if (!assemblies.Contains(_linqToDbAssembly))
            {
                assemblies.Insert(0, _linqToDbAssembly);
            }
            else
            {
                assemblies.Remove(_linqToDbAssembly);
                assemblies.Insert(0, _linqToDbAssembly);
            }

            Debug.WriteLine($"[SxmLinqExtensions] DumpProviderCandidates('{methodName}') scanning {assemblies.Count} assembly(ies).");
            foreach (var asm in assemblies)
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SxmLinqExtensions] Could not get types from {asm.FullName}: {ex.GetType().Name}: {ex.Message}");
                    continue;
                }

                foreach (var t in types.Where(tt => tt.IsSealed && tt.IsAbstract))
                {
                    foreach (var m in t.GetMethods(BindingFlags.Static | BindingFlags.Public))
                    {
                        if (!string.Equals(m.Name, methodName, StringComparison.Ordinal)) continue;

                        var paramList = string.Join(", ", m.GetParameters()
                            .Select(p => $"{(p.ParameterType.IsGenericType ? p.ParameterType.GetGenericTypeDefinition().Name : p.ParameterType.Name)} {p.Name}"));
                        var genericInfo = m.IsGenericMethodDefinition ? $"<g{m.GetGenericArguments().Length}>" : "";
                        Debug.WriteLine($"[SxmLinqExtensions] Found: {t.FullName}.{m.Name}{genericInfo}({paramList}) -> DeclaringAssembly={asm.GetName().Name}");
                    }
                }
            }
        }



        /// <summary>
        /// Asynchronously materializes the query to a list (forwarding overload for IQueryable).
        /// </summary>
        public static Task<List<T>> ToListAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            return LinqToDB.AsyncExtensions.ToListAsync(query, cancellationToken);
        }

        /// <summary>
        /// Asynchronously materializes the query to an array (forwarding overload for IQueryable).
        /// </summary>
        public static Task<T[]> ToArrayAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            return LinqToDB.AsyncExtensions.ToArrayAsync(query, cancellationToken);
        }

        /// <summary>
        /// Asynchronously returns the first element (forwarding overload for IQueryable).
        /// </summary>
        public static Task<T> FirstAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            return LinqToDB.AsyncExtensions.FirstAsync(query, cancellationToken);
        }

        /// <summary>
        /// Asynchronously returns the first element or default (forwarding overload for IQueryable).
        /// </summary>
        public static Task<T?> FirstOrDefaultAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            return LinqToDB.AsyncExtensions.FirstOrDefaultAsync(query, cancellationToken);
        }

        /// <summary>
        /// Asynchronously returns the single element (forwarding overload for IQueryable).
        /// </summary>
        public static Task<T> SingleAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            return LinqToDB.AsyncExtensions.SingleAsync(query, cancellationToken);
        }

        /// <summary>
        /// Asynchronously returns the single element or default (forwarding overload for IQueryable).
        /// </summary>
        public static Task<T?> SingleOrDefaultAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            return LinqToDB.AsyncExtensions.SingleOrDefaultAsync(query, cancellationToken);
        }

        /// <summary>
        /// Asynchronously counts elements (forwarding overload for IQueryable).
        /// </summary>
        public static Task<int> CountAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            return LinqToDB.AsyncExtensions.CountAsync(query, cancellationToken);
        }

        /// <summary>
        /// Asynchronously determines whether any element exists (forwarding overload for IQueryable).
        /// </summary>
        public static Task<bool> AnyAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            return LinqToDB.AsyncExtensions.AnyAsync(query, cancellationToken);
        }

        /// <summary>
        /// Asynchronously determines whether all elements satisfy the predicate (forwarding overload for IQueryable).
        /// </summary>
        public static Task<bool> AllAsync<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            return LinqToDB.AsyncExtensions.AllAsync(query, predicate, cancellationToken);
        }

        /// <summary>
        /// Asynchronously returns the maximum value using the specified selector (forwarding overload for IQueryable).
        /// </summary>
        public static Task<TResult> MaxAsync<T, TResult>(this IQueryable<T> query, Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            return LinqToDB.AsyncExtensions.MaxAsync(query, selector, cancellationToken);
        }

        /// <summary>
        /// Asynchronously returns the minimum value using the specified selector (forwarding overload for IQueryable).
        /// </summary>
        public static Task<TResult> MinAsync<T, TResult>(this IQueryable<T> query, Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            return LinqToDB.AsyncExtensions.MinAsync(query, selector, cancellationToken);
        }

        /// <summary>
        /// Asynchronously returns the number of elements as a 64-bit integer (forwarding overload for IQueryable).
        /// </summary>
        public static Task<long> LongCountAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            return LinqToDB.AsyncExtensions.LongCountAsync(query, cancellationToken);
        }

        /// <summary>
        /// Asynchronously determines whether the sequence contains the specified element (forwarding overload for IQueryable).
        /// </summary>
        public static Task<bool> ContainsAsync<T>(this IQueryable<T> query, T item, CancellationToken cancellationToken = default)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            return LinqToDB.AsyncExtensions.ContainsAsync(query, item, cancellationToken);
        }

        /// <summary>
        /// Asynchronously deletes matching rows for the provided query.
        /// Automatically recovers SxmLinqContext from LINQ chains for transactional bulk deletes.
        /// </summary>
        public static Task<int> DeleteAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));

            // Try to recover context from the query (works for SxmTable and LinqToDB query chains)
            var context = SxmLinqContext.TryGetContextFromQuery(query);

            if (context != null)
            {
                // Defer execution - enqueue into context's change set
                context.EnqueueBulkDelete(() => Task.FromResult(LinqToDB.LinqExtensions.Delete<T>(query)));
                return Task.FromResult(0); // Return placeholder; actual count available after SubmitChangesAsync
            }

            // No context available - execute immediately (for backward compatibility with direct LinqToDB usage)
            return Task.FromResult(LinqToDB.LinqExtensions.Delete<T>(query));
        }

        /// <summary>
        /// Asynchronously computes the average over the provided selector for the supplied <see cref="SxmTable{T}"/> returning double.
        /// </summary>
        public static Task<double> AverageAsync<T>(this SxmTable<T> table, Expression<Func<T, double>> selector, CancellationToken cancellationToken = default)
            where T : class
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            var itable = table.AsITable() ?? throw new InvalidOperationException("Operation requires LinqToDB ITable<T>.");
            return LinqToDB.AsyncExtensions.AverageAsync((IQueryable<T>)itable, selector, cancellationToken);
        }

        /// <summary>
        /// Asynchronously computes the average over the provided selector for the supplied <see cref="SxmTable{T}"/> returning nullable double.
        /// </summary>
        public static Task<double?> AverageAsync<T>(this SxmTable<T> table, Expression<Func<T, double?>> selector, CancellationToken cancellationToken = default)
            where T : class
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            var itable = table.AsITable() ?? throw new InvalidOperationException("Operation requires LinqToDB ITable<T>.");
            return LinqToDB.AsyncExtensions.AverageAsync((IQueryable<T>)itable, selector, cancellationToken);
        }

        /// <summary>
        /// Asynchronously computes the average over the provided selector for the supplied <see cref="SxmTable{T}"/> returning float.
        /// </summary>
        public static Task<float> AverageAsync<T>(this SxmTable<T> table, Expression<Func<T, float>> selector, CancellationToken cancellationToken = default)
            where T : class
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            var itable = table.AsITable() ?? throw new InvalidOperationException("Operation requires LinqToDB ITable<T>.");
            return LinqToDB.AsyncExtensions.AverageAsync((IQueryable<T>)itable, selector, cancellationToken);
        }

        /// <summary>
        /// Asynchronously computes the average over the provided selector for the supplied <see cref="SxmTable{T}"/> returning nullable float.
        /// </summary>
        public static Task<float?> AverageAsync<T>(this SxmTable<T> table, Expression<Func<T, float?>> selector, CancellationToken cancellationToken = default)
            where T : class
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            var itable = table.AsITable() ?? throw new InvalidOperationException("Operation requires LinqToDB ITable<T>.");
            return LinqToDB.AsyncExtensions.AverageAsync((IQueryable<T>)itable, selector, cancellationToken);
        }

        /// <summary>
        /// Asynchronously computes the average over the provided selector for the supplied <see cref="SxmTable{T}"/> returning decimal.
        /// </summary>
        public static Task<decimal> AverageAsync<T>(this SxmTable<T> table, Expression<Func<T, decimal>> selector, CancellationToken cancellationToken = default)
            where T : class
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            var itable = table.AsITable() ?? throw new InvalidOperationException("Operation requires LinqToDB ITable<T>.");
            return LinqToDB.AsyncExtensions.AverageAsync((IQueryable<T>)itable, selector, cancellationToken);
        }

        /// <summary>
        /// Asynchronously computes the average over the provided selector for the supplied <see cref="SxmTable{T}"/> returning nullable decimal.
        /// </summary>
        public static Task<decimal?> AverageAsync<T>(this SxmTable<T> table, Expression<Func<T, decimal?>> selector, CancellationToken cancellationToken = default)
            where T : class
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            var itable = table.AsITable() ?? throw new InvalidOperationException("Operation requires LinqToDB ITable<T>.");
            return LinqToDB.AsyncExtensions.AverageAsync((IQueryable<T>)itable, selector, cancellationToken);
        }

        // ---------- forwarding IQueryable overloads ----------

        public static Task<double> AverageAsync<T>(this IQueryable<T> query, Expression<Func<T, double>> selector, CancellationToken cancellationToken = default)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            return LinqToDB.AsyncExtensions.AverageAsync(query, selector, cancellationToken);
        }

        public static Task<double?> AverageAsync<T>(this IQueryable<T> query, Expression<Func<T, double?>> selector, CancellationToken cancellationToken = default)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            return LinqToDB.AsyncExtensions.AverageAsync(query, selector, cancellationToken);
        }

        public static Task<float> AverageAsync<T>(this IQueryable<T> query, Expression<Func<T, float>> selector, CancellationToken cancellationToken = default)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            return LinqToDB.AsyncExtensions.AverageAsync(query, selector, cancellationToken);
        }

        public static Task<float?> AverageAsync<T>(this IQueryable<T> query, Expression<Func<T, float?>> selector, CancellationToken cancellationToken = default)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            return LinqToDB.AsyncExtensions.AverageAsync(query, selector, cancellationToken);
        }

        public static Task<decimal> AverageAsync<T>(this IQueryable<T> query, Expression<Func<T, decimal>> selector, CancellationToken cancellationToken = default)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            return LinqToDB.AsyncExtensions.AverageAsync(query, selector, cancellationToken);
        }

        public static Task<decimal?> AverageAsync<T>(this IQueryable<T> query, Expression<Func<T, decimal?>> selector, CancellationToken cancellationToken = default)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            return LinqToDB.AsyncExtensions.AverageAsync(query, selector, cancellationToken);
        }

        /// <summary>
        /// Asynchronously computes the sum over the provided selector for the supplied <see cref="SxmTable{T}"/> returning int.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="table">Table to query.</param>
        /// <param name="selector">Selector expression that returns int.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that returns the sum result.</returns>
        public static Task<int> SumAsync<T>(this SxmTable<T> table, Expression<Func<T, int>> selector, CancellationToken cancellationToken = default)
            where T : class
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            var itable = table.AsITable() ?? throw new InvalidOperationException("Operation requires LinqToDB ITable<T>.");
            return LinqToDB.AsyncExtensions.SumAsync((IQueryable<T>)itable, selector, cancellationToken);
        }

        /// <summary>
        /// Asynchronously computes the sum over the provided selector for the supplied <see cref="SxmTable{T}"/> returning nullable int.
        /// </summary>
        public static Task<int?> SumAsync<T>(this SxmTable<T> table, Expression<Func<T, int?>> selector, CancellationToken cancellationToken = default)
            where T : class
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            var itable = table.AsITable() ?? throw new InvalidOperationException("Operation requires LinqToDB ITable<T>.");
            return LinqToDB.AsyncExtensions.SumAsync((IQueryable<T>)itable, selector, cancellationToken);
        }

        /// <summary>
        /// Asynchronously computes the sum over the provided selector for the supplied <see cref="SxmTable{T}"/> returning long.
        /// </summary>
        public static Task<long> SumAsync<T>(this SxmTable<T> table, Expression<Func<T, long>> selector, CancellationToken cancellationToken = default)
            where T : class
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            var itable = table.AsITable() ?? throw new InvalidOperationException("Operation requires LinqToDB ITable<T>.");
            return LinqToDB.AsyncExtensions.SumAsync((IQueryable<T>)itable, selector, cancellationToken);
        }

        /// <summary>
        /// Asynchronously computes the sum over the provided selector for the supplied <see cref="SxmTable{T}"/> returning nullable long.
        /// </summary>
        public static Task<long?> SumAsync<T>(this SxmTable<T> table, Expression<Func<T, long?>> selector, CancellationToken cancellationToken = default)
            where T : class
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            var itable = table.AsITable() ?? throw new InvalidOperationException("Operation requires LinqToDB ITable<T>.");
            return LinqToDB.AsyncExtensions.SumAsync((IQueryable<T>)itable, selector, cancellationToken);
        }

        /// <summary>
        /// Asynchronously computes the sum over the provided selector for the supplied <see cref="SxmTable{T}"/> returning float.
        /// </summary>
        public static Task<float> SumAsync<T>(this SxmTable<T> table, Expression<Func<T, float>> selector, CancellationToken cancellationToken = default)
            where T : class
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            var itable = table.AsITable() ?? throw new InvalidOperationException("Operation requires LinqToDB ITable<T>.");
            return LinqToDB.AsyncExtensions.SumAsync((IQueryable<T>)itable, selector, cancellationToken);
        }

        /// <summary>
        /// Asynchronously computes the sum over the provided selector for the supplied <see cref="SxmTable{T}"/> returning nullable float.
        /// </summary>
        public static Task<float?> SumAsync<T>(this SxmTable<T> table, Expression<Func<T, float?>> selector, CancellationToken cancellationToken = default)
            where T : class
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            var itable = table.AsITable() ?? throw new InvalidOperationException("Operation requires LinqToDB ITable<T>.");
            return LinqToDB.AsyncExtensions.SumAsync((IQueryable<T>)itable, selector, cancellationToken);
        }

        /// <summary>
        /// Asynchronously computes the sum over the provided selector for the supplied <see cref="SxmTable{T}"/> returning double.
        /// </summary>
        public static Task<double> SumAsync<T>(this SxmTable<T> table, Expression<Func<T, double>> selector, CancellationToken cancellationToken = default)
            where T : class
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            var itable = table.AsITable() ?? throw new InvalidOperationException("Operation requires LinqToDB ITable<T>.");
            return LinqToDB.AsyncExtensions.SumAsync((IQueryable<T>)itable, selector, cancellationToken);
        }

        /// <summary>
        /// Asynchronously computes the sum over the provided selector for the supplied <see cref="SxmTable{T}"/> returning nullable double.
        /// </summary>
        public static Task<double?> SumAsync<T>(this SxmTable<T> table, Expression<Func<T, double?>> selector, CancellationToken cancellationToken = default)
            where T : class
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            var itable = table.AsITable() ?? throw new InvalidOperationException("Operation requires LinqToDB ITable<T>.");
            return LinqToDB.AsyncExtensions.SumAsync((IQueryable<T>)itable, selector, cancellationToken);
        }

        /// <summary>
        /// Asynchronously computes the sum over the provided selector for the supplied <see cref="SxmTable{T}"/> returning decimal.
        /// </summary>
        public static Task<decimal> SumAsync<T>(this SxmTable<T> table, Expression<Func<T, decimal>> selector, CancellationToken cancellationToken = default)
            where T : class
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            var itable = table.AsITable() ?? throw new InvalidOperationException("Operation requires LinqToDB ITable<T>.");
            return LinqToDB.AsyncExtensions.SumAsync((IQueryable<T>)itable, selector, cancellationToken);
        }

        /// <summary>
        /// Asynchronously computes the sum over the provided selector for the supplied <see cref="SxmTable{T}"/> returning nullable decimal.
        /// </summary>
        public static Task<decimal?> SumAsync<T>(this SxmTable<T> table, Expression<Func<T, decimal?>> selector, CancellationToken cancellationToken = default)
            where T : class
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            var itable = table.AsITable() ?? throw new InvalidOperationException("Operation requires LinqToDB ITable<T>.");
            return LinqToDB.AsyncExtensions.SumAsync((IQueryable<T>)itable, selector, cancellationToken);
        }

        // ---------- forwarding IQueryable overloads ----------

        /// <summary>
        /// Forwarding overload for <see cref="IQueryable{T}"/> to compute sum asynchronously (int).
        /// </summary>
        public static Task<int> SumAsync<T>(this IQueryable<T> query, Expression<Func<T, int>> selector, CancellationToken cancellationToken = default)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            return LinqToDB.AsyncExtensions.SumAsync(query, selector, cancellationToken);
        }

        /// <summary>
        /// Forwarding overload for <see cref="IQueryable{T}"/> to compute sum asynchronously (int?).
        /// </summary>
        public static Task<int?> SumAsync<T>(this IQueryable<T> query, Expression<Func<T, int?>> selector, CancellationToken cancellationToken = default)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            return LinqToDB.AsyncExtensions.SumAsync(query, selector, cancellationToken);
        }

        /// <summary>
        /// Forwarding overload for <see cref="IQueryable{T}"/> to compute sum asynchronously (long).
        /// </summary>
        public static Task<long> SumAsync<T>(this IQueryable<T> query, Expression<Func<T, long>> selector, CancellationToken cancellationToken = default)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            return LinqToDB.AsyncExtensions.SumAsync(query, selector, cancellationToken);
        }

        /// <summary>
        /// Forwarding overload for <see cref="IQueryable{T}"/> to compute sum asynchronously (long?).
        /// </summary>
        public static Task<long?> SumAsync<T>(this IQueryable<T> query, Expression<Func<T, long?>> selector, CancellationToken cancellationToken = default)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            return LinqToDB.AsyncExtensions.SumAsync(query, selector, cancellationToken);
        }

        /// <summary>
        /// Forwarding overload for <see cref="IQueryable{T}"/> to compute sum asynchronously (float).
        /// </summary>
        public static Task<float> SumAsync<T>(this IQueryable<T> query, Expression<Func<T, float>> selector, CancellationToken cancellationToken = default)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            return LinqToDB.AsyncExtensions.SumAsync(query, selector, cancellationToken);
        }

        /// <summary>
        /// Forwarding overload for <see cref="IQueryable{T}"/> to compute sum asynchronously (float?).
        /// </summary>
        public static Task<float?> SumAsync<T>(this IQueryable<T> query, Expression<Func<T, float?>> selector, CancellationToken cancellationToken = default)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            return LinqToDB.AsyncExtensions.SumAsync(query, selector, cancellationToken);
        }

        /// <summary>
        /// Forwarding overload for <see cref="IQueryable{T}"/> to compute sum asynchronously (double).
        /// </summary>
        public static Task<double> SumAsync<T>(this IQueryable<T> query, Expression<Func<T, double>> selector, CancellationToken cancellationToken = default)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            return LinqToDB.AsyncExtensions.SumAsync(query, selector, cancellationToken);
        }

        /// <summary>
        /// Forwarding overload for <see cref="IQueryable{T}"/> to compute sum asynchronously (double?).
        /// </summary>
        public static Task<double?> SumAsync<T>(this IQueryable<T> query, Expression<Func<T, double?>> selector, CancellationToken cancellationToken = default)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            return LinqToDB.AsyncExtensions.SumAsync(query, selector, cancellationToken);
        }

        /// <summary>
        /// Forwarding overload for <see cref="IQueryable{T}"/> to compute sum asynchronously (decimal).
        /// </summary>
        public static Task<decimal> SumAsync<T>(this IQueryable<T> query, Expression<Func<T, decimal>> selector, CancellationToken cancellationToken = default)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            return LinqToDB.AsyncExtensions.SumAsync(query, selector, cancellationToken);
        }

        /// <summary>
        /// Forwarding overload for <see cref="IQueryable{T}"/> to compute sum asynchronously (decimal?).
        /// </summary>
        public static Task<decimal?> SumAsync<T>(this IQueryable<T> query, Expression<Func<T, decimal?>> selector, CancellationToken cancellationToken = default)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            return LinqToDB.AsyncExtensions.SumAsync(query, selector, cancellationToken);
        }

        /// <summary>
        /// Asynchronously materializes the query to a dictionary using the specified key selector.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <typeparam name="TKey">Key type.</typeparam>
        /// <param name="table">Table to materialize.</param>
        /// <param name="keySelector">Key selector expression.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that returns the dictionary.</returns>
        public static async Task<Dictionary<TKey, T>> ToDictionaryAsync<T, TKey>(this SxmTable<T> table, Expression<Func<T, TKey>> keySelector, CancellationToken cancellationToken = default)
            where T : class
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            var itable = table.AsITable() ?? throw new InvalidOperationException("Operation requires LinqToDB ITable<T>.");

            // Materialize rows asynchronously then build dictionary client-side to avoid provider overload mismatches.
            var list = await LinqToDB.AsyncExtensions.ToListAsync((IQueryable<T>)itable, cancellationToken).ConfigureFalse();
            var keyFunc = keySelector.Compile();

            var dict = new Dictionary<TKey, T>();
            foreach (var item in list)
            {
                var k = keyFunc(item);
                if (dict.ContainsKey(k))
                    throw new ArgumentException("An item with the same key has already been added.", nameof(keySelector));
                dict[k] = item;
            }
            return dict;
        }

        /// <summary>
        /// Asynchronously materializes the query to a dictionary using the specified key and element selectors.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <typeparam name="TKey">Key type.</typeparam>
        /// <typeparam name="TElement">Element type.</typeparam>
        /// <param name="table">Table to materialize.</param>
        /// <param name="keySelector">Key selector expression.</param>
        /// <param name="elementSelector">Element selector expression.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that returns the dictionary.</returns>
        public static async Task<Dictionary<TKey, TElement>> ToDictionaryAsync<T, TKey, TElement>(this SxmTable<T> table, Expression<Func<T, TKey>> keySelector, Expression<Func<T, TElement>> elementSelector, CancellationToken cancellationToken = default)
            where T : class
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));
            if (elementSelector == null) throw new ArgumentNullException(nameof(elementSelector));

            var itable = table.AsITable() ?? throw new InvalidOperationException("Operation requires LinqToDB ITable<T>.");

            var list = await LinqToDB.AsyncExtensions.ToListAsync((IQueryable<T>)itable, cancellationToken).ConfigureFalse();
            var keyFunc = keySelector.Compile();
            var elemFunc = elementSelector.Compile();

            var dict = new Dictionary<TKey, TElement>();
            foreach (var item in list)
            {
                var k = keyFunc(item);
                if (dict.ContainsKey(k))
                    throw new ArgumentException("An item with the same key has already been added.", nameof(keySelector));
                dict[k] = elemFunc(item);
            }
            return dict;
        }

        /// <summary>
        /// Forwarding overload for <see cref="IQueryable{T}"/> to produce a dictionary asynchronously.
        /// </summary>
        public static async Task<Dictionary<TKey, T>> ToDictionaryAsync<T, TKey>(this IQueryable<T> query, Expression<Func<T, TKey>> keySelector, CancellationToken cancellationToken = default)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            var list = await LinqToDB.AsyncExtensions.ToListAsync(query, cancellationToken).ConfigureFalse();
            var keyFunc = keySelector.Compile();

            var dict = new Dictionary<TKey, T>();
            foreach (var item in list)
            {
                var k = keyFunc(item);
                if (dict.ContainsKey(k))
                    throw new ArgumentException("An item with the same key has already been added.", nameof(keySelector));
                dict[k] = item;
            }
            return dict;
        }

        /// <summary>
        /// Forwarding overload for <see cref="IQueryable{T}"/> to produce a dictionary asynchronously with element selector.
        /// </summary>
        public static async Task<Dictionary<TKey, TElement>> ToDictionaryAsync<T, TKey, TElement>(this IQueryable<T> query, Expression<Func<T, TKey>> keySelector, Expression<Func<T, TElement>> elementSelector, CancellationToken cancellationToken = default)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));
            if (elementSelector == null) throw new ArgumentNullException(nameof(elementSelector));

            var list = await LinqToDB.AsyncExtensions.ToListAsync(query, cancellationToken).ConfigureFalse();
            var keyFunc = keySelector.Compile();
            var elemFunc = elementSelector.Compile();

            var dict = new Dictionary<TKey, TElement>();
            foreach (var item in list)
            {
                var k = keyFunc(item);
                if (dict.ContainsKey(k))
                    throw new ArgumentException("An item with the same key has already been added.", nameof(keySelector));
                dict[k] = elemFunc(item);
            }
            return dict;
        }

        // Overloads with predicate.

        /// <summary>
        /// Asynchronously returns the first element that matches the predicate from the provided <see cref="SxmTable{T}"/>.
        /// </summary>
        public static Task<T> FirstAsync<T>(this SxmTable<T> table, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            where T : class
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            var itable = table.AsITable() ?? throw new InvalidOperationException("Operation requires LinqToDB ITable<T>.");
            return LinqToDB.AsyncExtensions.FirstAsync((IQueryable<T>)itable, predicate, cancellationToken);
        }

        /// <summary>
        /// Asynchronously returns the first element that matches the predicate or default from the provided <see cref="SxmTable{T}"/>.
        /// </summary>
        public static Task<T?> FirstOrDefaultAsync<T>(this SxmTable<T> table, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            where T : class
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            var itable = table.AsITable() ?? throw new InvalidOperationException("Operation requires LinqToDB ITable<T>.");
            return LinqToDB.AsyncExtensions.FirstOrDefaultAsync((IQueryable<T>)itable, predicate, cancellationToken);
        }

        /// <summary>
        /// Asynchronously returns the single element that matches the predicate from the provided <see cref="SxmTable{T}"/>.
        /// </summary>
        public static Task<T> SingleAsync<T>(this SxmTable<T> table, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            where T : class
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            var itable = table.AsITable() ?? throw new InvalidOperationException("Operation requires LinqToDB ITable<T>.");
            return LinqToDB.AsyncExtensions.SingleAsync((IQueryable<T>)itable, predicate, cancellationToken);
        }

        /// <summary>
        /// Asynchronously returns the single element that matches the predicate or default from the provided <see cref="SxmTable{T}"/>.
        /// </summary>
        public static Task<T?> SingleOrDefaultAsync<T>(this SxmTable<T> table, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            where T : class
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            var itable = table.AsITable() ?? throw new InvalidOperationException("Operation requires LinqToDB ITable<T>.");
            return LinqToDB.AsyncExtensions.SingleOrDefaultAsync((IQueryable<T>)itable, predicate, cancellationToken);
        }

        /// <summary>
        /// Asynchronously determines whether any element that matches the predicate exists in the provided <see cref="SxmTable{T}"/>.
        /// </summary>
        public static Task<bool> AnyAsync<T>(this SxmTable<T> table, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            where T : class
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            var itable = table.AsITable() ?? throw new InvalidOperationException("Operation requires LinqToDB ITable<T>.");
            return LinqToDB.AsyncExtensions.AnyAsync((IQueryable<T>)itable, predicate, cancellationToken);
        }

        /// <summary>
        /// Asynchronously counts elements that match the predicate from the provided <see cref="SxmTable{T}"/>.
        /// </summary>
        public static Task<int> CountAsync<T>(this SxmTable<T> table, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            where T : class
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            var itable = table.AsITable() ?? throw new InvalidOperationException("Operation requires LinqToDB ITable<T>.");
            return LinqToDB.AsyncExtensions.CountAsync((IQueryable<T>)itable, predicate, cancellationToken);
        }

        // ---------- forwarding IQueryable overloads with predicate ----------

        public static Task<T> FirstAsync<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            return LinqToDB.AsyncExtensions.FirstAsync(query, predicate, cancellationToken);
        }

        public static Task<T?> FirstOrDefaultAsync<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            return LinqToDB.AsyncExtensions.FirstOrDefaultAsync(query, predicate, cancellationToken);
        }

        public static Task<T> SingleAsync<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            return LinqToDB.AsyncExtensions.SingleAsync(query, predicate, cancellationToken);
        }

        public static Task<T?> SingleOrDefaultAsync<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            return LinqToDB.AsyncExtensions.SingleOrDefaultAsync(query, predicate, cancellationToken);
        }

        public static Task<bool> AnyAsync<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            return LinqToDB.AsyncExtensions.AnyAsync(query, predicate, cancellationToken);
        }

        public static Task<int> CountAsync<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            return LinqToDB.AsyncExtensions.CountAsync(query, predicate, cancellationToken);
        }
    }
}