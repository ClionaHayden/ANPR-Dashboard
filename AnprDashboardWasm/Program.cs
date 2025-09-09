using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using AnprDashboardWasm;
using AnprDashboardWasm.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp =>
    new HttpClient { BaseAddress = new Uri("http://localhost:5131/") });

builder.Services.AddSingleton<DetectionHubClient>();
builder.Services.AddScoped<ToastService>();
await builder.Build().RunAsync();
