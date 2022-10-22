﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Keycloak.Net;
using Keycloak.Net.Models.Clients;
using Keycloak.Net.Models.ClientScopes;
using Keycloak.Net.Models.ProtocolMappers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;

namespace EShopOnAbp.DbMigrator;

public class KeyCloakDataSeeder : IDataSeedContributor, ITransientDependency
{
    private readonly KeycloakClient _keycloakClient;
    private readonly KeycloakClientOptions _keycloakOptions;
    private readonly ILogger<KeyCloakDataSeeder> _logger;
    private readonly IConfiguration _configuration;

    public KeyCloakDataSeeder(IOptions<KeycloakClientOptions> keycloakClientOptions, ILogger<KeyCloakDataSeeder> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _keycloakOptions = keycloakClientOptions.Value;

        _keycloakClient = new KeycloakClient(
            _keycloakOptions.Url,
            _keycloakOptions.AdminUserName,
            _keycloakOptions.AdminPassword
        );
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        await UpdateAdminUserAsync();
        await CreateClientScopesAsync();
        await CreateClientsAsync();
    }

    private async Task CreateClientScopesAsync()
    {
        await CreateScopeAsync("AccountService");
        await CreateScopeAsync("AdministrationService");
        await CreateScopeAsync("IdentityService");
        await CreateScopeAsync("BasketService");
        await CreateScopeAsync("CatalogService");
        await CreateScopeAsync("OrderingService");
        await CreateScopeAsync("PaymentService");
        await CreateScopeAsync("CmskitService");
    }

    private async Task CreateScopeAsync(string scopeName)
    {
        var scope = (await _keycloakClient.GetClientScopesAsync(_keycloakOptions.RealmName))
            .FirstOrDefault(q => q.Name == scopeName);

        if (scope == null)
        {
            scope = new ClientScope
            {
                Name = scopeName,
                Description = scopeName + " scope",
                Protocol = "openid-connect",
                Attributes = new Attributes
                {
                    ConsentScreenText = scopeName,
                    DisplayOnConsentScreen = "true",
                    IncludeInTokenScope = "true"
                },
                ProtocolMappers = new List<ProtocolMapper>()
                {
                    new ProtocolMapper()
                    {
                        Name = scopeName,
                        Protocol = "openid-connect",
                        _ProtocolMapper = "oidc-audience-mapper",
                        Config = new Dictionary<string, string>()
                        {
                            { "id.token.claim", "false" },
                            { "access.token.claim", "true" },
                            { "included.custom.audience", scopeName }
                        }
                        // Config = new Config() // This should be dictionary -> Outdated library
                        // {
                        //     AccessTokenClaim = "true",
                        //     IdTokenClaim = "false"
                        // }
                    }
                }
            };

            await _keycloakClient.CreateClientScopeAsync(_keycloakOptions.RealmName, scope);
        }
    }

    private async Task CreateClientsAsync()
    {
        await CreatePublicWebClientAsync();
        await CreateSwaggerClientAsync(); // TODO: Test when Volo.Abp.Swashbuckle v6.0.1 is released (https://github.com/abpframework/abp/pull/14409)
        await CreateWebClientAsync();
    }

    private async Task CreateWebClientAsync()
    {
        var webClient = (await _keycloakClient.GetClientsAsync(_keycloakOptions.RealmName, clientId: "Web"))
            .FirstOrDefault();

        if (webClient == null)
        {
            var webRootUrl = _configuration[$"Clients:Web:RootUrl"];
            webClient = new Client
            {
                ClientId = "Web",
                Name = "Angular Back-Office Web Application",
                Protocol = "openid-connect",
                Enabled = true,
                BaseUrl = webRootUrl,
                RedirectUris = new List<string>
                {
                    $"{webRootUrl.TrimEnd('/')}"
                },
                FrontChannelLogout = true,
                PublicClient = true
            };
            webClient.Attributes = new Dictionary<string, object>
            {
                { "post.logout.redirect.uris", $"{webRootUrl.TrimEnd('/')}" }
            };

            await _keycloakClient.CreateClientAsync(_keycloakOptions.RealmName, webClient);
            //TODO: Update when //https://github.com/AnderssonPeter/Keycloak.Net/pull/5 is merged
            // await AddOptionalClientScopesAsync(
            //     "PublicWeb",
            //     new List<string>
            //     {
            //         "AccountService", "AdministrationService", "IdentityService", "BasketService", "CatalogService",
            //         "OrderingService", "PaymentService", "CmskitService"
            //     }
            // );
        }
    }

    private async Task CreateSwaggerClientAsync()
    {
        var swaggerClient = (await _keycloakClient.GetClientsAsync(_keycloakOptions.RealmName, clientId: "SwaggerClient"))
            .FirstOrDefault();
        
        if (swaggerClient == null)
        {
            var webGatewaySwaggerRootUrl = _configuration[$"Clients:WebGateway:RootUrl"].TrimEnd('/');
            var publicWebGatewayRootUrl = _configuration[$"Clients:PublicWebGateway:RootUrl"].TrimEnd('/');
            var accountServiceRootUrl = _configuration[$"Clients:AccountService:RootUrl"].TrimEnd('/');
            var identityServiceRootUrl = _configuration[$"Clients:IdentityService:RootUrl"].TrimEnd('/');
            var administrationServiceRootUrl = _configuration[$"Clients:AdministrationService:RootUrl"].TrimEnd('/');
            var catalogServiceRootUrl = _configuration[$"Clients:CatalogService:RootUrl"].TrimEnd('/');
            var basketServiceRootUrl = _configuration[$"Clients:BasketService:RootUrl"].TrimEnd('/');
            var orderingServiceRootUrl = _configuration[$"Clients:OrderingService:RootUrl"].TrimEnd('/');
            var paymentServiceRootUrl = _configuration[$"Clients:PaymentService:RootUrl"].TrimEnd('/');
            var cmskitServiceRootUrl = _configuration[$"Clients:CmskitService:RootUrl"].TrimEnd('/');
            
            swaggerClient = new Client
            {
                ClientId = "SwaggerClient",
                Name = "Swagger Client Application",
                Protocol = "openid-connect",
                Enabled = true,
                RedirectUris = new List<string>
                {
                    $"{webGatewaySwaggerRootUrl}/swagger/oauth2-redirect.html", // WebGateway redirect uri
                    $"{publicWebGatewayRootUrl}/swagger/oauth2-redirect.html", // PublicWebGateway redirect uri
                    $"{accountServiceRootUrl}/swagger/oauth2-redirect.html", // AccountService redirect uri
                    $"{identityServiceRootUrl}/swagger/oauth2-redirect.html", // IdentityService redirect uri
                    $"{administrationServiceRootUrl}/swagger/oauth2-redirect.html", // AdministrationService redirect uri
                    $"{catalogServiceRootUrl}/swagger/oauth2-redirect.html", // CatalogService redirect uri
                    $"{basketServiceRootUrl}/swagger/oauth2-redirect.html", // BasketService redirect uri
                    $"{orderingServiceRootUrl}/swagger/oauth2-redirect.html", // OrderingService redirect uri
                    $"{paymentServiceRootUrl}/swagger/oauth2-redirect.html", // PaymentService redirect uri
                    $"{cmskitServiceRootUrl}/swagger/oauth2-redirect.html" // CmskitService redirect uri
                },
                FrontChannelLogout = true,
                PublicClient = true
            };

            await _keycloakClient.CreateClientAsync(_keycloakOptions.RealmName, swaggerClient);
        }
    }

    private async Task CreatePublicWebClientAsync()
    {
        var publicWebClient = (await _keycloakClient.GetClientsAsync(_keycloakOptions.RealmName, clientId: "PublicWeb"))
            .FirstOrDefault();

        if (publicWebClient == null)
        {
            var publicWebRootUrl = _configuration[$"Clients:PublicWeb:RootUrl"];
            publicWebClient = new Client
            {
                ClientId = "PublicWeb",
                Name = "Public Web Application",
                Protocol = "openid-connect",
                Enabled = true,
                BaseUrl =  publicWebRootUrl,
                RedirectUris = new List<string>
                {
                    $"{publicWebRootUrl.TrimEnd('/')}/signin-oidc"
                },
                FrontChannelLogout = true,
                PublicClient = true
            };
            publicWebClient.Attributes = new Dictionary<string, object>
            {
                { "post.logout.redirect.uris", $"{publicWebRootUrl.TrimEnd('/')}/signout-callback-oidc" }
            };

            await _keycloakClient.CreateClientAsync(_keycloakOptions.RealmName, publicWebClient);
            //TODO: Update when //https://github.com/AnderssonPeter/Keycloak.Net/pull/5 is merged
            // await AddOptionalClientScopesAsync(
            //     "PublicWeb",
            //     new List<string>
            //     {
            //         "AccountService", "AdministrationService", "IdentityService", "BasketService", "CatalogService",
            //         "OrderingService", "PaymentService", "CmskitService"
            //     }
            // );
        }
    }

    private async Task AddOptionalClientScopesAsync(string clientName, List<string> scopes)
    {
        var client = (await _keycloakClient.GetClientsAsync(_keycloakOptions.RealmName, clientId: clientName))
            .FirstOrDefault();
        if (client == null)
        {
            _logger.LogError($"Couldn't find {clientName}! Could not seed optional scopes!");
            return;
        }

        var clientOptionalScopes =
            (await _keycloakClient.GetOptionalClientScopesAsync(_keycloakOptions.RealmName, client.Id)).ToList();

        var clientScopes = (await _keycloakClient.GetClientScopesAsync(_keycloakOptions.RealmName)).ToList();

        foreach (var scope in scopes)
        {
            if (!clientOptionalScopes.Any(q => q.Name == scope))
            {
                var serviceScope = clientScopes.First(q => q.Name == scope);
                _logger.LogInformation($"Seeding {scope} scope to {clientName}.");
                await _keycloakClient.UpdateOptionalClientScopeAsync(_keycloakOptions.RealmName, client.Id,
                    serviceScope.Id);
            }
        }
    }

    private async Task UpdateAdminUserAsync()
    {
        var users = await _keycloakClient.GetUsersAsync(_keycloakOptions.RealmName, username: "admin");
        var adminUser = users.FirstOrDefault();
        if (adminUser == null)
        {
            _logger.LogError(
                "Keycloak admin user is not provided, check if KEYCLOAK_ADMIN environment variable is passed properly.");
            throw new Exception(
                "Keycloak admin user is not provided, check if KEYCLOAK_ADMIN environment variable is passed properly.");
        }

        if (string.IsNullOrEmpty(adminUser.Email))
        {
            adminUser.Email = "admin@abp.io";
            adminUser.FirstName = "admin";
            adminUser.EmailVerified = true;

            _logger.LogInformation("Updating admin user with email and first name...");
            await _keycloakClient.UpdateUserAsync(_keycloakOptions.RealmName, adminUser.Id, adminUser);
        }
    }
}