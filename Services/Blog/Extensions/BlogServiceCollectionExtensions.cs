using Microsoft.Extensions.DependencyInjection;
using CompanyHQ.Blogs.Services;

namespace CompanyHQ.Blogs.Extensions;

/// <summary>Registers the blog content service and its controller assembly.</summary>
public static class BlogServiceCollectionExtensions
{
    public static IServiceCollection AddBlogs(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IBlogService, BlogService>();
        services.AddControllers().AddApplicationPart(typeof(BlogServiceCollectionExtensions).Assembly);

        return services;
    }
}
