﻿namespace NServiceBus.Hosting.Wcf
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.ServiceModel;
    using System.ServiceModel.Channels;
    using Logging;

    /// <summary>
    ///     Enable users to expose messages as WCF services
    /// </summary>
    public class WcfManager
    {
        /// <summary>
        ///     Starts a <see cref="ServiceHost" /> for each found service. Defaults to <see cref="BasicHttpBinding" /> if
        ///     no user specified binding is found
        /// </summary>
        public void Startup(Configure config)
        {
            var conventions = config.Builder.Build<Conventions>();

            foreach (var serviceType in config.TypesToScan.Where(t => !t.IsAbstract && IsWcfService(t, conventions)))
            {
                var host = new WcfServiceHost(serviceType);

                Binding binding = new BasicHttpBinding();

                if (config.Configurer.HasComponent<Binding>())
                {
                    binding = config.Builder.Build<Binding>();
                }

                host.AddDefaultEndpoint(GetContractType(serviceType),
                    binding
                    , "");

                hosts.Add(host);

                logger.Debug("Going to host the WCF service: " + serviceType.AssemblyQualifiedName);
                host.Open();
            }
        }

        /// <summary>
        ///     Shuts down the service hosts
        /// </summary>
        public void Shutdown()
        {
            hosts.ForEach(h => h.Close());
        }

        static Type GetContractType(Type t)
        {
            var args = t.BaseType.GetGenericArguments();

            return typeof(IWcfService<,>).MakeGenericType(args);
        }

        static bool IsWcfService(Type t, Conventions conventions)
        {
            var args = t.GetGenericArguments();
            if (args.Length == 2)
            {
                if (conventions.IsMessageType(args[0]))
                {
                    var wcfType = typeof(WcfService<,>).MakeGenericType(args);
                    if (wcfType.IsAssignableFrom(t))
                    {
                        return true;
                    }
                }
            }

            if (t.BaseType != null)
            {
                return IsWcfService(t.BaseType, conventions) && !t.IsAbstract;
            }

            return false;
        }


        static ILog logger = LogManager.GetLogger<WcfManager>();
        readonly List<ServiceHost> hosts = new List<ServiceHost>();
    }
}