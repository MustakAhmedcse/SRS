namespace SalesCom.Application.Authorization;

/// <summary>
/// Stable integer right ids referenced by <c>[HasRight(id)]</c> on the API. Rights are granted to a
/// user directly in the database (one <c>user_rights</c> row per grant) — there is no rights catalog
/// table and no management endpoint. These constants only name the ids the code enforces; their
/// meaning is owned by whoever administers the grants.
/// </summary>
public static class Rights
{
    public static class DataSources
    {
        /// <summary>Read registered data sources and introspect available source tables.</summary>
        public const int View = 1001;

        /// <summary>Register and update data sources.</summary>
        public const int Manage = 1002;
    }
}
