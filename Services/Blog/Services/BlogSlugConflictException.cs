namespace CompanyHQ.Blogs.Services;

public sealed class BlogSlugConflictException : Exception
{
    public BlogSlugConflictException() : base("A blog post with this slug already exists.")
    {
    }
}
