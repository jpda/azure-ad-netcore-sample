using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json.Linq;

namespace AzureAdNetCoreSample
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true);

            if (env.IsDevelopment())
            {
                // For more details on using the user secret store see http://go.microsoft.com/fwlink/?LinkID=532709
                builder.AddUserSecrets();
            }
            builder.AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Add framework services.
            services.AddMvc();
            services.AddAuthentication(sharedOptions => sharedOptions.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseBrowserLink();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();
            app.UseCookieAuthentication();

            //there are many different events you can listen for – like authentication failed, AuthorizationCodeReceived (useful for requesting tokens for downstream APIs, etc)
            app.UseOpenIdConnectAuthentication(new OpenIdConnectOptions
            {
                ClientId = Configuration["Authentication:AzureAd:ClientId"],
                Authority = Configuration["Authentication:AzureAd:AADInstance"] + Configuration["Authentication:AzureAd:TenantId"],
                CallbackPath = Configuration["Authentication:AzureAd:CallbackPath"],
                Events = new OpenIdConnectEvents()
                {
                    OnTokenValidated = async ctx =>
                    {
                        //here you can add your own claims with additional data. OnTokenValidated happens *after* the user has been redirected to Azure AD, a token has been issued AND the 
                        //  OpenIdConnect middleware has validated the token with Azure AD – this event is happening right *before* the local application cookie is issued – meaning any claims
                        //  you add here will be part of the cookie and will persist the duration of the session.
                        //  If you add them at a different place in the pipeline, you risk having to fetch them again.
                        //Connect to database or service to fetch user-specific info
                        //add user data to new Identity which is being added to the ticket Principal
                        var extraIdentity = new ClaimsIdentity(new List<Claim>() { new Claim("someIdentifier:ClaimName", "claimValue") });

                        //for group membership with the Authorize attribute, use the Role claim type - be aware the Azure AD Application Role claims use this type as well
                        //but be aware - these are group GUIDs in Azure AD - not group names as you would expect to see them in AD
                        var groups = ctx.Ticket.Principal.Claims.Where(x => x.Type == "groups");
                        foreach (var g in groups)
                        {
                            //or we *could* resolve the group name, if that's preferable - note that this will require the Group.Read.All scope, which REQUIRES administrator consent
                            // authenticate as the app
                            var appCredential = new ClientCredential(Configuration["Authentication:AzureAd:ClientId"], Configuration["Authentication:AzureAd:ClientSecret"]);
                            // create a new authentication context
                            var appCtx = new AuthenticationContext(Configuration["Authentication:AzureAd:AADInstance"] + Configuration["Authentication:AzureAd:TenantId"]);
                            // ask for a token connecting to the graph resource
                            var appGraphToken = await appCtx.AcquireTokenAsync("https://graph.microsoft.com", appCredential);
                            
                            var wc = new HttpClient();
                            wc.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", appGraphToken.AccessToken);
                            var groupData = await wc.GetAsync($"https://graph.microsoft.com/v1.0/groups/{g.Value}");

                            if (!groupData.IsSuccessStatusCode && groupData.StatusCode == HttpStatusCode.NotFound)
                            {
                                extraIdentity.AddClaim(new Claim(ClaimTypes.Role, g.Value));
                                continue;
                            }

                            var j = JObject.Parse(await groupData.Content.ReadAsStringAsync());
                            var groupName = j["displayName"].Value<string>();
                            extraIdentity.AddClaim(new Claim(ClaimTypes.Role, groupName));
                        }
                        ctx.Ticket.Principal.AddIdentity(extraIdentity);
                    }
                }
            });

            app.UseMvc(routes =>
            {
                routes.MapRoute(name: "default", template: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}