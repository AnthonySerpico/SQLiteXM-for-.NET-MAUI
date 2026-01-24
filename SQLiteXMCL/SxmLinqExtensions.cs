using LinqToDB;
using LinqToDB.Linq;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;

namespace SQLiteXM
{
    /// <summary>
    /// Lightweight wrapper over LinqToDB's update builder so callers that import
    /// <see cref="SQLiteXM"/> don't need to import <c>LinqToDB</c>.
    /// </summary>
    /// <typeparam name="T">Entity type.</typeparam>
    public sealed class SxmUpdateSet<T> where T : class
    {
        private readonly IUpdatable<T> _inner;

        /// <summary>
        /// Creates a new wrapper around the LinqToDB update builder instance.
        /// </summary>
        /// <param name="inner">The LinqToDB update builder instance.</param>
        public SxmUpdateSet(IUpdatable<T> inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
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
            return new SxmUpdateSet<T>(next);
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
            return new SxmUpdateSet<T>(next);
        }

        /// <summary>
        /// Executes the update asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of rows affected.</returns>
        public Task<int> UpdateAsync(CancellationToken cancellationToken = default)
        {
            return _inner.UpdateAsync(cancellationToken);
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
        private static readonly Assembly LinqToDbAssembly = typeof(LinqToDB.Data.DataConnection).Assembly;

        /// <summary>
        /// Starts a LinqToDB update builder for the supplied <see cref="SxmTable{T}"/>.
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
            return new SxmUpdateSet<T>((IUpdatable<T>)updatable!);
        }

        /// <summary>
        /// Starts a LinqToDB update builder for the supplied <see cref="SxmTable{T}"/> using expression value provider.
        /// </summary>
        public static SxmUpdateSet<T> Set<T, TProp>(this SxmTable<T> query, Expression<Func<T, TProp>> setter, Expression<Func<T, TProp>> expression)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (setter == null) throw new ArgumentNullException(nameof(setter));
            if (expression == null) throw new ArgumentNullException(nameof(expression));

            var itable = query.AsITable() ?? throw new InvalidOperationException("Operation requires LinqToDB ITable<T>.");

            var updatable = LinqToDB.LinqExtensions.Set<T, TProp>((IQueryable<T>)itable, setter, expression);
            return new SxmUpdateSet<T>((IUpdatable<T>)updatable!);
        }

        /// <summary>
        /// Starts a LinqToDB update builder for the supplied <see cref="IQueryable{T}"/>.
        /// Forwards to LinqToDB directly when the query is not an SxmTable.
        /// </summary>
        public static SxmUpdateSet<T> Set<T, TProp>(this IQueryable<T> query, Expression<Func<T, TProp>> setter, TProp value)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (setter == null) throw new ArgumentNullException(nameof(setter));

            if (query is SxmTable<T> sxmTable)
                return sxmTable.Set(setter, value);

            // Call LinqToDB helper directly using the IQueryable overload.
            var updatable = LinqToDB.LinqExtensions.Set<T, TProp>(query, setter, value);
            return new SxmUpdateSet<T>((IUpdatable<T>)updatable!);
        }

        /// <summary>
        /// Starts a LinqToDB update builder for the supplied <see cref="IQueryable{T}"/> using expression value provider.
        /// Forwards to LinqToDB directly when the query is not an SxmTable.
        /// </summary>
        public static SxmUpdateSet<T> Set<T, TProp>(this IQueryable<T> query, Expression<Func<T, TProp>> setter, Expression<Func<T, TProp>> expression)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (setter == null) throw new ArgumentNullException(nameof(setter));
            if (expression == null) throw new ArgumentNullException(nameof(expression));

            if (query is SxmTable<T> sxmTable)
                return sxmTable.Set(setter, expression);

            var updatable = LinqToDB.LinqExtensions.Set<T, TProp>(query, setter, expression);
            return new SxmUpdateSet<T>((IUpdatable<T>)updatable!);
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
        /// Asynchronously deletes matching rows from the provided <see cref="SxmTable{T}"/>.
        /// </summary>
        public static Task<int> DeleteAsync<T>(this SxmTable<T> table, CancellationToken cancellationToken = default)
            where T : class
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            var itable = table.AsITable() ?? throw new InvalidOperationException("Operation requires LinqToDB ITable<T>.");

            // Run synchronous provider delete on threadpool so API stays non-blocking when no async overload exists in Linq2DB package.
            return Task.Run(() => LinqToDB.LinqExtensions.Delete<T>((IQueryable<T>)itable), cancellationToken);
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
            if (!assemblies.Contains(LinqToDbAssembly))
            {
                assemblies.Insert(0, LinqToDbAssembly);
            }
            else
            {
                assemblies.Remove(LinqToDbAssembly);
                assemblies.Insert(0, LinqToDbAssembly);
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
            return LinqToDB.AsyncExtensions.SingleOrDefaultAsync(query,cancellationToken);
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
        /// Asynchronously deletes matching rows for the provided query (forwarding overload for IQueryable).
        /// </summary>
        public static Task<int> DeleteAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default)
            where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (query is SxmTable<T> sxmTable)
                return sxmTable.DeleteAsync(cancellationToken);

            return Task.Run(() => LinqToDB.LinqExtensions.Delete<T>(query), cancellationToken);
        }
    }
}