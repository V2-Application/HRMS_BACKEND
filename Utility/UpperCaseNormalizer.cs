using System.Reflection;

namespace HRMSAPI.Utility
{
    /// <summary>
    /// Forces every user-entered text field to UPPERCASE before it is persisted, so that
    /// newly submitted candidate / applicant / employee forms match the existing data that
    /// was bulk-uppercased by DatabaseScripts/Force_UpperCase_Candidate_Employee_20260903.sql.
    ///
    /// Deliberately SKIPPED (uppercasing these breaks things):
    ///   * password / hash / salt  -> BCrypt values are compared case-sensitively; uppercasing
    ///                                them locks every user out of the portal.
    ///   * path / url / file / photo / attachment -> document links are case-sensitive on the
    ///                                web server, so uppercasing silently 404s every document.
    ///   * *Json payloads          -> JSON *keys* are case-sensitive; uppercasing the raw string
    ///                                turns {"name":"x"} into {"NAME":"X"} and deserialization
    ///                                stops binding. Nested list values are left as submitted.
    ///   * token / otp / guid / facedata / base64 -> opaque machine values, nothing to gain.
    ///
    /// EMAIL IS INTENTIONALLY UPPERCASED (explicitly requested 2026-09-03). Safe for login:
    /// both DBs use SQL_Latin1_General_CP1_CI_AS, a case-insensitive collation.
    /// </summary>
    public static class UpperCaseNormalizer
    {
        private static readonly string[] SkipIfNameContains =
        {
            "password", "hash", "salt",
            "path", "url", "file", "photo", "attachment", "resume", "link",
            "json",
            // NOTE: keep these specific. A bare "ip" would also match Description,
            // Zip, Recipient, Relationship... and silently leave them mixed case.
            "token", "otp", "guid", "facedata", "base64", "signature", "updatedip",
            // tblEmployee_MedicalCard.RawText is the raw OCR text the card parser reads;
            // uppercasing it risks breaking case-sensitive UHID / policy extraction.
            "rawtext",
            // Audit columns -- machine values, excluded in the bulk SQL script too,
            // so keep the code path consistent with the data that already shipped.
            "createdby", "updatedby", "deletedby"
        };

        private static bool ShouldSkip(string propertyName)
        {
            var n = propertyName.ToLowerInvariant();
            foreach (var frag in SkipIfNameContains)
                if (n.Contains(frag)) return true;
            return false;
        }

        /// <summary>
        /// Uppercases every writable string property on <paramref name="target"/> in place,
        /// except the excluded ones above. Null/whitespace values are left untouched so a
        /// blank field does not become an empty string.
        /// </summary>
        public static void Apply<T>(T target) where T : class => Apply((object)target);

        /// <summary>
        /// Non-generic overload. Reflects over the RUNTIME type, which matters when the caller
        /// only has the object as `object` -- e.g. EF's ChangeTracker entries. Using typeof(T)
        /// there would resolve to System.Object, find no string properties, and silently do
        /// nothing at all.
        /// </summary>
        public static void Apply(object target)
        {
            if (target == null) return;

            foreach (var prop in target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.PropertyType != typeof(string)) continue;
                if (!prop.CanRead || !prop.CanWrite) continue;
                if (ShouldSkip(prop.Name)) continue;

                var current = prop.GetValue(target) as string;
                if (string.IsNullOrWhiteSpace(current)) continue;

                var upper = current.ToUpperInvariant();
                if (!string.Equals(current, upper, StringComparison.Ordinal))
                    prop.SetValue(target, upper);
            }
        }
    }
}
