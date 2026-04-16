namespace SuzukiVidms.Infrastructure.Utilities;

using System.Text.Json;

public static class FaceDataUtility
{
    public static float[]? DeserializeFaceDescriptors(string? faceDescriptorsJson)
    {
        if (string.IsNullOrEmpty(faceDescriptorsJson))
            throw new ArgumentException("Face descriptors JSON cannot be empty.");

        try
        {
            // Remove square brackets if present
            string cleanedJson = faceDescriptorsJson.Trim();
            if (cleanedJson.StartsWith("[") && cleanedJson.EndsWith("]"))
            {
                cleanedJson = cleanedJson.Substring(1, cleanedJson.Length - 2);
            }

            // Split by comma and trim each value
            var values = cleanedJson.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .ToArray();

            if (values.Length != 128)
                throw new ArgumentException("Face descriptors must contain exactly 128 values.");

            // Parse values to float array
            var parsedDescriptors = new float[128];
            for (int i = 0; i < values.Length; i++)
            {
                if (!float.TryParse(values[i], out float value))
                {
                    throw new ArgumentException("Invalid number format in face descriptors.");
                }
                parsedDescriptors[i] = value;
            }
            return parsedDescriptors;
        }
        catch (Exception ex)
        {
            throw new ArgumentException("Failed to parse face descriptors.", ex);
        }
    }
}