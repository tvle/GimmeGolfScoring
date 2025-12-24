var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.iDoublePress>("idoublepress");

builder.Build().Run();
