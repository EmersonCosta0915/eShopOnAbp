﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Domain.Entities.Events.Distributed;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.MongoDB;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace EShopOnAbp.Shared.Hosting.Microservices.DbMigrations.MongoDb;

public abstract class DatabaseMongoDbMigrationEventHandler<TDbContext> : DatabaseMigrationEventHandlerBase
    where TDbContext : AbpMongoDbContext, IAbpMongoDbContext
{
    protected const string TryCountPropertyName = "TryCount";
    protected const int MaxEventTryCount = 3;

    protected ICurrentTenant CurrentTenant { get; }
    protected IUnitOfWorkManager UnitOfWorkManager { get; }
    protected ITenantStore TenantStore { get; }
    protected IDistributedEventBus DistributedEventBus { get; }
    protected ILogger<DatabaseMongoDbMigrationEventHandler<TDbContext>> Logger { get; set; }
    protected IServiceProvider ServiceProvider { get; }
    protected string DatabaseName { get; }
    protected IAbpDistributedLock DistributedLockProvider { get; }


    protected DatabaseMongoDbMigrationEventHandler(
        ICurrentTenant currentTenant,
        IUnitOfWorkManager unitOfWorkManager,
        ITenantStore tenantStore,
        IDistributedEventBus distributedEventBus,
        string databaseName,
        IServiceProvider serviceProvider,
        IAbpDistributedLock distributedLockProvider
        )
    {
        CurrentTenant = currentTenant;
        UnitOfWorkManager = unitOfWorkManager;
        TenantStore = tenantStore;
        DatabaseName = databaseName;
        ServiceProvider = serviceProvider;
        DistributedEventBus = distributedEventBus;
        DistributedLockProvider = distributedLockProvider;

        Logger = NullLogger<DatabaseMongoDbMigrationEventHandler<TDbContext>>.Instance;
    }

    /// <summary>
    /// Apply pending EF Core schema migrations to the database.
    /// Returns true if any migration has applied.
    /// </summary>
    protected virtual async Task<bool> MigrateDatabaseSchemaAsync()
    {
        var result = false;

        using (var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: false))
        {
            async Task<bool> MigrateDatabaseSchemaWithDbContextAsync()
            {
                var dbContexts = ServiceProvider.GetServices<IAbpMongoDbContext>();
                var connectionStringResolver = ServiceProvider.GetRequiredService<IConnectionStringResolver>();

                foreach (var dbContext in dbContexts)
                {
                    var connectionString =
                        await connectionStringResolver.ResolveAsync(
                            ConnectionStringNameAttribute.GetConnStringName(dbContext.GetType()));
                    if (connectionString.IsNullOrWhiteSpace())
                    {
                        continue;
                    }

                    var mongoUrl = new MongoUrl(connectionString);
                    var databaseName = mongoUrl.DatabaseName;
                    var client = new MongoClient(mongoUrl);

                    if (databaseName.IsNullOrWhiteSpace())
                    {
                        databaseName = ConnectionStringNameAttribute.GetConnStringName(dbContext.GetType());
                    }

                    (dbContext as AbpMongoDbContext)?.InitializeCollections(client.GetDatabase(databaseName));
                }

                return true;
            }

            //Migrating the host database
            result = await MigrateDatabaseSchemaWithDbContextAsync();

            await uow.CompleteAsync();
        }

        return result;
    }

    protected virtual async Task HandleErrorOnApplyDatabaseMigrationAsync(
        ApplyDatabaseMigrationsEto eventData,
        Exception exception)
    {
        var tryCount = IncrementEventTryCount(eventData);
        if (tryCount <= MaxEventTryCount)
        {
            Logger.LogWarning(
                $"Could not apply database migrations. Re-queueing the operation. TenantId = {eventData.TenantId}, Database Name = {eventData.DatabaseName}.");
            Logger.LogException(exception, LogLevel.Warning);

            await Task.Delay(RandomHelper.GetRandom(5000, 15000));
            await DistributedEventBus.PublishAsync(eventData);
        }
        else
        {
            Logger.LogError(
                $"Could not apply database migrations. Canceling the operation. TenantId = {eventData.TenantId}, DatabaseName = {eventData.DatabaseName}.");
            Logger.LogException(exception);
        }
    }

    private static int GetEventTryCount(EtoBase eventData)
    {
        var tryCountAsString = eventData.Properties.GetOrDefault(TryCountPropertyName);
        if (tryCountAsString.IsNullOrEmpty())
        {
            return 0;
        }

        return int.Parse(tryCountAsString);
    }

    private static void SetEventTryCount(EtoBase eventData, int count)
    {
        eventData.Properties[TryCountPropertyName] = count.ToString();
    }

    private static int IncrementEventTryCount(EtoBase eventData)
    {
        var count = GetEventTryCount(eventData) + 1;
        SetEventTryCount(eventData, count);
        return count;
    }
}