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
            foreach (var kv in entity.Attributes) stored[kv.Key] = CloneValue(kv.Value);
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
            if (qe.PageInfo != null && (qe.PageInfo.Count > 0 || qe.PageInfo.PageNumber > 1))
                throw new NotSupportedException("Paging is not supported.");
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
            var has = e.Attributes.TryGetValue(c.AttributeName, out actual) && actual != null;
            switch (c.Operator)
            {
                case ConditionOperator.Null: return !has;
                case ConditionOperator.NotNull: return has;
                case ConditionOperator.Equal: return c.Values.Count > 0 && has && ValueEquals(actual, c.Values[0]);
                case ConditionOperator.NotEqual: return has && c.Values.Count > 0 && !ValueEquals(actual, c.Values[0]); // SQL semantics: null never matches
                default: throw new NotSupportedException("Condition operator not supported: " + c.Operator);
            }
        }

        private static bool ValueEquals(object a, object b)
        {
            var ra = a as EntityReference;
            var rb = b as EntityReference;
            if (ra != null && rb != null) return ra.Id == rb.Id && ra.LogicalName == rb.LogicalName;
            var oa = a as OptionSetValue;
            var ob = b as OptionSetValue;
            if (oa != null && ob != null) return oa.Value == ob.Value;
            var ma = a as Money;
            var mb = b as Money;
            if (ma != null && mb != null) return ma.Value == mb.Value;
            return object.Equals(a, b);
        }

        private static Entity Project(Entity source, ColumnSet columns)
        {
            var result = new Entity(source.LogicalName, source.Id);
            if (columns == null) return result;
            foreach (var kv in source.Attributes)
            {
                if (columns.AllColumns || columns.Columns.Contains(kv.Key))
                    result[kv.Key] = CloneValue(kv.Value);
            }
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
