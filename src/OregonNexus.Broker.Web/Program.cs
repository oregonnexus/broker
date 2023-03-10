// Copyright: 2023 Education Nexus Oregon
// Author: Makoa Jacobsen, makoa@makoajacobsen.com

using Microsoft.EntityFrameworkCore;
using OregonNexus.Broker.Data;
using MediatR;
using Autofac;
using OregonNexus.Broker.SharedKernel;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

// Add Autofac
//builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());

// Add services to the container.

builder.Services.AddMediatR(typeof(Program).Assembly);

var msSqlConnectionString = builder.Configuration.GetConnectionString("MsSqlBrokerDatabase") ?? throw new InvalidOperationException("Connection string 'MsSqlBrokerDatabase' not found.");
var pgSqlConnectionString = builder.Configuration.GetConnectionString("PgSqlBrokerDatabase") ?? throw new InvalidOperationException("Connection string 'PgSqlBrokerDatabase' not found.");

builder.Services.AddDbContext<BrokerDbContext>(options => {
    if (msSqlConnectionString is not null && msSqlConnectionString != "")
    {
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("MsSqlBrokerDatabase")!,
            x => x.MigrationsAssembly("OregonNexus.Broker.Data.Migrations.SqlServer")
        );
    }
    if (pgSqlConnectionString is not null && pgSqlConnectionString != "")
    {
        options.UseNpgsql(
            builder.Configuration.GetConnectionString("PgSqlBrokerDatabase")!,
            x => x.MigrationsAssembly("OregonNexus.Broker.Data.Migrations.PostgreSQL")
        );
    }
}
);

builder.Services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
builder.Services.AddScoped(typeof(IMediator), typeof(Mediator));

builder.Services.AddIdentity<IdentityUser<Guid>, IdentityRole<Guid>>(options =>
{
    options.User.RequireUniqueEmail = false;
})
.AddEntityFrameworkStores<BrokerDbContext>();

builder.Services.ConfigureApplicationCookie(options => 
{
    options.AccessDeniedPath = "/AccessDenied";
    options.Cookie.Name = "OregonNexus.Broker";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
    options.LoginPath = "/Login";
    // ReturnUrlParameter requires 
    //using Microsoft.AspNetCore.Authentication.Cookies;
    options.ReturnUrlParameter = CookieAuthenticationDefaults.ReturnUrlParameter;
    options.SlidingExpiration = true;
});

builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".OregonNexus.Broker.Session";
    options.IdleTimeout = TimeSpan.FromSeconds(60);
    options.Cookie.IsEssential = true;
});

builder.Services.AddAuthentication()
    .AddGoogle(googleOptions =>
    {
        googleOptions.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
        googleOptions.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
    })
    .AddMicrosoftAccount(microsoftOptions =>
    {
        microsoftOptions.ClientId = builder.Configuration["Authentication:Microsoft:ClientId"]!;
        microsoftOptions.ClientSecret = builder.Configuration["Authentication:Microsoft:ClientSecret"]!;
    }
);

builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
