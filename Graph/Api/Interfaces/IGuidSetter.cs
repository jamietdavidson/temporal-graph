namespace Graph.Api;

public interface IGuidSetter
{
    void SetGuids(List<Create>? creations);
}

public abstract class MutuallyExclusiveGuidResolver : IGuidSetter
{
    public abstract void SetGuids(List<Create>? creations);

    public void SetGuidFromOneOf(string aPropName, string bPropName, List<Create>? creations)
    {
        var thisType = GetType();
        var aProp = thisType.GetProperty(aPropName);
        var bProp = thisType.GetProperty(bPropName);
        var bValue = bProp?.GetValue(this);

        var aDisplayName = Core.Utils.ToCamelCase(aPropName);
        var bDisplayName = Core.Utils.ToCamelCase(bPropName);

        if (aProp?.GetValue(this) != null)
        {
            if (bValue != null)
                throw new GraphQLException(
                    $"Operation is ambiguous: cannot specify both '{aDisplayName}' and '{bDisplayName}' for a single operation.");

            // already set
            return;
        }

        if (bValue == null)
            throw new GraphQLException(
                $"Operation must include either '{aDisplayName}' or '{bDisplayName}'");

        if (creations == null)
            throw new GraphQLException($"Cannot use '{bDisplayName}' when no 'creations' are specified.");

        if ((int)bValue >= creations.Count)
            throw new GraphQLException(
                $"Index out of range: '{bDisplayName}' must be less than size of the 'creations' list.");

        aProp?.SetValue(this, creations[(int)bValue].GetNodeGuid());
    }
}