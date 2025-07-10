using System;
using System.Threading.Tasks;
using Xunit;
using SupplyDemand;

namespace SupplyDemand.Tests
{
    public class SupplyDemandTests
    {
        [Fact]
        public async Task SupplyDemand_ReturnsRootSupplierResult()
        {
            var suppliers = new SupplierRegistry();
            suppliers["$$root"] = new Supplier<object, string>((data, scope) => Task.FromResult("RootResult"));
            var rootSupplier = (Supplier<object, string>)suppliers["$$root"];

            var result = await SupplyDemand.Init(rootSupplier, suppliers);
            Assert.Equal("RootResult", result);
        }

        [Fact]
        public async Task SupplyDemand_CallsDependentSupplier()
        {
            var suppliers = new SupplierRegistry();
            suppliers["child"] = new Supplier<object, int>((data, scope) => Task.FromResult(42));
            suppliers["$$root"] = new Supplier<object, int>(async (data, scope) =>
                await scope.Demand<string, object, int>(new DemandProps<SupplierRegistry, string, object>
                {
                    Key = "child",
                    Type = "child",
                    Path = "root/child",
                    Data = null,
                    Suppliers = suppliers
                })
            );
            var rootSupplier = (Supplier<object, int>)suppliers["$$root"];

            var result = await SupplyDemand.Init(rootSupplier, suppliers);
            Assert.Equal(42, result);
        }

        [Fact]
        public async Task SupplyDemand_NestedSuppliers_CompositionWorks()
        {
            var suppliers = new SupplierRegistry();
            suppliers["first"] = new Supplier<object, int>((data, scope) => Task.FromResult(10));
            suppliers["second"] = new Supplier<object, int>((data, scope) => Task.FromResult(32));
            suppliers["sum"] = new Supplier<object, int>(async (data, scope) =>
            {
                int a = await scope.Demand<string, object, int>(new DemandProps<SupplierRegistry, string, object>
                {
                    Key = "first",
                    Type = "first",
                    Path = "sum/first",
                    Data = null,
                    Suppliers = suppliers
                });
                int b = await scope.Demand<string, object, int>(new DemandProps<SupplierRegistry, string, object>
                {
                    Key = "second",
                    Type = "second",
                    Path = "sum/second",
                    Data = null,
                    Suppliers = suppliers
                });
                return a + b;
            });
            suppliers["$$root"] = new Supplier<object, int>(async (data, scope) =>
                await scope.Demand<string, object, int>(new DemandProps<SupplierRegistry, string, object>
                {
                    Key = "sum",
                    Type = "sum",
                    Path = "root/sum",
                    Data = null,
                    Suppliers = suppliers
                })
            );
            var rootSupplier = (Supplier<object, int>)suppliers["$$root"];

            var result = await SupplyDemand.Init(rootSupplier, suppliers);
            Assert.Equal(42, result);
        }

        [Fact]
        public async Task SupplyDemand_Throws_WhenSupplierMissing()
        {
            var suppliers = new SupplierRegistry();
            suppliers["$$root"] = new Supplier<object, int>(async (data, scope) =>
                await scope.Demand<string, object, int>(new DemandProps<SupplierRegistry, string, object>
                {
                    Key = "missing",
                    Type = "missing",
                    Path = "root/missing",
                    Data = null,
                    Suppliers = suppliers
                })
            );
            var rootSupplier = (Supplier<object, int>)suppliers["$$root"];

            await Assert.ThrowsAsync<Exception>(async () =>
                await SupplyDemand.Init(rootSupplier, suppliers)
            );
        }

        [Fact]
        public async Task SupplyDemand_AsyncSupplier_Works()
        {
            var suppliers = new SupplierRegistry();
            suppliers["async"] = new Supplier<object, int>(async (data, scope) =>
            {
                await Task.Delay(50);
                return 1234;
            });
            suppliers["$$root"] = new Supplier<object, int>(async (data, scope) =>
                await scope.Demand<string, object, int>(new DemandProps<SupplierRegistry, string, object>
                {
                    Key = "async",
                    Type = "async",
                    Path = "root/async",
                    Data = null,
                    Suppliers = suppliers
                })
            );
            var rootSupplier = (Supplier<object, int>)suppliers["$$root"];

            var result = await SupplyDemand.Init(rootSupplier, suppliers);
            Assert.Equal(1234, result);
        }

        [Fact]
        public async Task SupplyDemand_AsyncComposition_Works()
        {
            var suppliers = new SupplierRegistry();
            suppliers["first"] = new Supplier<object, int>(async (data, scope) => { await Task.Delay(10); return 10; });
            suppliers["second"] = new Supplier<object, int>(async (data, scope) => { await Task.Delay(10); return 32; });
            suppliers["sum"] = new Supplier<object, int>(async (data, scope) =>
            {
                int a = await scope.Demand<string, object, int>(new DemandProps<SupplierRegistry, string, object>
                {
                    Key = "first",
                    Type = "first",
                    Path = "sum/first",
                    Data = null,
                    Suppliers = suppliers
                });
                int b = await scope.Demand<string, object, int>(new DemandProps<SupplierRegistry, string, object>
                {
                    Key = "second",
                    Type = "second",
                    Path = "sum/second",
                    Data = null,
                    Suppliers = suppliers
                });
                return a + b;
            });
            suppliers["$$root"] = new Supplier<object, int>(async (data, scope) =>
                await scope.Demand<string, object, int>(new DemandProps<SupplierRegistry, string, object>
                {
                    Key = "sum",
                    Type = "sum",
                    Path = "root/sum",
                    Data = null,
                    Suppliers = suppliers
                }));

            var rootSupplier = (Supplier<object, int>)suppliers["$$root"];

            var result = await SupplyDemand.Init(rootSupplier, suppliers);
            Assert.Equal(42, result);
        }

        // --------------------------------------------------------------------------------
        // README EXAMPLES
        // --------------------------------------------------------------------------------
        [Fact]
        public async Task Readme_QuickExample_Works()
        {
            // 1. Define suppliers
            var getNumber = new Supplier<object, int>((data, scope) => Task.FromResult(10));
            var getMessage = new Supplier<object, string>((data, scope) => Task.FromResult("Hello!"));

            // Supply registry used by all demands
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

            string result = await SupplyDemand.Init(rootSupplier, suppliers);
            Assert.Equal("Hello! Your number is 10.", result);
        }

        [Fact]
        public async Task Readme_AdvancedOverride_Merge_Works()
        {
            // Setup base registry
            var numberSupplier = new Supplier<object, int>((data, scope) => Task.FromResult(123));
            var suppliers = new SupplierRegistry { ["number"] = numberSupplier };

            var rootSupplier = new Supplier<object, int>(async (data, scope) =>
            {
                // Create an override registry for only this demand
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
            Assert.Equal(999, result);
        }

        [Fact]
        public async Task Readme_ComposingSuppliers_Works()
        {
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
            Assert.Equal(42, output);
        }
    }
}