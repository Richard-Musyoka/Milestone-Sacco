using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using SaccoManagementSystem.Data;
using ChartJs.Blazor;

var builder = WebApplication.CreateBuilder(args);

// Load configuration files
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                     .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true);

// Get the connection string from appsettings.json
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Add services to the container
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddAuthorization();
builder.Services.AddControllers();  // 


// 🔑 Register your Authentication State Provider
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
builder.Services.AddScoped<CustomAuthStateProvider>();

// 🔑 Register Entity Framework DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// ✅ FIX: Register HttpClient so Blazor components can inject it
builder.Services.AddHttpClient("ApiClient", client =>
{
    client.BaseAddress = new Uri("https://localhost:7074/");  
});


var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.MapControllers();  // ✅ This maps the controllers like AuthController
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
