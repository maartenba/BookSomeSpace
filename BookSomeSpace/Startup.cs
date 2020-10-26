using System;
using System.IO;
using System.Net.Http;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SpaceDotNet.AspNetCore.Authentication.Space;
using SpaceDotNet.AspNetCore.Authentication.Space.Experimental.TokenManagement;
using SpaceDotNet.Common;

namespace BookSomeSpace
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Local storage
            services.AddSingleton<SettingsStorage>(provider =>
            {
                if (Environment.GetEnvironmentVariable("REGION_NAME") != null 
                    && Environment.GetEnvironmentVariable("HOME") != null)
                {
                    // Azure
                    return new SettingsStorage(Path.Combine(Environment.GetEnvironmentVariable("HOME")!, "Data", "BookSomeSpace", "Profiles"));
                }
                
                // Local
                return new SettingsStorage(Path.Combine(Path.GetFullPath("."), "Data", "BookSomeSpace", "Profiles"));
            });
            
            // Razor pages
            services.AddRouting(options => options.LowercaseUrls = true);
            services.AddHttpClient();
            services.AddRazorPages();
            
            // Space authentication
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = SpaceDefaults.AuthenticationScheme;
                })
                .AddCookie()
                .AddSpace(options => Configuration.Bind("Space", options))
                .AddSpaceTokenManagement(provider =>
                    new ClientCredentialsConnection(
                        new Uri(Configuration["Space:ServerUrl"]),
                        Configuration["Space:ClientId"],
                        Configuration["Space:ClientSecret"],
                        provider.GetService<IHttpClientFactory>().CreateClient()));
            
            // Space client API
            services.AddSpaceClientApi();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints => { endpoints.MapRazorPages(); });
        }
    }
}