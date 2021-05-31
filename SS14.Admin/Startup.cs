using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using Content.Server.Database;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using SS14.Admin.SignIn;

namespace SS14.Admin
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
            services.AddScoped<SignInManager>();
            services.AddScoped<LoginHandler>();

            PostgresServerDbContext.DoOptionsCheck = false;

            services.AddDbContext<PostgresServerDbContext>(options =>
                options.UseNpgsql(
                    Configuration.GetConnectionString("DefaultConnection")));

            services.AddControllers();
            services.AddRazorPages(options =>
            {
                options.Conventions.AuthorizeFolder("/Players");
                options.Conventions.AuthorizeFolder("/Connections");
                options.Conventions.AuthorizeFolder("/Bans");
            });

            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

            services.AddAuthentication(options =>
                {
                    options.DefaultScheme = "Cookies";
                    options.DefaultChallengeScheme = "oidc";
                })
                .AddCookie("Cookies", options =>
                {
                    options.ExpireTimeSpan = TimeSpan.FromHours(1);
                })
                .AddOpenIdConnect("oidc", options =>
                {
                    options.SignInScheme = "Cookies";

                    options.Authority = Configuration["Auth:Authority"];
                    options.ClientId = Configuration["Auth:ClientId"];
                    options.ClientSecret = Configuration["Auth:ClientSecret"];
                    options.SaveTokens = true;

                    options.Events.OnTokenValidated = async ctx =>
                    {
                        var handler = ctx.HttpContext.RequestServices.GetRequiredService<LoginHandler>();
                        await handler.HandleTokenValidated(ctx);
                    };
                });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseSerilogRequestLogging();

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

            app.UseAuthentication();
            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRazorPages();
                endpoints.MapControllers();
            });
        }
    }
}