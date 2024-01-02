using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

public static class TransformExtensions
{
	public static bool IsPointerOverGameObject()
	{
		//check mouse
		if (EventSystem.current.IsPointerOverGameObject())
		{
			return true;
		}

		//check touch
		if (Input.touchCount > 0 && Input.touches[0].phase == TouchPhase.Began)
		{
			if (EventSystem.current.IsPointerOverGameObject(Input.touches[0].fingerId))
			{
				return true;
			}
		}

		return false;
	}

	public static void DestroyChildren(this Transform transform)
	{
		foreach (Transform children in transform)
		{
			GameObject.Destroy(children.gameObject);
		}
	}

	public static List<TGameObject> RePopulateObjects<TGameObject, TData>
		(
			this Transform container,
			TGameObject prefab,
			List<TData> datas,
			Action<TGameObject, TData> action
		) where TGameObject : UnityEngine.Object
	{
		container.DestroyChildren();

		return container.PopulateObjects(prefab, datas, action);
	}

	public static List<TGameObject> PopulateObjects<TGameObject, TData>
		(
			this Transform container,
			TGameObject prefab,
			List<TData> datas,
			Action<TGameObject, TData> action
		) where TGameObject : UnityEngine.Object
	{
		var list = new List<TGameObject>();

		if (datas != null)
		{
			foreach (var data in datas)
			{
				var newObject = GameObject.Instantiate(prefab, container);
				action?.Invoke(newObject, data);

				list.Add(newObject);
			}
		}

		return list;
	}

	public static void ScrollToPosition(this UnityEngine.UI.ScrollRect instance, Vector3 childPosition)
	{
		instance.content.ForceUpdateRectTransforms();
		instance.viewport.ForceUpdateRectTransforms();

		// now takes scaling into account
		Vector2 viewportLocalPosition = instance.viewport.localPosition;
		Vector2 childLocalPosition = instance.content.InverseTransformPoint(childPosition);
		Vector2 newContentPosition = new Vector2(
			0 - ((viewportLocalPosition.x * instance.viewport.localScale.x) + (childLocalPosition.x * instance.content.localScale.x)),
			0 - ((viewportLocalPosition.y * instance.viewport.localScale.y) + (childLocalPosition.y * instance.content.localScale.y))
		);

		// clamp positions
		instance.content.localPosition = newContentPosition;
		Rect contentRectInViewport = TransformRectFromTo(instance.content.transform, instance.viewport);
		float deltaXMin = contentRectInViewport.xMin - instance.viewport.rect.xMin;
		if (deltaXMin > 0) // clamp to <= 0
		{
			newContentPosition.x -= deltaXMin;
		}
		float deltaXMax = contentRectInViewport.xMax - instance.viewport.rect.xMax;
		if (deltaXMax < 0) // clamp to >= 0
		{
			newContentPosition.x -= deltaXMax;
		}
		float deltaYMin = contentRectInViewport.yMin - instance.viewport.rect.yMin;
		if (deltaYMin > 0) // clamp to <= 0
		{
			newContentPosition.y -= deltaYMin;
		}
		float deltaYMax = contentRectInViewport.yMax - instance.viewport.rect.yMax;
		if (deltaYMax < 0) // clamp to >= 0
		{
			newContentPosition.y -= deltaYMax;
		}

		// apply final position
		instance.content.localPosition = newContentPosition;
		instance.content.ForceUpdateRectTransforms();
	}

	/// <summary>
	/// Converts a Rect from one RectTransfrom to another RectTransfrom.
	/// </summary>
	public static Rect TransformRectFromTo(Transform from, Transform to)
	{
		RectTransform fromRectTrans = from.GetComponent<RectTransform>();
		RectTransform toRectTrans = to.GetComponent<RectTransform>();

		if (fromRectTrans != null && toRectTrans != null)
		{
			Vector3[] fromWorldCorners = new Vector3[4];
			Vector3[] toLocalCorners = new Vector3[4];
			Matrix4x4 toLocal = to.worldToLocalMatrix;
			fromRectTrans.GetWorldCorners(fromWorldCorners);
			for (int i = 0; i < 4; i++)
			{
				toLocalCorners[i] = toLocal.MultiplyPoint3x4(fromWorldCorners[i]);
			}

			return new Rect(toLocalCorners[0].x, toLocalCorners[0].y, toLocalCorners[2].x - toLocalCorners[1].x, toLocalCorners[1].y - toLocalCorners[0].y);
		}

		return default(Rect);
	}

	public static void CopyPropertiesTo<T, TU>(this T source, TU dest)
	{
		var sourceProps = typeof(T).GetProperties().Where(x => x.CanRead).ToList();
		var destProps = typeof(TU).GetProperties()
				.Where(x => x.CanWrite)
				.ToList();

		foreach (var sourceProp in sourceProps)
		{
			if (destProps.Any(x => x.Name == sourceProp.Name))
			{
				var p = destProps.First(x => x.Name == sourceProp.Name);
				if (p.CanWrite)
				{ // check if the property can be set or no.
					p.SetValue(dest, sourceProp.GetValue(source, null), null);
				}
			}
		}
	}
	public static void CopyFieldsTo<T, TU>(this T source, TU dest)
	{
		var sourceProps = typeof(T).GetFields().ToList();
		var destProps = typeof(TU).GetFields().ToList();

		foreach (var sourceProp in sourceProps)
		{
			if (destProps.Any(x => x.Name == sourceProp.Name))
			{
				var p = destProps.First(x => x.Name == sourceProp.Name);
				p.SetValue(dest, sourceProp.GetValue(source));
			}
		}
	}

	public static TOut GetPropValue<T, TOut>(this T source, string propName)
	{
		var sourceProp = typeof(T).GetProperties().Where(x => x.CanRead).First(x => x.Name.Equals(propName));

		var value = (TOut)sourceProp.GetValue(source);

		return value;
	}

	public static List<Transform> GetChildrenAsList(this Transform parent)
	{
		List<Transform> returnList = new List<Transform>();

		foreach (Transform child in parent)
		{
			returnList.Add(child);
		}

		return returnList;
	}

	public static Transform FirstChildOrDefault(this Transform parent, Func<Transform, bool> query)
	{
		if (parent.childCount == 0)
		{
			return null;
		}

		Transform result = null;
		for (int i = 0; i < parent.childCount; i++)
		{
			var child = parent.GetChild(i);
			if (query(child))
			{
				return child;
			}
			result = FirstChildOrDefault(child, query);

			if (result != null)
			{
				return result;
			}
		}

		return result;
	}

	public static List<Transform> FindAllChildrenWith(this Transform root, Func<Transform, bool> predicate)
	{
		List<Transform> result = new List<Transform>();
		foreach (Transform child in root)
		{
			if (predicate(child))
			{
				result.Add(child);
			}

			result.AddRange(FindAllChildrenWith(child, predicate));
		}

		return result;
	}
}
