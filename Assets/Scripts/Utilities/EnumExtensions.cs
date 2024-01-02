using System;
using System.Collections.Generic;

public static class EnumExtensions
{
    public static List<T> GetSelectedEnums<T>(this T val) where T : IConvertible
    {
        List<T> selectedValues = new List<T>();
        Array values = Enum.GetValues(typeof(T));

        //skip i = 0; this is always "None"
        for (int i = 1; i < values.Length; i++)
        {
            int layer = 1 << i;
            if ((Convert.ToInt32(val) & layer) != 0)
            {
                selectedValues.Add((T)values.GetValue(i));
            }
        }
        return selectedValues;
    }

    public static T2 ToType<T, T2>(this T val) where T : IConvertible
    {
        var stringValue = val.ToString();
        Enum.TryParse(typeof(T2), stringValue, out object result);
        return (T2)result;
    }
}
