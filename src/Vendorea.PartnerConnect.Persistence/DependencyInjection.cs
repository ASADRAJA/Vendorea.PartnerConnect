using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Persistence.Repositories;

namespace Vendorea.PartnerConnect.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPartnerConnectPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<PartnerConnectDbContext>(options =>
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(PartnerConnectDbContext).Assembly.FullName);
                sqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
            }));

        RegisterRepositories(services);

        return services;
    }

    public static IServiceCollection AddPartnerConnectPersistenceInMemory(
        this IServiceCollection services,
        string databaseName = "PartnerConnectTestDb")
    {
        services.AddDbContext<PartnerConnectDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));

        RegisterRepositories(services);

        return services;
    }

    private static void RegisterRepositories(IServiceCollection services)
    {
        services.AddScoped<ITradingPartnerRepository, TradingPartnerRepository>();
        services.AddScoped<IDealerPartnerConnectionRepository, DealerPartnerConnectionRepository>();
        services.AddScoped<IPartnerDocumentRepository, PartnerDocumentRepository>();
    }
}
