using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public static class BuffStacking
{
	private static readonly FieldInfo[] Fields = typeof(StartingStats)
		.GetFields(BindingFlags.Public | BindingFlags.Instance)
		.Where(f => f.FieldType == typeof(int) || f.FieldType == typeof(float))
		.ToArray();

	// Per field, keep the value with the largest absolute size (first wins on ties). Null entries are skipped.
	public static StatModification Strongest(IEnumerable<StatModification> mods)
	{
		var result = new StatModification();
		if (mods == null) return result;
		foreach (var mod in mods)
		{
			if (mod == null) continue;
			foreach (var field in Fields)
			{
				if (field.FieldType == typeof(int))
				{
					int current = (int)field.GetValue(result), candidate = (int)field.GetValue(mod);
					if (Math.Abs(candidate) > Math.Abs(current)) field.SetValue(result, candidate);
				}
				else
				{
					float current = (float)field.GetValue(result), candidate = (float)field.GetValue(mod);
					if (Math.Abs(candidate) > Math.Abs(current)) field.SetValue(result, candidate);
				}
			}
		}
		return result;
	}

	// Multiply every field; ints use Math.Round(value * factor, MidpointRounding.AwayFromZero).
	public static StatModification Scale(StatModification mod, float factor)
	{
		var result = new StatModification();
		if (mod == null) return result;
		foreach (var field in Fields)
		{
			if (field.FieldType == typeof(int))
				field.SetValue(result, (int)Math.Round((int)field.GetValue(mod) * factor, MidpointRounding.AwayFromZero));
			else
				field.SetValue(result, (float)field.GetValue(mod) * factor);
		}
		return result;
	}
}
