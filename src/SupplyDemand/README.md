# SupplyDemand

**SupplyDemand** is a tiny yet powerful C# library for dynamic, scoped, asynchronous dependency composition.

- **Compose dependencies with async supplier functions**, in a strongly typed (generic) way.
- **Override, add, or remove "suppliers" per operation or call tree.**
- **Make nested, context-aware demands throughout your call graph.**
- **Great for:** rules engines, workflows, plugins, test injection, and flexible orchestration.

---

## Installation

Add SupplyDemand to your project with [NuGet](https://www.nuget.org/packages/SupplyDemand):

```bash
dotnet add package SupplyDemand
```

Or with the Package Manager:

```powershell
Install-Package SupplyDemand
```

---

## Quick Example

```csharp
using System;
using System.Threading.Tasks;
using SupplyDemand;

class Program
{
    static async Task Main()
    {
        // 1. Define suppliers (async, strongly typed)
        var getNumber = new Supplier<object, int>((data, scope) => Task.FromResult(10));
        var getMessage = new Supplier<object, string>((data, scope) => Task.FromResult("Hello!"));

        // The supplier registry must exist before sumSupplier can reference it in closure:
        SupplierRegistry suppliers = null;
        var sumSupplier = new Supplier<object, string>(async (data, scope) =>
        {
            int n = await scope.Demand<string, object, int>(new DemandProps<SupplierRegistry, string, object>
            {
                Type = "number",
                Key = "number",
                Path = "sum/number",
                Data = null,
                Suppliers = suppliers
            });
            string msg = await scope.Demand<string, object, string>(new DemandProps<SupplierRegistry, string, object>
            {
                Type = "message",
                Key = "message",
                Path = "sum/message",
                Data = null,
                Suppliers = suppliers
            });
            return $"{msg} Your number is {n}.";
        });

        suppliers = new SupplierRegistry
        {
            ["number"] = getNumber,
            ["message"] = getMessage,
            ["sum"] = sumSupplier
        };

        var rootSupplier = new Supplier<object, string>((data, scope) =>
            scope.Demand<string, object, string>(new DemandProps<SupplierRegistry, string, object>
            {
                Type = "sum",
                Key = "sum",
                Path = "root/sum",
                Data = null,
                Suppliers = suppliers
            })
        );

        // 4. Run!
        string result = await SupplyDemand.Init(rootSupplier, suppliers);
        Console.WriteLine(result); // Output: Hello! Your number is 10.
    }
}
```

---

## Advanced: Contextual Supplier Overrides (Merging)

Need to override/replace a supplier for just one demand?  
Just "clone" your registry, replace the supplier, and pass the new registry for that call!

```csharp
// Make sure your SupplierRegistry class contains a copy constructor:
// public SupplierRegistry(IDictionary<string, object> dict) : base(dict) { }

var numberSupplier = new Supplier<object, int>((data, scope) => Task.FromResult(123));
var suppliers = new SupplierRegistry { ["number"] = numberSupplier };

var rootSupplier = new Supplier<object, int>(async (data, scope) =>
{
    // Clone and replace just for this demand:
    var customSuppliers = new SupplierRegistry(suppliers)
    {
        ["number"] = new Supplier<object, int>((d, s) => Task.FromResult(999))
    };

    return await scope.Demand<string, object, int>(new DemandProps<SupplierRegistry, string, object>
    {
        Type = "number",
        Key = "number",
        Path = "root/number",
        Data = null,
        Suppliers = customSuppliers
    });
});

int result = await SupplyDemand.Init(rootSupplier, suppliers);
Console.WriteLine(result); // Output: 999
```

---

## Pattern: Composing Suppliers

Suppliers can call other suppliers dynamically and combine their results:

```csharp
var suppliers = new SupplierRegistry();
suppliers["double"] = new Supplier<int, int>((data, scope) => Task.FromResult(data * 2));
suppliers["greet"] = new Supplier<string, string>((data, scope) => Task.FromResult("Hello, " + data));
suppliers["sumDoubles"] = new Supplier<(int, int), int>(async (data, scope) =>
{
    int a = await scope.Demand<string, int, int>(new DemandProps<SupplierRegistry, string, int>
    {
        Type = "double",
        Key = "doubleA",
        Data = data.Item1,
        Suppliers = suppliers
    });
    int b = await scope.Demand<string, int, int>(new DemandProps<SupplierRegistry, string, int>
    {
        Type = "double",
        Key = "doubleB",
        Data = data.Item2,
        Suppliers = suppliers
    });
    return a + b;
});

var rootSupplier = new Supplier<(int, int), int>((data, scope) =>
    scope.Demand<string, (int, int), int>(new DemandProps<SupplierRegistry, string, (int, int)>
    {
        Type = "sumDoubles",
        Key = "sum",
        Data = data,
        Suppliers = suppliers
    }));

int output = await SupplyDemand.Init(rootSupplier, suppliers, (10, 11));
// output == 42
```

---

## How It Works

- **Suppliers** are async functions, registered by name/type.
- A **demand** is a context-aware call: specify the supplier name (type/key), provide data, and optionally scope/override the registry.
- Suppliers receive a **scope** object, to make further, nested and contextually controlled `Demand` calls (with their own data and/or supplier registry).
- The API is fully async and uses generics for *strong type safety* from registry through demand to result.

---

## API Reference

### Supplier

```csharp
public class Supplier<TData, TReturn> : ISupplier<TData, SupplierRegistry, TReturn>
{
    public Supplier(Func<TData, Scope<SupplierRegistry>, Task<TReturn>> func);
    public Task<TReturn> Invoke(TData data, Scope<SupplierRegistry> scope);
}
```

### SupplierRegistry

- `SupplierRegistry` is a `Dictionary<string, object>` that holds your named suppliers.
- For merging/overriding:  
  ```csharp
  public SupplierRegistry(IDictionary<string, object> dict) : base(dict) { }
  ```

### Scope

- Represents the current demand context.
- `Demand<TType, TData, TReturn>(DemandProps<SupplierRegistry, TType, TData> props)`:  
  Strongly-typed demand for another supplier.

### DemandProps

- For structured, type-safe demand requests.

---

## License

MIT