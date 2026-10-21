namespace Dvx.Plugin.Sdk
{
    /// <summary>Pipeline stage of a plugin step (Dataverse values).</summary>
    internal enum Stage
    {
        PreValidation = 10,
        PreOperation = 20,
        PostOperation = 40
    }

    /// <summary>Execution mode of a plugin step.</summary>
    internal enum Mode
    {
        Synchronous = 0,
        Asynchronous = 1
    }

    /// <summary>Binding type of a Custom API.</summary>
    internal enum CustomApiBindingType
    {
        Global = 0,
        Entity = 1,
        EntityCollection = 2
    }

    /// <summary>Allowed custom processing step type of a Custom API.</summary>
    internal enum CustomProcessingStepType
    {
        None = 0,
        AsyncOnly = 1,
        SyncAndAsync = 2
    }

    /// <summary>Data type of a Custom API request parameter or response property.</summary>
    internal enum CustomApiFieldType
    {
        Boolean = 0,
        DateTime = 1,
        Decimal = 2,
        Entity = 3,
        EntityCollection = 4,
        EntityReference = 5,
        Float = 6,
        Integer = 7,
        Money = 8,
        Picklist = 9,
        String = 10,
        StringArray = 11,
        Guid = 12
    }
}
