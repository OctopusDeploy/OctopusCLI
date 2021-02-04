using System;
using Nancy;
using Serilog;

namespace Octo.Tests.Integration
{
    public class CatchAllModule : NancyModule
    {
        public CatchAllModule()
        {
            Get(@"/{uri*}",
                p =>
                {
                    Log.Error($"Nothing listening at {Request.Url.Path}");
                    return HttpStatusCode.NotFound;
                });
            Post(@"/{uri*}",
                p =>
                {
                    Log.Error($"Nothing listening at {Request.Url.Path}");
                    return HttpStatusCode.NotFound;
                });
        }
    }
}
