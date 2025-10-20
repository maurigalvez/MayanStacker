using System;
using System.Collections.Generic;
using UnityEngine;

public class DependencyRegistry
{
    private static DependencyRegistry _instance;
    public static DependencyRegistry Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new DependencyRegistry();
            }
            return _instance;
        }
    }

    private Dictionary<Type, object> _dependencies = new Dictionary<Type, object>();

    public static void Register<T>(T instance) where T : class
    {
        var type = typeof(T);
        if (Instance._dependencies.ContainsKey(type))
        {
            Debug.LogWarning($"Dependency of type {type} is already registered. Overwriting.");
        }
        Instance._dependencies[type] = instance;
    }

    public static void Unregister<T>(T instance) where T : class
    {
        var type = typeof(T);
        if (Instance._dependencies.ContainsKey(type) && Instance._dependencies[type] == (object)instance)
        {
            Instance._dependencies.Remove(type);
        }
    }

    public static T Find<T>() where T : class
    {
        var type = typeof(T);
        if (Instance._dependencies.TryGetValue(type, out var obj))
        {
            return obj as T;
        }
        Debug.LogWarning($"Dependency of type {type} not found.");
        return null;
    }
}