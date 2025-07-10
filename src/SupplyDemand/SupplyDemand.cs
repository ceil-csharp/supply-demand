using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SupplyDemand
{
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
        public string Path { get; set; }
        public TData Data { get; set; }
        public TSuppliers Suppliers { get; set; }
    }

    // Scope passes supplier registry for nested demand resolution.
    public class Scope<TSuppliers>
    {
        public string Key { get; set; }
        public string Type { get; set; }
        public string Path { get; set; }
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

            // Prepare scope for next demand.
            var nextScope = new Scope<TSuppliers>
            {
                Key = props.Key,
                Type = props.Type.ToString(),
                Path = props.Path,
                Suppliers = props.Suppliers // Also propagate the correct registry!
            };

            return await supplier.Invoke(props.Data, nextScope);
        }
    }

    // Static class for bootstrapping.
    public static class SupplyDemand
    {
        // Only exposes a root entry for now, extend as required.
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
                Path = "root",
                Suppliers = suppliers
            };
            return await rootSupplier.Invoke(rootData, rootScope);
        }
    }

    // Helper: You may want to wrap suppliers into a generic dictionary.
    public class SupplierRegistry : Dictionary<string, object>
    {
        public SupplierRegistry() : base() { }
        public SupplierRegistry(IDictionary<string, object> dict) : base(dict) { }
    }

    // Typed supplier for convenience (replace with DI/registration as needed)
    public class Supplier<TData, TReturn> : ISupplier<TData, SupplierRegistry, TReturn>
    {
        private readonly Func<TData, Scope<SupplierRegistry>, Task<TReturn>> _func;
        public Supplier(Func<TData, Scope<SupplierRegistry>, Task<TReturn>> func) => _func = func;
        public Task<TReturn> Invoke(TData data, Scope<SupplierRegistry> scope) => _func(data, scope);
    }
}