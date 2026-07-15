using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Web.Admin.Overrides.Pages;

public sealed class Dashboard : ComponentBase
{
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "section");
        builder.AddAttribute(1, "data-page", "integration-override");
        builder.AddContent(2, "Integration override dashboard");
        builder.CloseElement();
    }
}
