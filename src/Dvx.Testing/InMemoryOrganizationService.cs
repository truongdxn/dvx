using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Dvx.Testing
{
    /// <summary>
    /// Minimal in-memory <see cref="IOrganizationService"/> for plugin unit tests. Not thread-safe (test helper).
    /// Real Dataverse throws a FaultException for missing or duplicate records; this fake throws
    /// <see cref="InvalidOperationException"/> instead and does not emulate fault shapes.
    /// Anything it does not support throws <see cref="NotSupportedException"/>.
    /// </summary>
    internal sealed class InMemoryOrganizationService : IOrganizationService
    {
        private readonly Dictionary<string, Dictionary<Guid, Entity>> _store =
            new Dictionary<string, Dictionary<Guid, Entity>>(StringComparer.Ordinal);

        /// <summary>Stores a clone of each entity (a new id is assigned when empty).</summary>
        public void Seed(params Entity[] entities)
        {
            if (entities == null) throw new ArgumentNullException("entities");
            foreach (var e in entities) Create(e);
        }

        /// <summary>Returns a clone of the stored record, or null.</summary>
        public Entity Get(string logicalName, Guid id)
        {
            Dictionary<Guid, Entity> table;
            Entity e;
            if (_store.TryGetValue(logicalName, out table) && table.TryGetValue(id, out e)) return Clone(e);
            return null;
        }

        public Guid Create(Entity entity)
        {
            if (entity == null) throw new ArgumentNullException("entity");
            var id = entity.Id != Guid.Empty ? entity.Id : Guid.NewGuid();
            Dictionary<Guid, Entity> table;
            if (!_store.TryGetValue(entity.LogicalName, out table))
            {
                table = new Dictionary<Guid, Entity>();
                _store[entity.LogicalName] = table;
            }
            if (table.ContainsKey(id))
                throw new InvalidOperationException("Duplicate record: " + entity.LogicalName + " " + id);
            var copy = Clone(entity);
            copy.Id = id;
            // Dataverse does not store null attributes.
            foreach (var key in copy.Attributes.Where(kv => kv.Value == null).Select(kv => kv.Key).ToList())
                copy.Attributes.Remove(key);
            table[id] = copy;
            return id;
        }

        public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet)
        {
            return Project(Find(entityName, id), columnSet);
        }

        public void Update(Entity entity)
        {
            if (entity == null) throw new ArgumentNullException("entity");
            var stored = Find(entity.LogicalName, entity.Id);
            foreach (var kv in entity.Attributes)
            {
                // Setting an attribute to null removes it (Dataverse does not return null attributes).
                if (kv.Value == null) stored.Attributes.Remove(kv.Key);
                else stored[kv.Key] = CloneValue(kv.Value);
            }
        }

        public void Delete(string entityName, Guid id)
        {
            Find(entityName, id);
            _store[entityName].Remove(id);
        }

        public EntityCollection RetrieveMultiple(QueryBase query)
        {
            var qe = query as QueryExpression;
            if (qe == null)
                throw new NotSupportedException("RetrieveMultiple query type not supported: " + (query == null ? "null" : query.GetType().Name));
            if (qe.LinkEntities.Count > 0) throw new NotSupportedException("Link entities are not supported.");
            if (qe.Orders.Count > 0) throw new NotSupportedException("Orders are not supported.");
            // Page 0/1 with any Count returns every match (MoreRecords stays false); other pages are unsupported.
            if (qe.PageInfo != null && qe.PageInfo.PageNumber > 1)
                throw new NotSupportedException("Paging beyond page 1 is not supported (PageNumber " + qe.PageInfo.PageNumber + ").");
            if (qe.TopCount.HasValue) throw new NotSupportedException("TopCount is not supported.");
            var criteria = qe.Criteria;
            if (criteria != null)
            {
                if (criteria.Filters.Count > 0) throw new NotSupportedException("Nested filters are not supported.");
                if (criteria.FilterOperator != LogicalOperator.And)
                    throw new NotSupportedException("Filter operator not supported: " + criteria.FilterOperator);
            }

            var result = new EntityCollection();
            result.EntityName = qe.EntityName;
            Dictionary<Guid, Entity> table;
            if (_store.TryGetValue(qe.EntityName, out table))
            {
                foreach (var e in table.Values)
                {
                    if (criteria == null || criteria.Conditions.All(c => Matches(e, c)))
                        result.Entities.Add(Project(e, qe.ColumnSet));
                }
            }
            return result;
        }

        public OrganizationResponse Execute(OrganizationRequest request)
        {
            throw new NotSupportedException("Request not supported: " + (request == null ? "null" : request.RequestName));
        }

        public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            throw new NotSupportedException("Associate is not supported.");
        }

        public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            throw new NotSupportedException("Disassociate is not supported.");
        }

        private Entity Find(string logicalName, Guid id)
        {
            Dictionary<Guid, Entity> table;
            Entity e;
            if (_store.TryGetValue(logicalName, out table) && table.TryGetValue(id, out e)) return e;
            throw new InvalidOperationException("Record not found: " + logicalName + " " + id);
        }

        private static bool Matches(Entity e, ConditionExpression c)
        {
            object actual;
            var has = TryGetValue(e, c.AttributeName, out actual);
            switch (c.Operator)
            {
                case ConditionOperator.Null: return !has;
                case ConditionOperator.NotNull: return has;
                case ConditionOperator.Equal: return c.Values.Count > 0 && has && ValueEquals(actual, c.Values[0]);
                // INFERRED (SQL semantics: a null attribute never satisfies NotEqual); to be confirmed in Spike C.
                case ConditionOperator.NotEqual: return has && c.Values.Count > 0 && !ValueEquals(actual, c.Values[0]);
                default: throw new NotSupportedException("Condition operator not supported: " + c.Operator);
            }
        }

        // A condition on "<logicalname>id" addresses the primary key, which lives in Entity.Id.
        private static bool TryGetValue(Entity e, string attribute, out object value)
        {
            if (string.Equals(attribute, e.LogicalName + "id", StringComparison.Ordinal))
            {
                value = e.Id;
                return e.Id != Guid.Empty;
            }
            return e.Attributes.TryGetValue(attribute, out value) && value != null;
        }

        private static bool ValueEquals(object a, object b)
        {
            var na = Normalize(a);
            var nb = Normalize(b);
            if (na == null || nb == null)
                throw new NotSupportedException("Null condition values are not supported (use the Null/NotNull operators).");
            if (na is decimal && nb is decimal) return (decimal)na == (decimal)nb;
            var sa = na as string;
            var sb = nb as string;
            // INFERRED from Dataverse's default case-insensitive collation; to be confirmed in Spike C.
            if (sa != null && sb != null) return string.Equals(sa, sb, StringComparison.OrdinalIgnoreCase);
            if (na.GetType() == nb.GetType()) return object.Equals(na, nb);
            throw new NotSupportedException("Cannot compare condition value of type " + b.GetType().Name
                + " with attribute value of type " + a.GetType().Name + ".");
        }

        // EntityReference -> Guid; OptionSetValue, Money and every numeric type -> decimal (compared by value).
        private static object Normalize(object value)
        {
            var er = value as EntityReference;
            if (er != null) return er.Id;
            var os = value as OptionSetValue;
            if (os != null) return (decimal)os.Value;
            var m = value as Money;
            if (m != null) return m.Value;
            if (value is byte || value is short || value is int || value is long || value is float || value is double)
                return Convert.ToDecimal(value);
            return value;
        }

        private static Entity Project(Entity source, ColumnSet columns)
        {
            var result = new Entity(source.LogicalName, source.Id);
            if (columns == null) return result;
            var idAttribute = source.LogicalName + "id";
            foreach (var kv in source.Attributes)
            {
                if (kv.Value == null) continue; // Dataverse does not return null attributes
                if (columns.AllColumns || columns.Columns.Contains(kv.Key))
                    result[kv.Key] = CloneValue(kv.Value);
            }
            if (columns.AllColumns || columns.Columns.Contains(idAttribute))
                result[idAttribute] = source.Id;
            return result;
        }

        private static Entity Clone(Entity source)
        {
            var copy = new Entity(source.LogicalName, source.Id);
            foreach (var kv in source.Attributes) copy[kv.Key] = CloneValue(kv.Value);
            return copy;
        }

        private static object CloneValue(object value)
        {
            var er = value as EntityReference;
            if (er != null) return new EntityReference(er.LogicalName, er.Id) { Name = er.Name };
            var os = value as OptionSetValue;
            if (os != null) return new OptionSetValue(os.Value);
            var m = value as Money;
            if (m != null) return new Money(m.Value);
            var nested = value as Entity;
            if (nested != null) return Clone(nested);
            return value;
        }
    }
}
