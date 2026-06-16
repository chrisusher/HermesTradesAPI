#:sdk Aspire.AppHost.Sdk@13.3.0+4517e4a1ffb7f00a4c0e66882c2db952d637c0cc

var builder = DistributedApplication.CreateBuilder(args);

// The aspireify skill will wire up your projects here.

builder.Build().Run();