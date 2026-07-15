namespace Services.Quotes.Tests; public sealed class StarterTests { [Fact] public void ServiceLoads() => Assert.NotNull(typeof(QuotesModule)); }
