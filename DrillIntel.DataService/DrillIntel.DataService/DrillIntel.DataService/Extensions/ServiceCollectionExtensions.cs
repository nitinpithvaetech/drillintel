using System;
using Microsoft.Extensions.DependencyInjection;

namespace DrillIntel.Data
{
    /// <summary>
    /// Extension methods for registering DrillIntel DataService infrastructure in IServiceCollection.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the DrintProjectScopeManager as a singleton and provides dynamic access
        /// to the current project's scope, write coordinator, and processor contexts.
        /// </summary>
        public static IServiceCollection AddDrillIntelDataService(this IServiceCollection services)
        {
            services.AddSingleton<IDrintProjectScopeManager, DrintProjectScopeManager>();

            // Factory to resolve the active project scope from DI
            services.AddTransient<IDrintProjectScope>(sp =>
            {
                var manager = sp.GetRequiredService<IDrintProjectScopeManager>();
                return manager.CurrentScope
                    ?? throw new InvalidOperationException("No DrillIntel project is currently open in the scope manager.");
            });

            // Factory to resolve the active write coordinator
            services.AddTransient<IDrintWriteCoordinator>(sp =>
            {
                var scope = sp.GetRequiredService<IDrintProjectScope>();
                return scope.WriteCoordinator;
            });

            // Factory to create a new processor context for background services
            services.AddTransient<IDrintProcessorContext>(sp =>
            {
                var scope = sp.GetRequiredService<IDrintProjectScope>();
                return scope.CreateProcessorContext();
            });

            // Domain Calculation and Import Services
            services.AddTransient<IDrintCalculationService, DrintCalculationService>();
            services.AddTransient<IBulkLogImportService, BulkLogImportService>();

            return services;
        }
    }
}

