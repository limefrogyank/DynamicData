using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DynamicData.SignalR.JSInterop.TestBlazorApp.Data;

namespace DynamicData.SignalR.JSInterop.TestBlazorApp
{
    public class Startup
    {
        public Startup(IWebHostEnvironment env)
        {
            using (var client = new TestContext())
            {
                client.Database.EnsureCreated();
            }
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddEntityFrameworkSqlite().AddDbContext<TestContext>();

            //services.AddSignalR().AddNewtonsoftJsonProtocol(options =>
            //{
            //    options.PayloadSerializerSettings.ContractResolver = new DynamicData.SignalR.CustomContractResolver();
            //});

            services.AddRazorPages();

            services.AddServerSideBlazor().AddSignalR().AddNewtonsoftJsonProtocol(config =>
            {
                config.PayloadSerializerSettings.ContractResolver = new DynamicData.SignalR.CustomContractResolver();
            })
                //hack to make other hubs work until preview-5 comes out.
            .AddHubOptions<DynamicData.SignalR.DynamicDataCacheHub<TestModel.Person, string, TestContext>>(config =>
            {
                config.SupportedProtocols = new List<string>();
                config.SupportedProtocols.Add("json");
            });

            services.AddSingleton<WeatherForecastService>();
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
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseStaticFiles();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapBlazorHub();
                endpoints.MapHub<DynamicData.SignalR.DynamicDataCacheHub<TestModel.Person, string, TestContext>>("/TestHub");
                endpoints.MapFallbackToPage("/_Host");
            });
        }
    }
}
