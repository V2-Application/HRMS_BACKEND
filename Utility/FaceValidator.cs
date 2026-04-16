namespace SuzukiVidms.Infrastructure.Utilities;

public class FaceValidator
{
    private const float SimilarityThreshold = 0.6f;

    public bool ValidateFaceDescriptors(float[] inputDescriptors, float[] storedDescriptors)
    {
        if (inputDescriptors.Length != storedDescriptors.Length)
        {
            throw new ArgumentException("Face descriptors must have the same length.");
        }

        float distance = 0f;
        for (int i = 0; i < inputDescriptors.Length; i++)
        {
            float diff = inputDescriptors[i] - storedDescriptors[i];
            distance += diff * diff;
        }
        distance = (float)Math.Sqrt(distance);

        return distance < SimilarityThreshold;
    }

    public bool ValidateFace(byte[] imageBytes)
    {
        return true; // Placeholder for face detection in image
    }
}