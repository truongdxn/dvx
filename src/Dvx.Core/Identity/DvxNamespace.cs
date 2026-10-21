using System;

namespace Dvx.Core.Identity;

/// <summary>
/// The frozen UUID v5 namespace for all dvx component IDs (SPEC §7, D33). Never change this value.
/// </summary>
public static class DvxNamespace
{
    public static readonly Guid Value = new("b3f5d2a1-7c4e-4f8a-9d61-2e0c5a7b9f13");
}
