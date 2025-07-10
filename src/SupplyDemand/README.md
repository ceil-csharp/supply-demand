# SupplyDemand

**SupplyDemand** is a tiny C# library for dynamic, scoped, asynchronous dependency composition.

- **Compose dependencies with async supplier functions**
- **Override, add, or remove "suppliers" for a single operation**
- **Make nested, context-aware demands in a call graph**
- **Perfect for: rules engines, workflows, plugins, flexible test injection**

---

## Installation

Add SupplyDemand to your project with [NuGet](https://www.nuget.org/):

```bash
dotnet add package SupplyDemand
```

or with the Package Manager:

```powershell
Install-Package SupplyDemand
```

---

## Quick Example

```csharp
using SupplyDemand;

// 1. Define some suppliers (async functions)
Supplier getNumber = async (data, scope) => 10;
Supplier getMessage = async (data, scope) => "Hello!";
Supplier sumSupplier = async (data, scope) =>
{
    int n = await scope.Demand(new ScopedDemandProps { Type = "number" });
    string msg = await scope.Demand(new ScopedDemandProps { Type = "message" });
    return $"{msg} Your number is {n}.";
};

// 2. Register suppliers
var suppliers = new Dictionary<string, Supplier>
{
    { "number", getNumber },
    { "message", getMessage },
    { "sum", sumSupplier }
};

// 3. Define root supplier (entry-point)
Supplier rootSupplier = async (data, scope) =>
    await scope.Demand(new ScopedDemandProps { Type = "sum" });

// 4. Run!
var result = await SupplyDemand.SupplyDemand.Init(rootSupplier, suppliers);
Console.WriteLine(result); // Output: Hello! Your number is 10.
```

---

## Advanced: Merging Suppliers on Demand

You can scope-add, remove, or clear/replace suppliers for a specific demand:

```csharp
// Example: Remove a supplier for the next nested demand only
Supplier root = async (data, scope) =>
{
    var merge = new SuppliersMerge
    {
        Remove = new Dictionary<string, bool> { { "number", true } },
        Add = new Dictionary<string, Supplier> { { "number", async (d, s) => 999 } }
    };
    return await scope.Demand(new ScopedDemandProps
    {
        Type = "number",
        SuppliersMerge = merge
    });
};
```

---

## License

MIT