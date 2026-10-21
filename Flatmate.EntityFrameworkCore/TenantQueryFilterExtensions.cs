using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Microsoft.EntityFrameworkCore;

/// <summary>Query filters that follow the tenant selected for the current scope.</summary>
public static class TenantQueryFilterExtensions
{
    private static readonly MethodInfo PropertyMethod = typeof(EF)
        .GetMethod(nameof(EF.Property), 1, [typeof(object), typeof(string)])!;

    /// <summary>
    /// Filters <typeparamref name="TEntity"/> to the current tenant.
    /// The filter is named <see cref="TenantQueryFilter.Name"/>, so a query can skip it with
    /// <c>IgnoreQueryFilters</c> without turning off any other filters.
    /// <paramref name="currentTenantKey"/> must be a property on the <see cref="DbContext"/>,
    /// for example <c>() =&gt; TenantId</c>. EF Core reads it again for every query.
    /// </summary>
    public static EntityTypeBuilder<TEntity> HasTenantQueryFilter<TEntity, TKey>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, TKey>> entityTenantKey,
        Expression<Func<TKey>> currentTenantKey)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(entityTenantKey);
        ArgumentNullException.ThrowIfNull(currentTenantKey);
        EnsureContextMember(currentTenantKey.Body);

        var body = Expression.Equal(entityTenantKey.Body, currentTenantKey.Body);
        var filter = Expression.Lambda<Func<TEntity, bool>>(body, entityTenantKey.Parameters);
        return builder.HasQueryFilter(TenantQueryFilter.Name, filter);
    }

    /// <summary>
    /// Adds the same tenant filter to every mapped entity with a property of type <typeparamref name="TKey"/>
    /// named <paramref name="propertyName"/>. Call this after the entities are configured.
    /// <paramref name="currentTenantKey"/> must be a property on the <see cref="DbContext"/>.
    /// </summary>
    public static ModelBuilder ApplyTenantQueryFilter<TKey>(
        this ModelBuilder modelBuilder,
        Expression<Func<TKey>> currentTenantKey,
        string propertyName = "TenantId")
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentNullException.ThrowIfNull(currentTenantKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        EnsureContextMember(currentTenantKey.Body);

        var propertyMethod = PropertyMethod.MakeGenericMethod(typeof(TKey));

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
        {
            if (entityType.ClrType is not { } clrType || entityType.IsOwned())
            {
                continue;
            }

            var property = entityType.FindProperty(propertyName);
            if (property is null || property.ClrType != typeof(TKey) || !Equals(property.DeclaringType, entityType))
            {
                continue;
            }

            var parameter = Expression.Parameter(clrType, "entity");
            var tenantKey = Expression.Call(
                propertyMethod,
                Expression.Convert(parameter, typeof(object)),
                Expression.Constant(propertyName));
            var body = Expression.Equal(tenantKey, currentTenantKey.Body);
            var filter = Expression.Lambda(body, parameter);
            modelBuilder.Entity(clrType).HasQueryFilter(TenantQueryFilter.Name, filter);
        }

        return modelBuilder;
    }

    private static void EnsureContextMember(Expression body)
    {
        if (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
        {
            body = unary.Operand;
        }

        if (body is MemberExpression { Expression: ConstantExpression { Value: DbContext } })
        {
            return;
        }

        throw new ArgumentException(
            "Pass a property or field on the DbContext, for example () => TenantId. EF Core reads that member for each query.",
            "currentTenantKey");
    }
}
