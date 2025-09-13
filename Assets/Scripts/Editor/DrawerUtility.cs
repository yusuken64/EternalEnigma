using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

public static class DrawerUtility
{
	public static Dictionary<string, Type> BuildTypeMap<T>()
	{
		var baseType = typeof(T);
		var typeMap = AppDomain.CurrentDomain.GetAssemblies()
			.SelectMany(asm =>
			{
				try { return asm.GetTypes(); }
				catch { return Type.EmptyTypes; }
			})
			.Where(t => !t.IsAbstract &&
						baseType.IsAssignableFrom(t))
			.ToDictionary(t => ObjectNames.NicifyVariableName(t.Name), t => t);

		return typeMap;
	}

	public static string GetShortTypeName(string fullTypeName)
	{
		if (string.IsNullOrEmpty(fullTypeName)) return null;
		var parts = fullTypeName.Split(' ');
		return parts.Length > 1 ? parts[1].Split('.').Last() : fullTypeName;
	}
}