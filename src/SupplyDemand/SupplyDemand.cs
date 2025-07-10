using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SupplyDemand
{
    // Delegate for supplier functions
    public delegate Task<dynamic> Supplier(dynamic data, Scope scope);

    // Struct representing supplier operations (merge/add/remove)
    public struct SuppliersMerge
    {
        public bool Clear { get; set; }
        public Dictionary<string, Supplier> Add { get; set; }
        public Dictionary<string, bool> Remove { get; set; }
    }

    // Struct for demand properties
    public struct DemandProps
    {
        public string Key { get; set; }
        public string Type { get; set; }
        public string Path { get; set; }
        public dynamic Data { get; set; }
        public Dictionary<string, Supplier> Suppliers { get; set; }
    }

    // Struct for scoped demand properties
    public struct ScopedDemandProps
    {
        public string Key { get; set; }
        public string Type { get; set; }
        public SuppliersMerge SuppliersMerge { get; set; }
        public dynamic Data { get; set; }
    }

    // Struct for scope
    public struct Scope
    {
        public string Key { get; set; }
        public string Type { get; set; }
        public string Path { get; set; }
        public Func<ScopedDemandProps, Task<dynamic>> Demand { get; set; }
    }

    // Static class for core SupplyDemand logic
    public static class SupplyDemand
    {
        internal static Func<ScopedDemandProps, Task<dynamic>> CreateScopedDemand(DemandProps superProps)
        {
            return async scopedProps =>
            {
                string demandKey = string.IsNullOrEmpty(scopedProps.Key) ? superProps.Key : scopedProps.Key;
                string path = $"{superProps.Path}/{demandKey}({scopedProps.Type})";

                var newSuppliers = MergeSuppliers(superProps.Suppliers, scopedProps.SuppliersMerge);

                return await GlobalDemand(new DemandProps
                {
                    Key = demandKey,
                    Type = scopedProps.Type,
                    Path = path,
                    Data = scopedProps.Data,
                    Suppliers = newSuppliers
                });
            };
        }

        internal static Dictionary<string, Supplier> MergeSuppliers(
            Dictionary<string, Supplier> original,
            SuppliersMerge suppliersOp)
        {
            var merged = new Dictionary<string, Supplier>(original ?? new Dictionary<string, Supplier>());

            if (suppliersOp.Clear)
            {
                merged.Clear();
            }

            // REMOVE FIRST!
            if (suppliersOp.Remove != null)
            {
                foreach (var kvp in suppliersOp.Remove)
                {
                    if (kvp.Value && merged.ContainsKey(kvp.Key))
                    {
                        merged.Remove(kvp.Key);
                    }
                }
            }

            // THEN ADD/REPLACE
            if (suppliersOp.Add != null)
            {
                foreach (var kvp in suppliersOp.Add)
                {
                    merged[kvp.Key] = kvp.Value;
                }
            }

            return merged;
        }

        internal static async Task<dynamic> GlobalDemand(DemandProps props)
        {
            if (props.Suppliers != null && props.Suppliers.TryGetValue(props.Type, out Supplier supplier))
            {
                var scope = new Scope
                {
                    Key = props.Key,
                    Type = props.Type,
                    Path = props.Path,
                    Demand = CreateScopedDemand(props)
                };

                return await supplier(props.Data, scope);
            }
            else
            {
                throw new Exception($"Supplier not found for type: {props.Type}");
            }
        }

        public static async Task<dynamic> Init(Supplier rootSupplier, Dictionary<string, Supplier> suppliers)
        {
            suppliers["$$root"] = rootSupplier;

            var demand = new DemandProps
            {
                Key = "root",
                Type = "$$root",
                Path = "root",
                Suppliers = suppliers
            };

            return await GlobalDemand(demand);
        }
    }
}