using CompanyHQ.Persistence.Services;
using CompanyHQ.Postgres.Contracts;
using CompanyHQ.Postgres.Contracts.Dtos;
using CompanyHQ.Postgres.Contracts.Enums;
using CompanyHQ.Postgres.Contracts.Forms;
using CompanyHQ.Shipping.Contracts.Dtos;
using CompanyHQ.Shipping.Contracts.Enums;
using CompanyHQ.Shipping.Contracts.Forms;
using CompanyHQ.Shipping.Contracts.Models;
using CompanyHQ.Shipping.Contracts.Ports;
using CompanyHQ.Users.Services;

namespace CompanyHQ.Shipping.Services;

/// <inheritdoc cref="IShippingService" />
public sealed class ShippingService(
    IPersistence persistence,
    IUserAuthorizationService authorization,
    IShippingCatalog? catalog = null,
    IShippingGeocoder? geocoder = null,
    IShippingDiscountPolicy? discounts = null) : IShippingService
{
    public async Task<PagedResult<ClientShippingMethodDto>> ListMethodsAsync(
        PageRequest page,
        SearchRequest? search = null,
        ResponseAudience audience = ResponseAudience.Client,
        Guid? actingUserId = null,
        CancellationToken cancellationToken = default)
    {
        var manager = audience is ResponseAudience.Admin && await IsManagerAsync(actingUserId, cancellationToken);
        var query = persistence.Query<ShippingMethod>();
        if (!manager)
        {
            query = query.Where(method => method.IsActive);
        }

        var result = await query
            .Search(search?.Field, search?.Keyword, manager ? typeof(AdminShippingMethodDto) : typeof(ClientShippingMethodDto))
            .OrderBy(method => method.Cost)
            .ThenBy(method => method.Name)
            .ThenBy(method => method.Id)
            .ToPageAsync(page.Skip, page.PageSize, cancellationToken);

        IReadOnlyList<ClientShippingMethodDto> items = manager
            ? await ToAdminAsync(result.Items, cancellationToken)
            : result.Items.Select(ToClient).ToArray();

        return new PagedResult<ClientShippingMethodDto>(items, page.Page, page.PageSize, result.Total);
    }

    public async Task<ClientShippingMethodDto?> GetMethodAsync(
        Guid methodId,
        ResponseAudience audience = ResponseAudience.Client,
        Guid? actingUserId = null,
        CancellationToken cancellationToken = default)
    {
        var manager = audience is ResponseAudience.Admin && await IsManagerAsync(actingUserId, cancellationToken);
        var method = await persistence.Query<ShippingMethod>()
            .FirstOrDefaultAsync(candidate => candidate.Id == methodId && (manager || candidate.IsActive), cancellationToken);
        if (method is null)
        {
            return null;
        }

        return manager ? (await ToAdminAsync([method], cancellationToken))[0] : ToClient(method);
    }

    public async Task<AdminShippingMethodDto> CreateMethodAsync(
        SaveShippingMethodRequest request,
        Guid? actingUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureManagerAsync(actingUserId, cancellationToken);
        await ValidateAsync(request, cancellationToken);

        var method = new ShippingMethod { Id = Guid.CreateVersion7(), Name = string.Empty };
        Apply(method, request);
        persistence.Add(method);
        StageChildren(method.Id, request);
        await persistence.SaveChangesAsync(cancellationToken);

        return (await ToAdminAsync([method], cancellationToken))[0];
    }

    public async Task<AdminShippingMethodDto?> UpdateMethodAsync(
        Guid methodId,
        SaveShippingMethodRequest request,
        Guid? actingUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureManagerAsync(actingUserId, cancellationToken);
        await ValidateAsync(request, cancellationToken);

        // Zones and links are replaced as a whole, so the old set must go in the same commit as the new one.
        var updated = await persistence.InTransactionAsync(async token =>
        {
            var method = await persistence.Tracked<ShippingMethod>().FirstOrDefaultAsync(candidate => candidate.Id == methodId, token);
            if (method is null)
            {
                return null;
            }

            Apply(method, request);
            await persistence.Query<DeliveryZone>().Where(zone => zone.ShippingMethodId == methodId).DeleteAsync(token);
            await persistence.Query<ShippingMethodFulfillmentOrigin>().Where(link => link.ShippingMethodId == methodId).DeleteAsync(token);
            StageChildren(methodId, request);
            await persistence.SaveChangesAsync(token);

            return method;
        }, cancellationToken: cancellationToken);

        return updated is null ? null : (await ToAdminAsync([updated], cancellationToken))[0];
    }

    public async Task<bool> DeleteMethodAsync(Guid methodId, Guid? actingUserId, CancellationToken cancellationToken = default)
    {
        await EnsureManagerAsync(actingUserId, cancellationToken);

        return await persistence.Query<ShippingMethod>().Where(method => method.Id == methodId).DeleteAsync(cancellationToken) > 0;
    }

    public async Task<PagedResult<AdminFulfillmentOriginDto>> ListOriginsAsync(
        PageRequest page,
        SearchRequest? search,
        Guid? actingUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureManagerAsync(actingUserId, cancellationToken);

        var result = await persistence.Query<FulfillmentOrigin>()
            .Search(search?.Field, search?.Keyword, typeof(AdminFulfillmentOriginDto))
            .OrderBy(origin => origin.Name)
            .ThenBy(origin => origin.Id)
            .ToPageAsync(page.Skip, page.PageSize, cancellationToken);

        return new PagedResult<AdminFulfillmentOriginDto>(result.Items.Select(ToDto).ToArray(), page.Page, page.PageSize, result.Total);
    }

    public async Task<AdminFulfillmentOriginDto?> GetOriginAsync(Guid originId, Guid? actingUserId, CancellationToken cancellationToken = default)
    {
        await EnsureManagerAsync(actingUserId, cancellationToken);

        return await persistence.Query<FulfillmentOrigin>().FirstOrDefaultAsync(origin => origin.Id == originId, cancellationToken) is { } origin
            ? ToDto(origin)
            : null;
    }

    public async Task<AdminFulfillmentOriginDto> CreateOriginAsync(
        SaveFulfillmentOriginRequest request,
        Guid? actingUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureManagerAsync(actingUserId, cancellationToken);
        Validate(request);

        var origin = new FulfillmentOrigin { Name = string.Empty };
        await ApplyAsync(origin, request, cancellationToken);
        persistence.Add(origin);
        await persistence.SaveChangesAsync(cancellationToken);

        return ToDto(origin);
    }

    public async Task<AdminFulfillmentOriginDto?> UpdateOriginAsync(
        Guid originId,
        SaveFulfillmentOriginRequest request,
        Guid? actingUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureManagerAsync(actingUserId, cancellationToken);
        Validate(request);

        var origin = await persistence.Tracked<FulfillmentOrigin>().FirstOrDefaultAsync(candidate => candidate.Id == originId, cancellationToken);
        if (origin is null)
        {
            return null;
        }

        await ApplyAsync(origin, request, cancellationToken);
        await persistence.SaveChangesAsync(cancellationToken);

        return ToDto(origin);
    }

    public async Task<bool> DeleteOriginAsync(Guid originId, Guid? actingUserId, CancellationToken cancellationToken = default)
    {
        await EnsureManagerAsync(actingUserId, cancellationToken);

        return await persistence.Query<FulfillmentOrigin>().Where(origin => origin.Id == originId).DeleteAsync(cancellationToken) > 0;
    }

    public async Task<ShippingQuoteDto> QuoteAsync(ShippingQuoteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ShippingValidation.ThrowIfInvalid(request);
        ShippingValidation.ThrowIfInvalid(request.Address);

        if (request.Address.Latitude.HasValue != request.Address.Longitude.HasValue)
            throw new ArgumentException("Latitude and longitude must be given together.");
        foreach (var item in request.Items)
        {
            ShippingValidation.ThrowIfInvalid(item);
            if(item.ProductId == Guid.Empty) throw new ArgumentException("A product id is required.");
            if((item.Options?.Count ?? 0)>20 || (item.Options ?? new Dictionary<string,string>()).Any(x=>
                string.IsNullOrWhiteSpace(x.Key) || x.Key.Length>100 || string.IsNullOrWhiteSpace(x.Value) || x.Value.Length>255))
                throw new ArgumentException("Invalid basket option selections.");
            if(item.UpsellParentLineId.HasValue && (item.Id is null || item.Id==Guid.Empty || item.UpsellParentLineId==Guid.Empty))
                throw new ArgumentException("Upsell context requires stable line and parent ids.");
        }
        var lineIds=request.Items.Where(x=>x.Id.HasValue && x.Id!=Guid.Empty).Select(x=>x.Id.GetValueOrDefault()).ToArray();
        if(lineIds.Distinct().Count()!=lineIds.Length)throw new ArgumentException("Basket line ids must be unique.");
        if((request.CouponCodes ?? []).Any(code=>string.IsNullOrWhiteSpace(code) || code.Length>100))
            throw new ArgumentException("Invalid coupon code.");
        var destination = await LocateAsync(request.Address, cancellationToken);
        var postalCode = request.Address.PostalCode?.Trim() ?? string.Empty;
        var productIds = request.Items.Select(item => item.ProductId).Distinct().ToArray();
        var known = catalog is null
            ? null
            : (await catalog.GetProductsAsync(productIds, cancellationToken)).ToDictionary(product => product.ProductId);

        // Cheapest first, then by name: the planner tries candidates in this order, which makes it
        // the tie-break between plans that cost the same.
        var methods = await persistence.Query<ShippingMethod>()
            .Where(method => method.IsActive)
            .OrderBy(method => method.Cost)
            .ThenBy(method => method.Name)
            .ThenBy(method => method.Id)
            .Take(201)
            .ToListAsync(cancellationToken);
        if(methods.Count>200) return Unavailable("Too many active shipping methods.",
            [new(ShippingQuoteFailureCodes.ComplexityLimit,"At most 200 active methods are supported.",null,null)],destination);
        var routes = await LoadRoutesAsync(methods, postalCode, cancellationToken);

        var failures = new List<ShippingQuoteFailureDto>();
        var candidates = new List<IReadOnlyList<ShippingPlanner.Candidate>>();
        var legs = new Dictionary<ShippingPlanner.Leg, LegDetail>();
        var unshippable = false;

        foreach (var productId in productIds)
        {
            ShippingCatalogProduct? product = null;
            if (known is not null && !known.TryGetValue(productId, out product))
            {
                unshippable = true;
                failures.Add(new(ShippingQuoteFailureCodes.ProductNotFound, $"Product {productId} was not found.", productId, null));
                continue;
            }

            var name = product?.Name ?? productId.ToString();
            var reasons = new List<ShippingQuoteFailureDto>();
            var options = new List<ShippingPlanner.Candidate>();

            foreach (var route in routes)
            {
                var method = route.Method;
                if (method.ProductIds.Length > 0 && !method.ProductIds.Contains(productId))
                {
                    continue;
                }

                var origins = (product?.Origins ?? [])
                    .Select(origin => new Origin(origin.OriginId, origin.Name, origin.Latitude, origin.Longitude))
                    .Concat(route.Origins)
                    .ToArray();
                if (origins.Length == 0)
                {
                    reasons.Add(new(ShippingQuoteFailureCodes.ProductWithoutOrigin,
                        $"\"{name}\" has no origin to ship from with \"{method.Name}\".", productId, method.Id));
                    continue;
                }

                if (!route.CoversPostalCode)
                {
                    reasons.Add(new(ShippingQuoteFailureCodes.OutOfZone,
                        $"\"{method.Name}\" does not deliver to postal code \"{postalCode}\".", productId, method.Id));
                    continue;
                }

                foreach (var origin in origins)
                {
                    // Ports are untrusted boundaries too. Missing pairs are allowed only for
                    // unrestricted methods; malformed supplied geography is never a candidate.
                    if (!ValidCoordinatePair(origin.Latitude, origin.Longitude))
                    {
                        reasons.Add(new(ShippingQuoteFailureCodes.OriginUnresolved,
                            $"Origin \"{origin.Name}\" returned invalid coordinates.", productId, method.Id));
                        continue;
                    }
                    var distance = destination is not null && origin.Latitude is { } latitude && origin.Longitude is { } longitude
                        ? ShippingRules.DistanceKm(latitude, longitude, destination.Latitude, destination.Longitude)
                        : (double?)null;

                    // A distance limit that cannot be measured is a refusal, never a pass: letting it
                    // through would promise a delivery nobody checked.
                    if (method.MaxDistanceKm is { } limit)
                    {
                        if (destination is null)
                        {
                            reasons.Add(new(ShippingQuoteFailureCodes.DestinationUnresolved,
                                $"\"{method.Name}\" is limited by distance and the delivery address has no coordinates.", productId, method.Id));
                            break;
                        }

                        if (distance is null)
                        {
                            reasons.Add(new(ShippingQuoteFailureCodes.OriginUnresolved,
                                $"\"{method.Name}\" is limited by distance and origin \"{origin.Name}\" has no coordinates.", productId, method.Id));
                            continue;
                        }

                        if (distance > limit)
                        {
                            reasons.Add(new(ShippingQuoteFailureCodes.OutOfRange,
                                $"The delivery address is beyond the {limit} km \"{method.Name}\" covers from \"{origin.Name}\".", productId, method.Id));
                            continue;
                        }
                    }

                    var leg = new ShippingPlanner.Leg(method.Id, origin.Id);
                    legs.TryAdd(leg, new LegDetail(method, origin, distance));
                    options.Add(new ShippingPlanner.Candidate(leg, method.Cost));
                    if(legs.Count>2000 || options.Count+candidates.Sum(group=>group.Count)>10000)
                        return Unavailable("Shipping plan exceeds supported complexity.",
                            [new(ShippingQuoteFailureCodes.ComplexityLimit,"Narrow the basket or shipping choices.",null,null)],destination);
                }
            }

            // Why one method could not serve a product only matters when none could.
            if (options.Count == 0)
            {
                unshippable = true;
                failures.AddRange(reasons);
                failures.Add(new(ShippingQuoteFailureCodes.NoShippingMethod, $"No shipping method can deliver \"{name}\" to this address.", productId, null));
                continue;
            }

            candidates.Add(options);
        }

        if (unshippable)
        {
            var unresolved = destination is null && failures.Any(failure => failure.Code == ShippingQuoteFailureCodes.DestinationUnresolved);
            return Unavailable(
                unresolved
                    ? "The delivery address could not be resolved to geographic coordinates."
                    : "No shipping method is available for one or more products.",
                failures,
                destination);
        }

        if (legs.Count > 2_000 || candidates.Sum(group => group.Count) > 10_000)
            return Unavailable("Shipping plan exceeds supported complexity.",
                [new(ShippingQuoteFailureCodes.ComplexityLimit, "Narrow the basket or shipping choices.", null, null)], destination);
        IReadOnlyList<ShippingPlanner.Candidate> plan;
        try { plan = ShippingPlanner.Cheapest(candidates); }
        catch (ShippingComplexityException)
        {
            return Unavailable("Shipping optimality could not be proven within the search limit.",
                [new(ShippingQuoteFailureCodes.ComplexityLimit, "Narrow the basket or shipping choices.", null, null)], destination);
        }
        var routeLegs = plan
            .Select((candidate, index) => (candidate.Leg, ProductId: productIds[index]))
            .GroupBy(choice => choice.Leg)
            .Select(group =>
            {
                var detail = legs[group.Key];
                return new ShippingRouteLegDto(
                    detail.Method.Id,
                    detail.Method.Name,
                    detail.Origin.Id,
                    detail.Origin.Name,
                    detail.Method.Cost,
                    detail.DistanceKm,
                    detail.Origin.Latitude,
                    detail.Origin.Longitude,
                    group.Select(choice => choice.ProductId).ToArray());
            })
            .ToArray();

        var baseCost = routeLegs.Sum(leg => leg.Cost);
        var discount = await DiscountAsync(request, baseCost, routeLegs, destination, cancellationToken);
        var primary = legs[plan[0].Leg].Method;

        return new ShippingQuoteDto(
            true,
            null,
            primary.Id,
            primary.Name,
            primary.MethodType,
            primary.EstimatedDeliveryDays,
            baseCost,
            discount.DiscountAmount,
            baseCost - discount.DiscountAmount,
            discount.AppliedCouponCodes,
            routeLegs,
            [],
            destination?.Latitude,
            destination?.Longitude);
    }

    private async Task<ShippingDiscountResult> DiscountAsync(
        ShippingQuoteRequest request,
        decimal baseCost,
        IReadOnlyList<ShippingRouteLegDto> routeLegs,
        ShippingCoordinates? destination,
        CancellationToken cancellationToken)
    {
        if (discounts is null || baseCost <= 0m)
        {
            return ShippingDiscountResult.None;
        }

        var codes = (request.CouponCodes ?? [])
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var result = await discounts.EvaluateAsync(
            new ShippingDiscountContext(
                baseCost,
                request.Items,
                codes,
                destination?.Latitude,
                destination?.Longitude,
                routeLegs
                    .Select(leg => leg.OriginLatitude is { } latitude && leg.OriginLongitude is { } longitude
                        ? new ShippingCoordinates(latitude, longitude)
                        : null)
                    .ToArray()),
            cancellationToken);
        var amount = Math.Clamp(result.DiscountAmount, 0m, baseCost);

        return amount == 0m ? ShippingDiscountResult.None : new ShippingDiscountResult(amount, result.AppliedCouponCodes);
    }

    private async Task<IReadOnlyList<Route>> LoadRoutesAsync(
        IReadOnlyList<ShippingMethod> methods,
        string postalCode,
        CancellationToken cancellationToken)
    {
        var methodIds = methods.Select(method => method.Id).ToList();
        var zones = await persistence.Query<DeliveryZone>()
            .Where(zone => zone.IsActive && methodIds.Contains(zone.ShippingMethodId))
            .ToListAsync(cancellationToken);
        var zoneIds = zones.Select(zone => zone.Id).ToList();
        var rules = (await persistence.Query<DeliveryZonePostalCodeRule>()
                .Where(rule => zoneIds.Contains(rule.DeliveryZoneId))
                .ToListAsync(cancellationToken))
            .ToLookup(rule => rule.DeliveryZoneId);
        var links = await persistence.Query<ShippingMethodFulfillmentOrigin>()
            .Where(link => methodIds.Contains(link.ShippingMethodId))
            .ToListAsync(cancellationToken);
        var originIds = links.Select(link => link.FulfillmentOriginId).Distinct().ToList();
        var origins = (await persistence.Query<FulfillmentOrigin>()
                .Where(origin => origin.IsActive && originIds.Contains(origin.Id))
                .ToListAsync(cancellationToken))
            .ToDictionary(origin => origin.Id);
        var zonesByMethod = zones.ToLookup(zone => zone.ShippingMethodId);
        var linksByMethod = links.ToLookup(link => link.ShippingMethodId);

        return methods
            .Select(method =>
            {
                // A method with no active zone delivers anywhere; with zones, one of them must cover the code.
                var active = zonesByMethod[method.Id].ToArray();
                var covers = active.Length == 0 || active.Any(zone =>
                    rules[zone.Id].Any(rule => ShippingRules.Matches(rule.RuleType, rule.Pattern, postalCode)));
                var linked = linksByMethod[method.Id]
                    .Select(link => origins.GetValueOrDefault(link.FulfillmentOriginId))
                    .OfType<FulfillmentOrigin>()
                    .OrderBy(origin => origin.Name, StringComparer.Ordinal)
                    .ThenBy(origin => origin.Id)
                    .Select(origin => new Origin(origin.Id, origin.Name, origin.Latitude, origin.Longitude))
                    .ToArray();

                return new Route(method, covers, linked);
            })
            .ToArray();
    }

    private async Task<ShippingCoordinates?> LocateAsync(ShippingAddressDto address, CancellationToken cancellationToken)
    {
        if (address.Latitude is { } latitude && address.Longitude is { } longitude)
        {
            return ValidCoordinatePair(latitude, longitude) ? new ShippingCoordinates(latitude, longitude) : null;
        }

        var located = geocoder is null ? null : await geocoder.LocateAsync(address, cancellationToken);
        return located is not null && ValidCoordinatePair(located.Latitude, located.Longitude) ? located : null;
    }

    private static bool ValidCoordinatePair(double? latitude, double? longitude) =>
        latitude is null && longitude is null ||
        latitude is { } lat && longitude is { } lon && double.IsFinite(lat) && double.IsFinite(lon)
        && lat is >= -90 and <= 90 && lon is >= -180 and <= 180;

    private async Task ValidateAsync(SaveShippingMethodRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ShippingValidation.ThrowIfInvalid(request);

        if((request.DeliveryZones ?? []).Sum(zone=>zone.Rules?.Count ?? 0)>2000)
            throw new ArgumentException("At most 2000 postal rules are supported per method.");
        foreach (var zone in request.DeliveryZones ?? [])
        {
            ShippingValidation.ThrowIfInvalid(zone);
            foreach (var rule in zone.Rules ?? [])
            {
                ShippingValidation.ThrowIfInvalid(rule);
                if (rule.RuleType is PostalCodeRuleType.Range && !ShippingRules.TryParseRange(rule.Pattern, out _, out _))
                {
                    throw new ArgumentException($"Range rule \"{rule.Pattern}\" must be two whole numbers as from-to.");
                }
            }
        }

        var originIds = (request.FulfillmentOriginIds ?? []).Distinct().ToList();
        if (originIds.Count > 0
            && await persistence.Query<FulfillmentOrigin>().CountAsync(origin => originIds.Contains(origin.Id), cancellationToken) != originIds.Count)
        {
            throw new ArgumentException("A fulfilment origin named by this method does not exist.");
        }
    }

    private static void Validate(SaveFulfillmentOriginRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ShippingValidation.ThrowIfInvalid(request);
        ShippingValidation.ThrowIfInvalid(request.Address);

        if (request.Address.Latitude.HasValue != request.Address.Longitude.HasValue)
        {
            throw new ArgumentException("Latitude and longitude must be given together.");
        }
    }

    private static void Apply(ShippingMethod method, SaveShippingMethodRequest request)
    {
        method.Name = request.Name.Trim();
        method.Code = Clean(request.Code);
        method.Description = request.Description?.Trim() ?? string.Empty;
        method.Cost = decimal.Round(request.Cost, 2, MidpointRounding.AwayFromZero);
        method.EstimatedDeliveryDays = request.EstimatedDeliveryDays;
        method.MaxDistanceKm = request.MaxDistanceKm;
        method.IsActive = request.IsActive;
        method.MethodType = request.MethodType;
        method.ProductIds = (request.ProductIds ?? []).Where(id => id != Guid.Empty).Distinct().ToArray();
    }

    private void StageChildren(Guid methodId, SaveShippingMethodRequest request)
    {
        var zones = request.DeliveryZones ?? [];
        for (var position = 0; position < zones.Count; position++)
        {
            var zone = new DeliveryZone
            {
                Id = Guid.CreateVersion7(),
                ShippingMethodId = methodId,
                Name = zones[position].Name.Trim(),
                Description = zones[position].Description?.Trim() ?? string.Empty,
                IsActive = zones[position].IsActive,
                SortOrder = position
            };
            persistence.Add(zone);
            persistence.AddRange((zones[position].Rules ?? []).Select(rule => new DeliveryZonePostalCodeRule
            {
                DeliveryZoneId = zone.Id,
                RuleType = rule.RuleType,
                Pattern = rule.Pattern.Trim(),
                SortOrder = rule.SortOrder
            }));
        }

        persistence.AddRange((request.FulfillmentOriginIds ?? []).Distinct().Select(originId => new ShippingMethodFulfillmentOrigin
        {
            ShippingMethodId = methodId,
            FulfillmentOriginId = originId
        }));
    }

    private async Task ApplyAsync(FulfillmentOrigin origin, SaveFulfillmentOriginRequest request, CancellationToken cancellationToken)
    {
        // An origin without coordinates cannot serve a distance-limited method, so place it now if anything can.
        var located = await LocateAsync(request.Address, cancellationToken);

        origin.Name = request.Name.Trim();
        origin.IsActive = request.IsActive;
        origin.SupplierId = request.SupplierId;
        origin.StreetAddress = Clean(request.Address.StreetAddress);
        origin.ApartmentUnit = Clean(request.Address.ApartmentUnit);
        origin.Suburb = Clean(request.Address.Suburb);
        origin.City = Clean(request.Address.City);
        origin.Province = Clean(request.Address.Province);
        origin.PostalCode = Clean(request.Address.PostalCode);
        origin.Country = Clean(request.Address.Country);
        origin.Latitude = located?.Latitude;
        origin.Longitude = located?.Longitude;
    }

    private async Task<IReadOnlyList<AdminShippingMethodDto>> ToAdminAsync(
        IReadOnlyList<ShippingMethod> methods,
        CancellationToken cancellationToken)
    {
        var methodIds = methods.Select(method => method.Id).ToList();
        var zones = await persistence.Query<DeliveryZone>()
            .Where(zone => methodIds.Contains(zone.ShippingMethodId))
            .ToListAsync(cancellationToken);
        var zoneIds = zones.Select(zone => zone.Id).ToList();
        var rules = (await persistence.Query<DeliveryZonePostalCodeRule>()
                .Where(rule => zoneIds.Contains(rule.DeliveryZoneId))
                .ToListAsync(cancellationToken))
            .ToLookup(rule => rule.DeliveryZoneId);
        var links = (await persistence.Query<ShippingMethodFulfillmentOrigin>()
                .Where(link => methodIds.Contains(link.ShippingMethodId))
                .ToListAsync(cancellationToken))
            .ToLookup(link => link.ShippingMethodId, link => link.FulfillmentOriginId);
        var zonesByMethod = zones.ToLookup(zone => zone.ShippingMethodId);

        return methods
            .Select(method => new AdminShippingMethodDto(
                method.Id,
                method.Name,
                method.Code,
                method.Description,
                method.Cost,
                method.EstimatedDeliveryDays,
                method.MaxDistanceKm,
                method.MethodType,
                method.IsActive,
                method.ProductIds,
                links[method.Id].Order().ToArray(),
                zonesByMethod[method.Id]
                    .OrderBy(zone => zone.SortOrder)
                    .ThenBy(zone => zone.Id)
                    .Select(zone => new AdminDeliveryZoneDto(
                        zone.Id,
                        zone.Name,
                        zone.Description,
                        zone.IsActive,
                        rules[zone.Id]
                            .OrderBy(rule => rule.SortOrder)
                            .ThenBy(rule => rule.Id)
                            .Select(rule => new AdminPostalCodeRuleDto(rule.RuleType, rule.Pattern, rule.SortOrder))
                            .ToArray()))
                    .ToArray(),
                method.CreatedAt,
                method.UpdatedAt))
            .ToArray();
    }

    private static ClientShippingMethodDto ToClient(ShippingMethod method) => new(
        method.Id,
        method.Name,
        method.Code,
        method.Description,
        method.Cost,
        method.EstimatedDeliveryDays,
        method.MaxDistanceKm,
        method.MethodType);

    private static AdminFulfillmentOriginDto ToDto(FulfillmentOrigin origin) => new(
        origin.Id,
        origin.Name,
        origin.IsActive,
        origin.SupplierId,
        new ShippingAddressDto(
            origin.StreetAddress,
            origin.ApartmentUnit,
            origin.Suburb,
            origin.City,
            origin.Province,
            origin.PostalCode,
            origin.Country,
            origin.Latitude,
            origin.Longitude),
        origin.CreatedAt,
        origin.UpdatedAt);

    private static ShippingQuoteDto Unavailable(
        string message,
        IReadOnlyList<ShippingQuoteFailureDto> failures,
        ShippingCoordinates? destination) =>
        new(false, message, null, null, null, null, 0m, 0m, 0m, [], [], failures, destination?.Latitude, destination?.Longitude);

    private async Task EnsureManagerAsync(Guid? actingUserId, CancellationToken cancellationToken)
    {
        if (!await IsManagerAsync(actingUserId, cancellationToken))
        {
            throw new UnauthorizedAccessException();
        }
    }

    private async Task<bool> IsManagerAsync(Guid? actingUserId, CancellationToken cancellationToken) =>
        actingUserId is { } userId
        && await authorization.HasPermissionAsync(userId, AccessPermissions.ManageShipping, cancellationToken);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record Origin(Guid Id, string Name, double? Latitude, double? Longitude);

    private sealed record Route(ShippingMethod Method, bool CoversPostalCode, IReadOnlyList<Origin> Origins);

    private sealed record LegDetail(ShippingMethod Method, Origin Origin, double? DistanceKm);
}
