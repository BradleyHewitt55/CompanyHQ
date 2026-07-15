namespace Services.Products.Tests; public sealed class StarterTests { [Fact] public void ServiceLoads() => Assert.NotNull(typeof(ProductsModule)); }
