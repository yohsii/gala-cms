using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.EntityFrameworkCore;
using puckweb.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using puck.core.Entities;
using puck.core.Abstract;
using puck.core.Concrete;
using puck.core.Helpers;
using puck.core.Services;
using puck.core.State;
using puck.core.Extensions;
using Microsoft.AspNetCore.Http;

namespace puckweb
{
    public class Startup
    {
        public Startup(IConfiguration configuration,IHostEnvironment env)
        {
            Configuration = configuration;
            Env = env;
        }
        IHostEnvironment Env { get; }
        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<PuckContext>(options =>
                options.UseSqlServer(
                    Configuration.GetConnectionString("DefaultConnection"))
                ,optionsLifetime:ServiceLifetime.Transient);
            services.AddDefaultIdentity<PuckUser>(options => { options.SignIn.RequireConfirmedAccount = false;})
                .AddRoles<PuckRole>()
                .AddEntityFrameworkStores<PuckContext>();
            services.AddMemoryCache();
            services.AddResponseCaching();
            services.AddSession();
            services.AddControllersWithViews()
                .AddApplicationPart(typeof(puck.core.Controllers.BaseController).Assembly)
                .AddControllersAsServices()
                .AddRazorRuntimeCompilation();
            services.AddRazorPages();
            services.AddAuthentication().AddCookie(puck.core.Constants.Mvc.AuthenticationScheme, options=> {
                options.LoginPath = "/puck/admin/in";
                options.LogoutPath = "/puck/admin/out";
                options.AccessDeniedPath= "/puck/admin/in";
                options.ForwardAuthenticate = "Identity.Application";
            });
            
            services.AddHttpContextAccessor();
            services.AddPuckServices(Env,Configuration);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IHttpContextAccessor httpContextAccessor)
        {
            var puckInit = puck.core.Bootstrap.Ini(Configuration,env,app.ApplicationServices, httpContextAccessor);
            puckInit.Wait();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseSession();
            app.UseResponseCaching();
            app.UseRouting();
            app.UseAuthentication();
            
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapAreaControllerRoute(
                    name:"puckarea",
                    areaName:"puck",
                    pattern: "puck/{controller=Home}/{action=Index}/{id?}"
                    );
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
                endpoints.MapRazorPages();
            });
        }
    }
}
