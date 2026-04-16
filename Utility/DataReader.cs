using System.Data.Common;

namespace HRMSAPI.Utility
{
    public static class DataReaderExtensions
    {
        public static bool IsDBNull(this DbDataReader reader, string columnName)
            => reader.IsDBNull(reader.GetOrdinal(columnName));

        public static DateTime? GetNullableDateTime(this DbDataReader reader, string columnName)
        {
            if (reader.IsDBNull(columnName))
                return null;
            var value = reader[columnName]?.ToString();
            if (string.IsNullOrWhiteSpace(value) || value == "00:00:00")
                return null;
            return DateTime.TryParse(value, out var date) ? date : null;
        }

        public static TimeSpan? GetNullableTimeSpan(this DbDataReader reader, string columnName)
        {
            if (reader.IsDBNull(columnName))
                return null;
            var value = reader[columnName]?.ToString();
            if (string.IsNullOrWhiteSpace(value) || value == "00:00:00")
                return null;
            return TimeSpan.TryParse(value, out var time) ? time : null;
        }
    }
}
