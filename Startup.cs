using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using VMBackuperBeckEnd.Configuration;
using VMBackuperBeckEnd.Models;
using System;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Http;
using System.IO;
using VMBackuper.Services;
using System.Diagnostics;
using Newtonsoft.Json;
using System.Security.Claims;
using System.Collections.Generic;

namespace VMBackuperBeckEnd
{
    public class Startup
    {

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<JwtConfig>(Configuration.GetSection("JwtConfig"));
            services.AddSingleton<JwtConfig>();

            var SQLiteConnection = Configuration.GetConnectionString("SQLiteConnection");
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(SQLiteConnection));

            services.AddCors();
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultSignInScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultSignOutScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(jwt =>
            {
                var key = Encoding.ASCII.GetBytes(Configuration["JwtConfig:Secret"]);

                jwt.SaveToken = true;
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    RequireExpirationTime = false,
                    ClockSkew = TimeSpan.Zero,
                };
                jwt.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        // check user when deleted himself and force invalidate
                        var um = GetUserManager(services);
                        var userId = context.Principal.FindFirstValue("Id");
                        var user = um.FindByIdAsync(userId).Result;
                        if (user == null)
                            context.Fail("User is removed");
                        return System.Threading.Tasks.Task.CompletedTask;
                    }
                };
            });
            //-------------------------------------------------------------------------------------
            services.Configure<IdentityOptions>(options =>
            {
                options.Password.RequireDigit = false;
                options.Password.RequiredLength = 0;
                options.Password.RequireLowercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;

            });
            //-------------------------------------------------------------------------------------
            services.AddIdentity<UserAccount, IdentityRole>()
                .AddEntityFrameworkStores<AppDbContext>()
                .AddSignInManager<SignInManager<UserAccount>>();
            services.AddIdentityCore<UserAccount>().AddSignInManager<SignInManager<UserAccount>>();
            services.Configure<DataProtectionTokenProviderOptions>(opts => opts.TokenLifespan = TimeSpan.FromHours(10));
            //-------------------------------------------------------------------------------------
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            //-------------------------------------------------------------------------------------
            services.AddControllers();

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "VMBackuper", Version = "v1" });
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, UserManager<UserAccount> userManager)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "VMBackuper v1"));
            }
            //--------------------------------------------------------------------------------
            // configure SSH Service envs
            string assetsDirectory = Path.Combine(env.ContentRootPath, "Assets");
            string privateKeysDirectory = Path.Combine(assetsDirectory, "PrivateKeys");
            string scriptsDirectory = Path.Combine(assetsDirectory, "BashScripts");
            // prepare directory and unique filenames
            try { if (!Directory.Exists(assetsDirectory)) { DirectoryInfo folder = Directory.CreateDirectory(assetsDirectory); } }
            catch (Exception) { }
            try { if (!Directory.Exists(privateKeysDirectory)) { DirectoryInfo folder = Directory.CreateDirectory(privateKeysDirectory); } }
            catch (Exception) { }
            try { if (!Directory.Exists(scriptsDirectory)) { DirectoryInfo folder = Directory.CreateDirectory(scriptsDirectory); } }
            catch (Exception) { }
            HyperSshService.AssetsDirectory = assetsDirectory;
            HyperSshService.PrivateKeysDirectory = privateKeysDirectory;
            HyperSshService.ScriptsDirectory = scriptsDirectory;
            //--------------------------------------------------------------------------------
            string
                defaultUsername = "admin",
                defaultPassword = "admin";
            var newUser = new UserAccount() { UserName = defaultUsername };
            var existingUser = userManager.FindByNameAsync(newUser.UserName).Result;
            if (existingUser == null)
            {
                var isCreated = userManager.CreateAsync(newUser, defaultPassword).Result;
                if (isCreated.Succeeded)
                {
                    Console.WriteLine($"Added default user: {defaultUsername}/{defaultPassword}");
                }
            }
            //--------------------------------------------------------------------------------
            app.UseRouting();

            // Global cors policy
            app.UseCors(builder => builder
                .AllowAnyMethod()
                .AllowAnyHeader()
                .SetIsOriginAllowed(origin => true) // allow any origin
                .AllowCredentials()); // allow credentials

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        private UserManager<UserAccount> GetUserManager(IServiceCollection services)
        {
            return services.BuildServiceProvider().GetService<UserManager<UserAccount>>();
        }
    }
}
