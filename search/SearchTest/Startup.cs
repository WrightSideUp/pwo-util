using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(SearchTest.Startup))]
namespace SearchTest
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
