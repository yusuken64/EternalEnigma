using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
public static class IEnumerableExtension
{
    public static IEnumerable<TSource> DistinctBy<TSource, TKey>
        (this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
    {
        HashSet<TKey> seenKeys = new HashSet<TKey>();
        foreach (TSource element in source)
        {
            if (seenKeys.Add(keySelector(element)))
            {
                yield return element;
            }
        }
    }

    public static bool ContainsDuplicates<T>(this IEnumerable<T> enumerable)
    {
        var knownKeys = new HashSet<T>();
        return enumerable.Any(item => !knownKeys.Add(item));
    }

    public static bool ContainsNoDuplicates<T>(this IEnumerable<T> enumerable)
    {
        var knownKeys = new HashSet<T>();
        return enumerable.All(item => knownKeys.Add(item));
    }

    public static T Sample<T>(this IEnumerable<T> enumerable)
    {
        return enumerable.OrderBy(x => UnityEngine.Random.Range(0, int.MaxValue)).Take(1).FirstOrDefault();
    }

    public static IEnumerable<T> Sample<T>(this IEnumerable<T> enumerable, int pick)
    {
        if (enumerable.IsNullOrEmpty<T>())
        {
            return Enumerable.Empty<T>();
        }

        return enumerable.OrderBy(x => UnityEngine.Random.Range(0, int.MaxValue)).Take(pick);
    }

    public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> enumerable)
    {
        return enumerable.OrderBy(x => UnityEngine.Random.Range(0, int.MaxValue));
    }

    //method compares two elements and returns true for the one to keep
    /// <summary>
    /// var maxHeight = dimensions
    //.Aggregate((agg, next) => 
    ///next.Height > agg.Height? next : agg);
    public static T ElementBy<T>(this IEnumerable<T> List, Func<T, T, bool> method)
    {
        return List
            .Aggregate((agg, next) =>
            method(next, agg) ? next : agg);
    }

    public static bool IsNullOrEmpty<T>(this IEnumerable<T> enumerable)
    {
        return enumerable == null || !enumerable.Any();
    }

    public static IEnumerable<IEnumerable<TSource>> Batch<TSource>(
             this IEnumerable<TSource> source, int size)
    {
        TSource[] bucket = null;
        var count = 0;

        foreach (var item in source)
        {
            if (bucket == null)
                bucket = new TSource[size];

            bucket[count++] = item;
            if (count != size)
                continue;

            yield return bucket;

            bucket = null;
            count = 0;
        }

        if (bucket != null && count > 0)
            yield return bucket.Take(count).ToArray();
    }

    /// <summary>
    ///Dictionary<string, float> foo = new Dictionary<string, float>();
    ///foo.Add("Item 25% 1", 0.5f);
    ///foo.Add("Item 25% 2", 0.5f);
    ///foo.Add("Item 50%", 1f);
    ///
    ///for(int i = 0; i< 10; i++)
    ///Console.WriteLine(this, "Item Chosen {0}", foo.RandomElementByWeight(e => e.Value));
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="sequence"></param>
    /// <param name="weightSelector"></param>
    /// <returns></returns>
    public static T RandomElementByWeight<T>(this IEnumerable<T> sequence, Func<T, float> weightSelector)
    {
        float totalWeight = sequence.Sum(weightSelector);
        // The weight we are after...
        float itemWeightIndex = (float)UnityEngine.Random.value * totalWeight;
        float currentWeightIndex = 0;

        foreach (var item in from weightedItem in sequence
                             select new
                             {
                                 Value = weightedItem,
                                 Weight = weightSelector(weightedItem)
                             })
        {
            currentWeightIndex += item.Weight;

            // If we've hit or passed the weight we are after for this item then it's the one we want....
            if (currentWeightIndex >= itemWeightIndex)
                return item.Value;

        }

        return default(T);
    }

    public static void InsertClamped<T>(this IList<T> list, T element, int index)
    {
        // Clamp the index to be within the bounds of the list
        index = Math.Clamp(index, 0, list.Count);

        // Insert the element at the clamped index
        list.Insert(index, element);
    }
}
