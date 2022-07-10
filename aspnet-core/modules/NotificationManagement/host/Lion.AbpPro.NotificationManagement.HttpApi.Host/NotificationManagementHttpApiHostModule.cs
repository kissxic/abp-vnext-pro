using System;
using System.Linq;
using Lion.AbpPro.CAP;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Lion.AbpPro.NotificationManagement.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Savorboard.CAP.InMemoryMessageQueue;
using StackExchange.Redis;
using Swashbuckle.AspNetCore.SwaggerUI;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc.AntiForgery;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.MySQL;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.Swashbuckle;
using Volo.Abp.VirtualFileSystem;

namespace Lion.AbpPro.NotificationManagement;

[DependsOn(
    typeof(NotificationManagementApplicationModule),
    typeof(NotificationManagementEntityFrameworkCoreModule),
    typeof(NotificationManagementHttpApiModule),
    typeof(AbpAutofacModule),
    typeof(AbpCachingStackExchangeRedisModule),
    typeof(AbpAspNetCoreSerilogModule),
    typeof(AbpSwashbuckleModule),
    typeof(AbpProAbpCapModule),
    typeof(AbpEntityFrameworkCoreMySQLModule)
)]
public class NotificationManagementHttpApiHostModule : AbpModule
{
    private const string DefaultCorsPolicyName = "Default";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        ConfigureVirtualFileSystem();
        ConfigureSwaggerServices(context);
        ConfigAntiForgery();
        ConfigureLocalization();
        ConfigureCap(context);
        ConfigureCache(context);
        ConfigureCors(context);
        ConfigDB();
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        var env = context.GetEnvironment();
        app.UseCorrelationId();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseCors(DefaultCorsPolicyName);
        app.UseAuthentication();
        app.UseAbpRequestLocalization();
        app.UseAuthorization();
        app.UseSwagger();
        app.UseAbpSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "NotificationManagement API");
            options.DocExpansion(DocExpansion.None);
            options.DefaultModelExpandDepth(-2);
        });
        app.UseAuditing();
        app.UseAbpSerilogEnrichers();
        app.UseConfiguredEndpoints();
    }

    /// <summary>
    ///     配置虚拟文件系统
    /// </summary>
    private void ConfigureVirtualFileSystem()
    {
        Configure<AbpVirtualFileSystemOptions>(options => { options.FileSets.AddEmbedded<NotificationManagementHttpApiHostModule>(); });
    }

    private void ConfigDB()
    {
        Configure<AbpDbContextOptions>(options =>
        {
            /* The main point to change your DBMS.
             * See also OperationsMigrationsDbContextFactory for EF Core tooling. */
            options.UseMySQL();
        });
    }

    private void ConfigureCap(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        var enabled = configuration.GetValue("Cap:Enabled", false);
        if (enabled)
        {
            context.AddAbpCap(capOptions =>
            {
                capOptions.UseEntityFramework<NotificationManagementHttpApiHostMigrationsDbContext>();
                capOptions.UseRabbitMQ(option =>
                {
                    option.HostName = configuration.GetValue<string>("Cap:RabbitMq:HostName");
                    option.UserName = configuration.GetValue<string>("Cap:RabbitMq:UserName");
                    option.Password = configuration.GetValue<string>("Cap:RabbitMq:Password");
                });

                var hostingEnvironment = context.Services.GetHostingEnvironment();
                bool auth = !hostingEnvironment.IsDevelopment();
                capOptions.UseDashboard(options => { options.UseAuth = auth; });
            });
        }
        else
        {
            context.AddAbpCap(capOptions =>
            {
                capOptions.UseInMemoryStorage();
                capOptions.UseInMemoryMessageQueue();
                var hostingEnvironment = context.Services.GetHostingEnvironment();
                bool auth = !hostingEnvironment.IsDevelopment();
                capOptions.UseDashboard(options => { options.UseAuth = auth; });
            });
        }
    }

    /// <summary>
    ///     配置SwaggerUI
    /// </summary>
    /// <param name="context"></param>
    private static void ConfigureSwaggerServices(ServiceConfigurationContext context)
    {
        context.Services.AddSwaggerGen(
            options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "NotificationManagement API", Version = "v1" });
                options.DocInclusionPredicate((docName, description) => true);


                #region 多语言

                options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.ApiKey,
                    In = ParameterLocation.Header,
                    Name = "Accept-Language",
                    Description = "多语言"
                });

                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" }
                        },
                        new string[] { }
                    }
                });

                #endregion
            });
    }

    private void ConfigAntiForgery()
    {
        Configure<AbpAntiForgeryOptions>(options => { options.AutoValidate = false; });
    }

    /// <summary>
    ///     配置本地化
    /// </summary>
    private void ConfigureLocalization()
    {
        Configure<AbpLocalizationOptions>(options =>
        {
            options.Languages.Add(new LanguageInfo("ar", "ar", "العربية"));
            options.Languages.Add(new LanguageInfo("cs", "cs", "Čeština"));
            options.Languages.Add(new LanguageInfo("en", "en", "English"));
            options.Languages.Add(new LanguageInfo("en-GB", "en-GB", "English (UK)"));
            options.Languages.Add(new LanguageInfo("fi", "fi", "Finnish"));
            options.Languages.Add(new LanguageInfo("fr", "fr", "Français"));
            options.Languages.Add(new LanguageInfo("hi", "hi", "Hindi", "in"));
            options.Languages.Add(new LanguageInfo("is", "is", "Icelandic", "is"));
            options.Languages.Add(new LanguageInfo("it", "it", "Italiano", "it"));
            options.Languages.Add(new LanguageInfo("hu", "hu", "Magyar"));
            options.Languages.Add(new LanguageInfo("pt-BR", "pt-BR", "Português"));
            options.Languages.Add(new LanguageInfo("ro-RO", "ro-RO", "Română"));
            options.Languages.Add(new LanguageInfo("ru", "ru", "Русский"));
            options.Languages.Add(new LanguageInfo("sk", "sk", "Slovak"));
            options.Languages.Add(new LanguageInfo("tr", "tr", "Türkçe"));
            options.Languages.Add(new LanguageInfo("zh-Hans", "zh-Hans", "简体中文"));
            options.Languages.Add(new LanguageInfo("zh-Hant", "zh-Hant", "繁體中文"));
            options.Languages.Add(new LanguageInfo("de-DE", "de-DE", "Deutsch"));
            options.Languages.Add(new LanguageInfo("es", "es", "Español"));
        });
    }

    /// <summary>
    ///     Redis缓存
    /// </summary>
    /// <param name="context"></param>
    private void ConfigureCache(ServiceConfigurationContext context)
    {
        Configure<AbpDistributedCacheOptions>(options =>
        {
            options.KeyPrefix = "NotificationManagement:";
            options.GlobalCacheEntryOptions = new DistributedCacheEntryOptions
            {
                // 全局缓存有效时间
                AbsoluteExpiration = DateTimeOffset.UtcNow.AddHours(2)
            };
        });

        var configuration = context.Services.GetConfiguration();
        var redis = ConnectionMultiplexer.Connect(configuration["Redis:Configuration"]);
        context.Services
            .AddDataProtection()
            .PersistKeysToStackExchangeRedis(redis, "YH.Wms.Operations-Keys");
    }

    /// <summary>
    ///     配置跨域
    /// </summary>
    private void ConfigureCors(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        context.Services.AddCors(options =>
        {
            options.AddPolicy(DefaultCorsPolicyName, builder =>
            {
                builder
                    .WithOrigins(
                        configuration["App:CorsOrigins"]
                            .Split(",", StringSplitOptions.RemoveEmptyEntries)
                            .Select(o => o.RemovePostFix("/"))
                            .ToArray()
                    )
                    .WithAbpExposedHeaders()
                    .SetIsOriginAllowedToAllowWildcardSubdomains()
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });
    }
}