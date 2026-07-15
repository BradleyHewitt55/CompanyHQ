using Cli;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config => config.AddCommand<NewCompanyCommand>("new").WithDescription("Provision an isolated company."));
app.Configure(config => config.AddCommand<NewServiceCommand>("new-service").WithDescription("Scaffold a user-owned service."));
return app.Run(CliArguments.Normalize(args));
