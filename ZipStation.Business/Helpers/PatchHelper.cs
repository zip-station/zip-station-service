using System.Collections;
using System.Reflection;
using ZipStation.Models.Attributes;
using ZipStation.Models.CommandModels;

namespace ZipStation.Business.Helpers;

public static class PatchHelper
{
    public static T ApplyPatch<T>(this T entity, PatchEntityCommandModel patchModel) where T : class
    {
        var type = typeof(T);

        foreach (var operation in patchModel.Operations)
        {
            var property = type.GetProperty(operation.PropertyName,
                BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

            if (property == null) continue;

            var doNotChange = property.GetCustomAttribute<DoNotChangeOnPatchAttribute>();
            if (doNotChange != null) continue;

            var doNotClear = property.GetCustomAttribute<DoNotClearOnPatchAttribute>();

            switch (operation.OperationType)
            {
                case PatchOperationTypes.Replace:
                    if (operation.Value == null && doNotClear != null)
                    {
                        var currentValue = property.GetValue(entity);
                        if (currentValue != null) continue;
                    }
                    SetPropertyValue(property, entity, operation.Value);
                    break;

                case PatchOperationTypes.Add:
                    AddToList(property, entity, operation.Value);
                    break;

                case PatchOperationTypes.Remove:
                    RemoveFromList(property, entity, operation.Value);
                    break;

                case PatchOperationTypes.Clear:
                    if (doNotClear != null)
                    {
                        var currentValue = property.GetValue(entity);
                        if (currentValue != null) continue;
                    }
                    ClearProperty(property, entity);
                    break;
            }
        }

        return entity;
    }

    private static void SetPropertyValue(PropertyInfo property, object entity, object? value)
    {
        if (value == null)
        {
            if (Nullable.GetUnderlyingType(property.PropertyType) != null || !property.PropertyType.IsValueType)
            {
                property.SetValue(entity, null);
            }
            return;
        }

        var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

        if (targetType.IsEnum)
        {
            var enumValue = Enum.Parse(targetType, value.ToString()!, true);
            property.SetValue(entity, enumValue);
            return;
        }

        var convertedValue = Convert.ChangeType(value, targetType);
        property.SetValue(entity, convertedValue);
    }

    private static void AddToList(PropertyInfo property, object entity, object? value)
    {
        if (value == null) return;

        var list = property.GetValue(entity) as IList;
        if (list == null) return;

        var itemType = property.PropertyType.GetGenericArguments().FirstOrDefault();
        if (itemType == null) return;

        var convertedValue = Convert.ChangeType(value, itemType);
        if (!list.Contains(convertedValue))
        {
            list.Add(convertedValue);
        }
    }

    private static void RemoveFromList(PropertyInfo property, object entity, object? value)
    {
        if (value == null) return;

        var list = property.GetValue(entity) as IList;
        if (list == null) return;

        var itemType = property.PropertyType.GetGenericArguments().FirstOrDefault();
        if (itemType == null) return;

        var convertedValue = Convert.ChangeType(value, itemType);
        list.Remove(convertedValue);
    }

    private static void ClearProperty(PropertyInfo property, object entity)
    {
        if (typeof(IList).IsAssignableFrom(property.PropertyType))
        {
            var list = property.GetValue(entity) as IList;
            list?.Clear();
        }
        else if (Nullable.GetUnderlyingType(property.PropertyType) != null || !property.PropertyType.IsValueType)
        {
            property.SetValue(entity, null);
        }
        else
        {
            property.SetValue(entity, Activator.CreateInstance(property.PropertyType));
        }
    }
}
