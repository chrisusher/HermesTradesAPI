using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.OpenApi.Extensions;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Services;

var host = new HostBuilder()
	.ConfigureFunctionsWebApplication(app =>
	{
		app.Use(next => new FunctionExecutionDelegate(async context =>
		{
			var httpContext = context.GetHttpContext();

			if (httpContext is not null)
			{
				httpContext.Response.Headers["Access-Control-Allow-Origin"] = "*";
				httpContext.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization";
				httpContext.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";

				if (httpContext.Request.Method == "OPTIONS")
				{
					httpContext.Response.StatusCode = 200;
					return;
				}
			}

			await next(context);
		}));
	})
	.ConfigureOpenApi()
	.ConfigureServices((context, services) =>
	{
		// Configure logging to suppress Azure Storage noise
		services.Configure<LoggerFilterOptions>(options =>
		{
			options.AddFilter("Azure.Storage.Blobs", LogLevel.Error);
			options.AddFilter("Azure.Storage.Common", LogLevel.Error);
			options.AddFilter("Azure.Core", LogLevel.Error);
			options.AddFilter("Azure", LogLevel.Error);
			options.AddFilter("Microsoft.Azure.Storage", LogLevel.Error);
			options.AddFilter("Microsoft.Azure.WebJobs.Host.Blobs", LogLevel.Error);
			options.AddFilter("Microsoft.Azure.WebJobs.Extensions.Storage", LogLevel.Error);
		});

		services.AddHttpClient();

		var configuration = context.Configuration;
		services.AddHermesTradeServices(configuration);
	})
	.Build();

host.Run();