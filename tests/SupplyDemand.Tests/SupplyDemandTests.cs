using System;
using System.Collections.Generic;
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
            Supplier rootSupplier = async (data, scope) => "RootResult";
            var suppliers = new Dictionary<string, Supplier>();
            var result = await SupplyDemand.Init(rootSupplier, suppliers);
            Assert.Equal("RootResult", result);
        }

        [Fact]
        public async Task SupplyDemand_CallsDependentSupplier()
        {
            Supplier childSupplier = async (data, scope) => 42;
            Supplier rootSupplier = async (data, scope) =>
                await scope.Demand(new ScopedDemandProps { Type = "child" });

            var suppliers = new Dictionary<string, Supplier>
            {
                { "child", childSupplier }
            };
            var result = await SupplyDemand.Init(rootSupplier, suppliers);
            Assert.Equal(42, result);
        }

        [Fact]
        public async Task SupplyDemand_NestedSuppliers_CompositionWorks()
        {
            Supplier first = async (data, scope) => 10;
            Supplier second = async (data, scope) => 32;
            Supplier sum = async (data, scope) =>
            {
                int a = await scope.Demand(new ScopedDemandProps { Type = "first" });
                int b = await scope.Demand(new ScopedDemandProps { Type = "second" });
                return a + b;
            };

            Supplier rootSupplier = async (data, scope) =>
                await scope.Demand(new ScopedDemandProps { Type = "sum" });

            var suppliers = new Dictionary<string, Supplier>
            {
                { "first", first },
                { "second", second },
                { "sum", sum }
            };
            var result = await SupplyDemand.Init(rootSupplier, suppliers);
            Assert.Equal(42, result);
        }

        [Fact]
        public async Task SupplyDemand_Throws_WhenSupplierMissing()
        {
            Supplier rootSupplier = async (data, scope) =>
                await scope.Demand(new ScopedDemandProps { Type = "missing" });
            var suppliers = new Dictionary<string, Supplier>();

            await Assert.ThrowsAsync<Exception>(async () =>
                await SupplyDemand.Init(rootSupplier, suppliers));
        }

        [Fact]
        public async Task SupplyDemand_Merging_AddsSupplier()
        {
            Supplier original = async (data, scope) => 1;
            Supplier adder = async (data, scope) => 2;

            Supplier rootSupplier = async (data, scope) =>
            {
                var merge = new SuppliersMerge
                {
                    Add = new Dictionary<string, Supplier>
                    {
                        { "adder", adder }
                    }
                };

                return await scope.Demand(new ScopedDemandProps
                {
                    Type = "adder",
                    SuppliersMerge = merge
                });
            };

            var suppliers = new Dictionary<string, Supplier>
            {
                { "original", original }
            };
            var result = await SupplyDemand.Init(rootSupplier, suppliers);
            Assert.Equal(2, result);
        }

        [Fact]
        public async Task SupplyDemand_Merging_RemovesSupplier()
        {
            Supplier willBeRemoved = async (data, scope) => -99;
            Supplier fallback = async (data, scope) => "fallback";

            Supplier rootSupplier = async (data, scope) =>
            {
                var merge = new SuppliersMerge
                {
                    // Remove 'toRemove' supplier
                    Remove = new Dictionary<string, bool>
                    {
                        { "toRemove", true }
                    },
                    // Add a fallback for 'toRemove'
                    Add = new Dictionary<string, Supplier>
                    {
                        { "toRemove", fallback }
                    }
                };

                return await scope.Demand(new ScopedDemandProps
                {
                    Type = "toRemove",
                    SuppliersMerge = merge
                });
            };

            var suppliers = new Dictionary<string, Supplier>
            {
                { "toRemove", willBeRemoved }
            };
            var result = await SupplyDemand.Init(rootSupplier, suppliers);
            Assert.Equal("fallback", result);
        }

        [Fact]
        public async Task SupplyDemand_Merging_ClearSuppliers()
        {
            Supplier dummy = async (data, scope) => "should be cleared";
            Supplier newOne = async (data, scope) => "after clear";

            Supplier rootSupplier = async (data, scope) =>
            {
                var merge = new SuppliersMerge
                {
                    Clear = true,
                    Add = new Dictionary<string, Supplier>
                    {
                        { "postClear", newOne }
                    }
                };

                return await scope.Demand(new ScopedDemandProps
                {
                    Type = "postClear",
                    SuppliersMerge = merge
                });
            };

            var suppliers = new Dictionary<string, Supplier>
            {
                { "dummy", dummy }
            };
            var result = await SupplyDemand.Init(rootSupplier, suppliers);
            Assert.Equal("after clear", result);
        }

        [Fact]
        public async Task SupplyDemand_AsyncSupplier_Works()
        {
            Supplier asyncSupplier = async (data, scope) =>
            {
                await Task.Delay(50);
                return 1234;
            };

            Supplier rootSupplier = async (data, scope) =>
                await scope.Demand(new ScopedDemandProps { Type = "async" });

            var suppliers = new Dictionary<string, Supplier>
            {
                { "async", asyncSupplier }
            };
            var result = await SupplyDemand.Init(rootSupplier, suppliers);
            Assert.Equal(1234, result);
        }

        [Fact]
        public async Task SupplyDemand_AsyncComposition_Works()
        {
            Supplier first = async (data, scope) => { await Task.Delay(10); return 10; };
            Supplier second = async (data, scope) => { await Task.Delay(10); return 32; };
            Supplier sum = async (data, scope) =>
            {
                int a = await scope.Demand(new ScopedDemandProps { Type = "first" });
                int b = await scope.Demand(new ScopedDemandProps { Type = "second" });
                return a + b;
            };

            Supplier rootSupplier = async (data, scope) =>
                await scope.Demand(new ScopedDemandProps { Type = "sum" });

            var suppliers = new Dictionary<string, Supplier>
            {
                { "first", first },
                { "second", second },
                { "sum", sum }
            };

            var result = await SupplyDemand.Init(rootSupplier, suppliers);
            Assert.Equal(42, result);
        }
    }
}