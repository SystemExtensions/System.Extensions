
namespace WebSample
{
    using System;
    using System.Reflection;
    using System.Linq.Expressions;
    using System.Collections.Generic;
    using Microsoft.AspNetCore.Razor.Hosting;
    public class ViewEngine
    {
        private Dictionary<string, Func<IView>> _handlers;
        public ViewEngine() 
        {
            _handlers = new Dictionary<string, Func<IView>>();
        }
        public IView Create(string viewName)
        {
            if (_handlers.TryGetValue(viewName, out var handler))
                return handler();
            return null;
        }
        public void Register(Action<Dictionary<string, Func<IView>>> handler) 
        {
            lock (this)
            {
                var handlers = new Dictionary<string, Func<IView>>(_handlers);
                handler(handlers);
                _handlers = handlers;
            }
        }
        public void Register(Assembly assembly)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));

            Register(new[] { assembly });
        }
        public void Register(IEnumerable<Assembly> assemblies)
        {
            if (assemblies == null)
                throw new ArgumentNullException(nameof(assemblies));

            Register((handlers) => {
                foreach (var assembly in assemblies)
                {
                    var razorDebug = assembly.GetCustomAttribute<RazorDebugAttribute>();
                    if (razorDebug == null)
                    {
                        var razorCompiledItems = assembly.GetCustomAttributes<RazorCompiledItemAttribute>();
                        foreach (var item in razorCompiledItems)
                        {
                            var viewType = item.Type;
                            var viewName = item.Identifier.Substring(0, item.Identifier.Length - ".cshtml".Length);
                            var ctor = viewType.GetConstructor(Type.EmptyTypes);
                            var handler = Expression.Lambda<Func<IView>>(
                                Expression.Convert(Expression.New(ctor), typeof(IView))).Compile();
                            handlers[viewName] = handler;
                        }
                    }
                    else
                    {
                        var debugItems = (List<KeyValuePair<string, Func<object>>>)razorDebug.Type.GetMethod("Execute").Invoke(null, new[] { razorDebug });
                        foreach (var item in debugItems)
                        {
                            var viewName = item.Key.Substring(0, item.Key.Length - ".cshtml".Length);
                            handlers[viewName] = () => (IView)item.Value();
                        }
                    }
                }
            });
        }
    }
}
