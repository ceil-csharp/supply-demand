using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace SupplyDemand
{
    // For typed path segment
    public class PathSegment
    {
        public string Key { get; set; }
        public string Type { get; set; }
    }

    // Main supplier interface to ensure type safety.
    public interface ISupplier<TData, TSuppliers, TReturn>
    {
        Task<TReturn> Invoke(TData data, Scope<TSuppliers> scope);
    }

    // Demand properties for strong typing.
    public class DemandProps<TSuppliers, TType, TData>
    {
        public string Key { get; set; }
        public TType Type { get; set; }
        public List<PathSegment> Path { get; set; }
        public TData Data { get; set; }
        public TSuppliers Suppliers { get; set; }
    }

    // Scope passes supplier registry for nested demand resolution.
    public class Scope<TSuppliers>
    {
        public string Key { get; set; }
        public string Type { get; set; }
        // Path will be array of { key, type }
        public List<PathSegment> Path { get; set; }
        public TSuppliers Suppliers { get; set; }

        // Type-safe demand (demand another supplier of correct sig).
        public async Task<TReturn> Demand<TNextType, TNextData, TReturn>(
            DemandProps<TSuppliers, TNextType, TNextData> props
        )
        {
            var suppliersDict = props.Suppliers as IDictionary<string, object>;
            if (suppliersDict == null)
                throw new Exception("Suppliers registry does not implement IDictionary<string, object>");

            if (!suppliersDict.TryGetValue(props.Type.ToString(), out var supplierObj))
                throw new Exception($"Supplier not found for type '{props.Type}'");

            if (!(supplierObj is ISupplier<TNextData, TSuppliers, TReturn> supplier))
                throw new Exception($"Supplier '{props.Type}' has the wrong signature for demand.");

            var newPath = (this.Path ?? new List<PathSegment>()).ToList();
            newPath.Add(new PathSegment { Key = props.Key, Type = props.Type.ToString() });

            var nextScope = new Scope<TSuppliers>
            {
                Key = props.Key,
                Type = props.Type.ToString(),
                Path = newPath,
                Suppliers = props.Suppliers
            };

            return await supplier.Invoke(props.Data, nextScope);
        }
    }

    public static class SupplyDemand
    {
        public static async Task<TReturn> Init<TSuppliers, TData, TReturn>(
            ISupplier<TData, TSuppliers, TReturn> rootSupplier,
            TSuppliers suppliers,
            TData rootData = default
        )
        {
            var rootScope = new Scope<TSuppliers>
            {
                Key = "root",
                Type = "$$root",
                Path = new List<PathSegment>(), // <-- Start with empty array
                Suppliers = suppliers
            };
            return await rootSupplier.Invoke(rootData, rootScope);
        }
    }

    public class SupplierRegistry : Dictionary<string, object>
    {
        public SupplierRegistry() : base() { }
        public SupplierRegistry(IDictionary<string, object> dict) : base(dict) { }
    }

    public class Supplier<TData, TReturn> : ISupplier<TData, SupplierRegistry, TReturn>
    {
        private readonly Func<TData, Scope<SupplierRegistry>, Task<TReturn>> _func;
        public Supplier(Func<TData, Scope<SupplierRegistry>, Task<TReturn>> func) => _func = func;
        public Task<TReturn> Invoke(TData data, Scope<SupplierRegistry> scope) => _func(data, scope);
    }
}