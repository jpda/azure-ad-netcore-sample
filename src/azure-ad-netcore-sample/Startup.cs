using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
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

            //force HTTPS off-box
            if (!env.IsDevelopment())
            {
                app.Use(async (context, next) =>
                {
                    if (context.Request.IsHttps)
                    {
                        await next();
                    }
                    else
                    {
                        var rewrite = $"https://{context.Request.Host}{context.Request.Path}{context.Request.QueryString}";
                        context.Response.Redirect(rewrite);
                    }
                });
            }

            //there are many different events you can listen for – like authentication failed, AuthorizationCodeReceived (useful for requesting tokens for downstream APIs, etc)
            app.UseOpenIdConnectAuthentication(new OpenIdConnectOptions
            {
                ClientId = Configuration["Authentication:AzureAd:ClientId"],
                Authority = Configuration["Authentication:AzureAd:AADInstance"] + Configuration["Authentication:AzureAd:TenantId"],
                CallbackPath = Configuration["Authentication:AzureAd:CallbackPath"],
                ResponseType = OpenIdConnectResponseType.CodeIdToken,
                Events = new OpenIdConnectEvents()
                {
                    OnAuthorizationCodeReceived = ctx =>
                    {
                        // since we're consuming and using the code during login @ OnTokenValidated below, we need to tell the middleware to skip it because we've already handled it
                        ctx.HandleCodeRedemption();
                        return Task.FromResult(0);
                    },
                    OnTokenValidated = async ctx =>
                    {
                        // here you can add your own claims with additional data. OnTokenValidated happens *after* the user has been redirected to Azure AD, a token has been issued AND the 
                        //   OpenIdConnect middleware has validated the token with Azure AD – this event is happening right *before* the local application cookie is issued – meaning any claims
                        //   you add here will be part of the cookie and will persist the duration of the session.
                        //   If you add them at a different place in the pipeline, you risk having to fetch them again.
                        // Connect to database or service to fetch user-specific info
                        // add user data to new Identity which is being added to the ticket Principal
                        var extraIdentity = new ClaimsIdentity(new List<Claim>() { new Claim("someIdentifier:ClaimName", "claimValue") });

                        // for group membership with the Authorize attribute, use the Role claim type - be aware the Azure AD Application Role claims use this type as well
                        // but be aware - these are group GUIDs in Azure AD - not group names as you would expect to see them in AD
                        // ToList this guy to prevent multiple enumerations
                        var groups = ctx.Ticket.Principal.Claims.Where(x => x.Type == "groups").ToList();

                        // do something with the groups - like add them to the claimset or make a downstream authorization decision
                        // you can add them as ClaimType.Role (below) to use the Authorize[Role=""] atrribute - but beware, the group is only the Azure AD group ID at this point. For more detail, you'll need to query the graph.

                        // extraIdentity.AddClaims(groups.Select(x => new Claim(ClaimTypes.Role, x.Value)));

                        // if you're going to resolve group names to AAD or Microsoft Graph, you can do that below - but ideally, check out AAD Application Roles instead

                        // we'll need to query the AAD Graph to resolve group names. Create a context to request tokens
                        var appCredential = new ClientCredential(Configuration["Authentication:AzureAd:ClientId"], Configuration["Authentication:AzureAd:ClientSecret"]);
                        var aadCtx = new AuthenticationContext(Configuration["Authentication:AzureAd:AADInstance"] + Configuration["Authentication:AzureAd:TenantId"]);

                        // If you're using the user's access to the directory, consume the Authorization Code for the graph here, to get the proper values written into the claimset
                        var userGraphToken = await aadCtx.AcquireTokenByAuthorizationCodeAsync(ctx.ProtocolMessage.Code, new Uri(ctx.Properties.Items[OpenIdConnectDefaults.RedirectUriForCodePropertiesKey]), appCredential, "https://graph.windows.net");

                        // Or, if you're using an App-Only token, we can request that here:
                        // note - if you're doing app-only, you'll need Groups.Read.All, which REQUIRES Admin consent
                        //var appGraphToken = await aadCtx.AcquireTokenAsync("https://graph.microsoft.com", appCredential);

                        var wc = new HttpClient();
                        wc.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userGraphToken.AccessToken);
                        //wc.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", appGraphToken.AccessToken);

                        foreach (var g in groups)
                        {
                            var url = $"https://graph.windows.net/{Configuration["Authentication:AzureAd:TenantId"]}/groups/{g.Value}?api-version=1.6";
                            var groupData = await wc.GetAsync(url);

                            // if it blows up for whatever reason, go ahead and just add the group GUID to the roles claimset
                            if (!groupData.IsSuccessStatusCode)
                            {
                                extraIdentity.AddClaim(new Claim(ClaimTypes.Role, g.Value));
                                continue;
                            }

                            var j = JObject.Parse(await groupData.Content.ReadAsStringAsync());
                            var groupName = j["displayName"].Value<string>();
                            extraIdentity.AddClaim(new Claim(ClaimTypes.Role, groupName));
                        }

                        // finally, add the identity to our existing one - when this method exits, the cookie gets written and we're in business.
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