namespace HRMSAPI.Utility
{
    using System.Data;
    using System.Data.Common;
    using System.Globalization;
    using System.Text;

    public static class DbReaderExt
    {
        // Normalize names: remove spaces/punct, lowercase
        private static string Normalize(string s)
        {
            if (s == null) return "";
            var sb = new StringBuilder(s.Length);
            foreach (var ch in s)
                if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
            return sb.ToString();
        }

        private static Dictionary<string, int> BuildNameMap(DbDataReader r)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < r.FieldCount; i++)
            {
                var raw = r.GetName(i) ?? "";
                var norm = Normalize(raw);
                if (!map.ContainsKey(norm)) map[norm] = i;
            }
            return map;
        }

        public static int SafeOrd(this DbDataReader r, params string[] names)
        {
            // try exact names first
            foreach (var n in names)
            {
                if (string.IsNullOrWhiteSpace(n)) continue;
                try { return r.GetOrdinal(n); } catch { }
            }
            // then normalized
            var map = BuildNameMap(r);
            foreach (var n in names)
            {
                if (string.IsNullOrWhiteSpace(n)) continue;
                if (map.TryGetValue(Normalize(n), out var idx)) return idx;
            }
            return -1;
        }

        private static object? GetVal(this DbDataReader r, params string[] names)
        {
            var i = r.SafeOrd(names);
            return (i < 0 || r.IsDBNull(i)) ? null : r.GetValue(i);
        }

        public static string GetStr(this DbDataReader r, params string[] names)
            => r.GetVal(names)?.ToString() ?? string.Empty;

        public static string? GetStrOrNull(this DbDataReader r, params string[] names)
            => r.GetVal(names)?.ToString();

        public static long GetInt64(this DbDataReader r, params string[] names)
        {
            var v = r.GetVal(names);
            return v switch
            {
                null => 0L,
                long l => l,
                int i => i,
                decimal d => (long)d,
                double d2 => (long)d2,
                string s when long.TryParse(s, out var x) => x,
                _ => 0L
            };
        }

        public static decimal? GetDec(this DbDataReader r, params string[] names)
        {
            var v = r.GetVal(names);
            if (v == null) return null;
            return v switch
            {
                decimal d => d,
                double d2 => (decimal)d2,
                float f => (decimal)f,
                int i => i,
                long l => l,
                short s => s,
                string s when decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) => d,
                _ => null
            };
        }

        public static DateTime? GetDt(this DbDataReader r, params string[] names)
        {
            var v = r.GetVal(names);
            if (v == null) return null;
            return v switch
            {
                DateTime dt => dt,
                string s when DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt) => dt,
                string s2 when DateTime.TryParse(s2, out var dt2) => dt2,
                _ => null
            };
        }
    }
}
