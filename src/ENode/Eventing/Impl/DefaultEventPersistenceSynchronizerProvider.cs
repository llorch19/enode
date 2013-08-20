﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ENode.Infrastructure;

namespace ENode.Eventing.Impl
{
    /// <summary>
    /// 
    /// </summary>
    public class DefaultEventPersistenceSynchronizerProvider : IEventPersistenceSynchronizerProvider, IAssemblyInitializer
    {
        private readonly IDictionary<Type, IList<IEventPersistenceSynchronizer>> _eventSynchronizerDict = new Dictionary<Type, IList<IEventPersistenceSynchronizer>>();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="assemblies"></param>
        /// <exception cref="Exception"></exception>
        public void Initialize(params Assembly[] assemblies)
        {
            foreach (var assembly in assemblies)
            {
                foreach (var synchronizerType in assembly.GetTypes().Where(IsEventPersistenceSynchronizer))
                {
                    if (!TypeUtils.IsComponent(synchronizerType))
                    {
                        throw new Exception(string.Format("{0} should be marked as component.", synchronizerType.FullName));
                    }
                    RegisterSynchronizer(synchronizerType);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="eventType"></param>
        /// <returns></returns>
        public IEnumerable<IEventPersistenceSynchronizer> GetSynchronizers(Type eventType)
        {
            var eventSynchronizers = new List<IEventPersistenceSynchronizer>();
            foreach (var key in _eventSynchronizerDict.Keys.Where(key => key.IsAssignableFrom(eventType)))
            {
                eventSynchronizers.AddRange(_eventSynchronizerDict[key]);
            }
            return eventSynchronizers;
        }

        private void RegisterSynchronizer(Type synchronizerType)
        {
            foreach (var synchronizerInterface in ScanSynchronizerInterfaces(synchronizerType))
            {
                var eventType = GetEventType(synchronizerInterface);
                var synchronizerWrapperType = typeof(EventPersistenceSynchronizerWrapper<>).MakeGenericType(eventType);
                IList<IEventPersistenceSynchronizer> eventSynchronizers = null;
                if (!_eventSynchronizerDict.TryGetValue(eventType, out eventSynchronizers))
                {
                    eventSynchronizers = new List<IEventPersistenceSynchronizer>();
                    _eventSynchronizerDict.Add(eventType, eventSynchronizers);
                }

                if (eventSynchronizers.Any(x => x.GetInnerSynchronizer().GetType() == synchronizerType)) continue;

                var synchronizer = ObjectContainer.Resolve(synchronizerType);
                var synchronizerWrapper = Activator.CreateInstance(synchronizerWrapperType, new object[] { synchronizer }) as IEventPersistenceSynchronizer;
                eventSynchronizers.Add(synchronizerWrapper);
            }
        }
        private bool IsEventPersistenceSynchronizer(Type type)
        {
            return type.IsClass && !type.IsAbstract && ScanSynchronizerInterfaces(type).Any();
        }
        private IEnumerable<Type> ScanSynchronizerInterfaces(Type type)
        {
            return type.GetInterfaces().Where(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEventPersistenceSynchronizer<>));
        }
        private Type GetEventType(Type synchronizerInterface)
        {
            return synchronizerInterface.GetGenericArguments().Single();
        }
    }
}
