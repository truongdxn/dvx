using System.Collections.Generic;

namespace Dvx.Contracts
{
    /// <summary>Result of <c>dvx describe</c>: the desired registration of one plugin assembly.</summary>
    public sealed class DescribeResult
    {
        public string AssemblyName { get; set; } = "";

        public IReadOnlyList<ComponentDto> Components { get; set; } = new ComponentDto[0];
    }

    /// <summary>One registration component. <see cref="Kind"/> is serialized as its name.</summary>
    public sealed class ComponentDto
    {
        public string Kind { get; set; } = "";

        public string Id { get; set; } = "";

        public string Key { get; set; } = "";

        public string? ParentId { get; set; }

        /// <summary>Dataverse logical field name to value; keys are written in ordinal order.</summary>
        public IReadOnlyDictionary<string, string?> Fields { get; set; } = new Dictionary<string, string?>();
    }
}
