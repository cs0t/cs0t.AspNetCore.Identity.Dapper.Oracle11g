/*
 * The following code is inspired from https://github.com/aspnet/Identity/blob/master/src/EF/IdentityEntityFrameworkBuilderExtensions.cs
 */

using System;
using cs0t.AspNetCore.Identity.Dapper.Oracle11g.Models;
using cs0t.AspNetCore.Identity.Dapper.Oracle11g.Stores;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Oracle.ManagedDataAccess.Client;

namespace cs0t.AspNetCore.Identity.Dapper.Oracle11g
{
    /// <summary>
    /// Extension methods on <see cref="IdentityBuilder"/> class.
    /// </summary>
    public static class IdentityBuilderExtensions
    {
        /// <summary>
        /// Adds a Dapper implementation of ASP.NET Core Identity stores.
        /// allows logon version11 by default. 
        /// </summary>
        /// <param name="builder">Helper functions for configuring identity services.</param>
        /// <param name="dbProviderOptionsAction"></param>
        /// <returns>The <see cref="IdentityBuilder"/> instance this method extends.</returns>
        public static IdentityBuilder AddDapperStores(this IdentityBuilder builder, Action<DbProviderOptions>? dbProviderOptionsAction) {
            AddStores(builder.Services, builder.UserType, builder.RoleType);
            var options = new DbProviderOptions();
            dbProviderOptionsAction?.Invoke(options);
            builder.Services.AddSingleton(options);
            
            //create supported 11g connection out of box
            OracleConfiguration.SqlNetAllowedLogonVersionClient = OracleAllowedLogonVersionClient.Version11;
            
            builder.Services.AddScoped<IDatabaseConnectionFactory>( _ => DefaultOracleConnectionFactory.Create(options));

            return builder;
        }

        private static void AddStores(IServiceCollection services, Type userType, Type? roleType) {
            if (userType != typeof(ApplicationUser)) {
                throw new InvalidOperationException($"{nameof(AddDapperStores)} can only be called with a user that is of type {nameof(ApplicationUser)}.");
            }

            if (roleType is null) 
                return;
            
            if (roleType != typeof(ApplicationRole)) {
                throw new InvalidOperationException($"{nameof(AddDapperStores)} can only be called with a role that is of type {nameof(ApplicationRole)}.");
            }

            services.TryAddScoped<IUserStore<ApplicationUser>, UserStore>();
            services.TryAddScoped<IRoleStore<ApplicationRole>, RoleStore>();
        }
    }
}
