namespace CompanyHQ.Services.Sales.Contracts.Dtos;
/// <summary>Generic physical and descriptive specifications. Units are explicit company data.</summary>
public sealed record ProductSpecificationsDto(decimal? Width=null,decimal? Height=null,decimal? Length=null,string DimensionUnit="cm",
    decimal? Weight=null,string WeightUnit="kg",IReadOnlyDictionary<string,string>? Attributes=null,IReadOnlyDictionary<string,string>? Policies=null);
/// <summary>Published review without private account details.</summary>
public sealed record ClientProductReviewDto(Guid Id,string Title,string Comment,int Rating,string AuthorName,DateTimeOffset CreatedAt);
/// <summary>Staff profitability scenario, including customer delivery subsidy impact.</summary>
public sealed record AdminProductProfitScenarioDto(Guid VariantId,string VariantSku,string Scenario,int Quantity,decimal DiscountPercentage,
    decimal DiscountedPrice,decimal ShippingImpact,decimal ProfitTotal,decimal ProfitPercentage);
