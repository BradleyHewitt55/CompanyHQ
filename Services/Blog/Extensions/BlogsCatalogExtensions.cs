using CompanyHQ.ServiceCatalog;
using CompanyHQ.ServiceCatalog.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyHQ.Blogs.Extensions;

public static class BlogsCatalogExtensions
{
    /// <summary>Registers public metadata. The host explicitly allowlists this service in AddServiceCatalog.</summary>
    public static IServiceCollection AddBlogsServiceDescriptor(this IServiceCollection services,
        string? loadedProvider = null, Func<IServiceProvider, bool>? isConfigured = null) =>
        services.AddServiceDescriptor(new(new(
            "blogs", "Blogs", "Publish articles and news on your website.",
            "Write, schedule and publish blog posts with cover images. Drafts and future-dated posts stay hidden until they are published. Turning it off removes the blog from your site and admin; posts are kept.",
            Core: false, Dependencies: [],
            Providers: null,
            DefaultProvider: null, MenuPath: "/blog", Capabilities: null, ReadPermission: "blogs.manage", AdditionalReadPermissions: ["list_blog_posts"]),
            typeof(BlogsCatalogExtensions).Assembly, loadedProvider,
            isConfigured));
}
