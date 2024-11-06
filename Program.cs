using System;
using System.Collections.Generic;
using System.Threading;

namespace SupplyDemand
{
    delegate dynamic Supplier(dynamic data, Scope scope);

    struct SuppliersMerge
    {
        public bool Clear { get; set; }
        public Dictionary<string, Supplier> Add { get; set; }
        public Dictionary<string, bool> Remove { get; set; }
    }

    struct DemandProps
    {
        public string Key { get; set; }
        public string Type { get; set; }
        public string Path { get; set; }
        public dynamic Data { get; set; }
        public Dictionary<string, Supplier> Suppliers { get; set; }
    }

    struct ScopedDemandProps
    {
        public string Key { get; set; }
        public string Type { get; set; }
        public SuppliersMerge SuppliersMerge { get; set; }
        public dynamic Data { get; set; }
    }

    struct Scope
    {
        public string Key { get; set; }
        public string Type { get; set; }
        public string Path { get; set; }
        public Func<ScopedDemandProps, dynamic> Demand { get; set; }
    }

    class Program
    {
        static Func<ScopedDemandProps, dynamic> CreateScopedDemand(DemandProps superProps)
        {
            return scopedProps =>
            {
                string demandKey = string.IsNullOrEmpty(scopedProps.Key) ? superProps.Key : scopedProps.Key;
                string path = $"{superProps.Path}/{demandKey}({scopedProps.Type})";

                var newSuppliers = MergeSuppliers(superProps.Suppliers, scopedProps.SuppliersMerge);

                return GlobalDemand(new DemandProps
                {
                    Key = demandKey,
                    Type = scopedProps.Type,
                    Path = path,
                    Data = scopedProps.Data,
                    Suppliers = newSuppliers
                });
            };
        }

        static Dictionary<string, Supplier> MergeSuppliers(Dictionary<string, Supplier> original, SuppliersMerge suppliersOp)
        {
            var merged = new Dictionary<string, Supplier>(original);

            if (suppliersOp.Clear)
            {
                merged.Clear();
            }

            if (suppliersOp.Add != null)
            {
                foreach (var kvp in suppliersOp.Add)
                {
                    merged[kvp.Key] = kvp.Value;
                }
            }

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

            return merged;
        }

        static dynamic GlobalDemand(DemandProps props)
        {
            Console.WriteLine($"Global demand function called with:\nKey: {props.Key}\nType: {props.Type}\nPath: {props.Path}");

            if (props.Suppliers != null && props.Suppliers.TryGetValue(props.Type, out Supplier supplier))
            {
                Console.WriteLine($"Calling supplier for type: {props.Type}");

                var scope = new Scope
                {
                    Key = props.Key,
                    Type = props.Type,
                    Path = props.Path,
                    Demand = CreateScopedDemand(props)
                };

                return supplier(props.Data, scope);
            }
            else
            {
                throw new Exception($"Supplier not found for type: {props.Type}");
            }
        }

        static dynamic SupplyDemand(Supplier rootSupplier, Dictionary<string, Supplier> suppliers)
        {
            suppliers["$$root"] = rootSupplier;

            var demand = new DemandProps
            {
                Key = "root",
                Type = "$$root",
                Path = "root",
                Suppliers = suppliers
            };

            return GlobalDemand(demand);
        }

        static Supplier thirdSupplier = (data, scope) =>
        {
            Console.WriteLine("Third supplier function called.");
            return scope.Demand(new ScopedDemandProps { Type = "first" });
        };

        static Supplier firstSupplier = (data, scope) =>
        {
            Console.WriteLine("First supplier function called.");
            Thread.Sleep(2000);
            return "First result";
        };

        static Supplier secondSupplier = (data, scope) =>
        {
            Console.WriteLine("Second supplier function called.");
            return 2;
        };

        static Supplier rootSupplier = (data, scope) =>
        {
            Console.WriteLine("Root supplier function called.");

            var mergeOps = new SuppliersMerge
            {
                Add = new Dictionary<string, Supplier> { { "third", thirdSupplier } },
                Remove = new Dictionary<string, bool>()
            };

            try
            {
                dynamic result = scope.Demand(new ScopedDemandProps { Type = "third", SuppliersMerge = mergeOps });
                Console.WriteLine($"Root supplier received result: {result}");
                return result;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception caught in rootSupplier: {e.Message}");
                return null;
            }
        };

        static void Main()
        {
            var suppliers = new Dictionary<string, Supplier>
            {
                { "first", firstSupplier },
                { "second", secondSupplier }
            };

            SupplyDemand(rootSupplier, suppliers);
        }
    }
}