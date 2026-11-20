using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Extensions;
using Umbraco.Cms.Core.Notifications;

namespace SearchDemo.DemoSites;

public class DemoSiteComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.Configure<DemoSiteOptions>(builder.Config.GetSection("DemoSites"));
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, DemoSiteSeeder>();
    }
}
