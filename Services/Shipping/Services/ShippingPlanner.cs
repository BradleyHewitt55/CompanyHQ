namespace CompanyHQ.Services.Shipping.Services;

/// <summary>
/// Picks one way to ship every product so the basket costs least. A leg is one method leaving one
/// origin and is paid for once however many products share it, so the cheapest plan is a choice
/// across the whole basket, never product by product.
/// </summary>
internal static class ShippingPlanner
{
    /// <summary>Search steps allowed before failing explicitly; approximate plans are never returned.</summary>
    internal const int DefaultBudget = 200_000;

    internal readonly record struct Leg(Guid MethodId, Guid OriginId);

    internal sealed record Candidate(Leg Leg, decimal Cost);

    /// <summary>
    /// One chosen candidate per entry of <paramref name="products"/>, in the same order. Every entry
    /// must have at least one candidate. Each entry's candidates are tried in the order given, so
    /// the caller's ordering is the tie-break between plans of equal cost.
    /// </summary>
    public static IReadOnlyList<Candidate> Cheapest(
        IReadOnlyList<IReadOnlyList<Candidate>> products,
        int budget = DefaultBudget)
    {
        // Products with the same choices can always share one: moving either to the other's leg
        // never adds a leg. Searching distinct choice sets keeps a large basket of similar products small.
        var groups = new List<IReadOnlyList<Candidate>>();
        var groupOf = new int[products.Count];
        var signatures = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < products.Count; index++)
        {
            var signature = string.Join('|', products[index]
                .Select(candidate => $"{candidate.Leg.MethodId:N}{candidate.Leg.OriginId:N}")
                .Order(StringComparer.Ordinal));
            if (!signatures.TryGetValue(signature, out var group))
            {
                group = groups.Count;
                signatures[signature] = group;
                groups.Add(products[index]);
            }

            groupOf[index] = group;
        }

        // Most constrained first, so a forced leg is in the plan before the open choices are tried.
        var order = Enumerable.Range(0, groups.Count).OrderBy(group => groups[group].Count).ToArray();
        var search = new Search(groups, order, budget);
        search.Visit(0, 0m);
        if (search.Exhausted) throw new ShippingComplexityException();

        return groupOf.Select(group => search.Best[group]).ToArray();
    }

    private sealed class Search(IReadOnlyList<IReadOnlyList<Candidate>> groups, int[] order, int budget)
    {
        private readonly HashSet<Leg> chosenLegs = [];
        private readonly Candidate[] current = new Candidate[groups.Count];
        private decimal bestCost = decimal.MaxValue;

        public Candidate[] Best { get; } = new Candidate[groups.Count];
        public bool Exhausted { get; private set; }

        public void Visit(int depth, decimal cost)
        {
            if (cost >= bestCost)
            {
                return;
            }

            if (depth == order.Length)
            {
                bestCost = cost;
                current.CopyTo(Best, 0);
                return;
            }

            var group = order[depth];

            // A leg already paid for costs nothing more, and no other choice can beat free.
            if (groups[group].FirstOrDefault(candidate => chosenLegs.Contains(candidate.Leg)) is { } shared)
            {
                current[group] = shared;
                Visit(depth + 1, cost);
                return;
            }

            foreach (var candidate in groups[group])
            {
                if (budget-- <= 0)
                {
                    Exhausted = true;
                    return;
                }

                current[group] = candidate;
                chosenLegs.Add(candidate.Leg);
                Visit(depth + 1, cost + candidate.Cost);
                chosenLegs.Remove(candidate.Leg);
                if (Exhausted) return;
            }
        }
    }
}

internal sealed class ShippingComplexityException : Exception;
