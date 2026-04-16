using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Face;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Emgu.CV.Util;

namespace HRMSAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FaceRecognitionController : ControllerBase
    {
        private readonly string _connectionString;
        private readonly CascadeClassifier _faceClassifier;
        private readonly LBPHFaceRecognizer _recognizer;
        private readonly string _trainedFacesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TrainedFaces");
        public FaceRecognitionController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _faceClassifier = new CascadeClassifier("haarcascade_frontalface_default.xml");
            _recognizer = new LBPHFaceRecognizer(1, 8, 8, 8, 80); // Tuned parameters
            Directory.CreateDirectory(_trainedFacesPath);
            LoadTrainedFaces();
        }

        [HttpPost("register")]
        public async Task<IActionResult> RegisterFace([FromForm] string employeeId, [FromForm] List<IFormFile> faceImages)
        {
            try
            {
                if (faceImages == null || !faceImages.Any() || string.IsNullOrEmpty(employeeId))
                    return BadRequest("Employee ID and at least one face image are required.");

                foreach (var faceImage in faceImages)
                {
                    using var memoryStream = new MemoryStream();
                    await faceImage.CopyToAsync(memoryStream);
                    var imageBytes = memoryStream.ToArray();

                    using Mat mat = new Mat();
                    CvInvoke.Imdecode(imageBytes, ImreadModes.Color, mat);
                    if (mat.IsEmpty)
                        continue;

                    var gray = mat.ToImage<Gray, byte>();
                    var faces = _faceClassifier.DetectMultiScale(gray, 1.2, 10, new Size(20, 20));
                    if (faces.Length == 0)
                        continue;

                    var face = gray.Copy(faces[0]).Resize(100, 100, Inter.Cubic);
                    var faceFilePath = Path.Combine(_trainedFacesPath, $"face_{employeeId}_{Guid.NewGuid()}.bmp");
                    face.Save(faceFilePath);

                    using var connection = new SqlConnection(_connectionString);
                    await connection.OpenAsync();
                    var command = new SqlCommand(
                        "INSERT INTO EmployeeFaces (EmployeeId, FaceImage, CreatedAt) VALUES (@EmployeeId, @FaceImage, @CreatedAt)",
                        connection);
                    command.Parameters.AddWithValue("@EmployeeId", employeeId);
                    command.Parameters.AddWithValue("@FaceImage", imageBytes);
                    command.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);
                    await command.ExecuteNonQueryAsync();
                }

                UpdateRecognizer();
                return Ok(new { Message = $"Face(s) registered successfully for employee {employeeId}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error registering face: {ex.Message}");
            }
        }
        [HttpPost("punchin")]
        public async Task<IActionResult> PunchIn([FromForm] IFormFile faceImage)
        {
            try
            {
                if (faceImage == null)
                    return BadRequest("Face image is required.");

                using var memoryStream = new MemoryStream();
                await faceImage.CopyToAsync(memoryStream);
                var imageBytes = memoryStream.ToArray();

                using Mat mat = new Mat();
                CvInvoke.Imdecode(imageBytes, ImreadModes.Color, mat);
                if (mat.IsEmpty)
                    return BadRequest("Failed to decode image.");

                var gray = mat.ToImage<Gray, byte>();
                var faces = _faceClassifier.DetectMultiScale(gray, 1.2, 10, new Size(20, 20));
                if (faces.Length == 0)
                    return BadRequest("No face detected in the image.");

                var face = gray.Copy(faces[0]).Resize(100, 100, Inter.Cubic);

                _recognizer.Read("recognizer/trainingData.yml");
                var result = _recognizer.Predict(face);

                if (result.Label == -1 || result.Distance > 80) // Add threshold
                    return BadRequest($"Face not recognized (Distance: {result.Distance}).");

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                var command = new SqlCommand(
                    "INSERT INTO PunchRecords (EmployeeId, PunchTime) VALUES (@EmployeeId, @PunchTime)",
                    connection);

                command.Parameters.AddWithValue("@EmployeeId", result.Label.ToString());
                command.Parameters.AddWithValue("@PunchTime", DateTime.UtcNow);
                await command.ExecuteNonQueryAsync();

                return Ok(new { Message = $"Punch-in successful for employee {result.Label}", Confidence = result.Distance });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error processing punch-in: {ex.Message}");
            }
        }
        private void LoadTrainedFaces()
        {
            var trainingImages = new List<Mat>();
            var labels = new List<int>();

            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            var command = new SqlCommand("SELECT EmployeeId, FaceImage FROM EmployeeFaces", connection);
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                var employeeId = reader.GetString(0);
                var imageBytes = (byte[])reader.GetValue(1);

                using Mat mat = new Mat();
                CvInvoke.Imdecode(imageBytes, ImreadModes.Grayscale, mat);
                if (mat.IsEmpty)
                    continue;

                var face = mat.ToImage<Gray, byte>().Resize(100, 100, Inter.Cubic);
                trainingImages.Add(face.Mat); // Convert Image<Gray, byte> to Mat
                labels.Add(int.Parse(employeeId));
            }

            if (trainingImages.Count > 0)
            {
                using var vectorOfImages = new VectorOfMat(trainingImages.ToArray());
                using var vectorOfLabels = new VectorOfInt(labels.ToArray());
                _recognizer.Train(vectorOfImages, vectorOfLabels);
                _recognizer.Write("recognizer/trainingData.yml");
            }

            // Dispose of Mat objects
            foreach (var mat in trainingImages)
            {
                mat.Dispose();
            }
        }
        private void UpdateRecognizer()
        {
            var trainingImages = new List<Mat>();
            var labels = new List<int>();

            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            var command = new SqlCommand("SELECT EmployeeId, FaceImage FROM EmployeeFaces", connection);
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                var employeeId = reader.GetString(0);
                var imageBytes = (byte[])reader.GetValue(1);

                using Mat mat = new Mat();
                CvInvoke.Imdecode(imageBytes, ImreadModes.Grayscale, mat);
                if (mat.IsEmpty)
                    continue;

                var face = mat.ToImage<Gray, byte>().Resize(100, 100, Inter.Cubic);
                trainingImages.Add(face.Mat); // Convert Image<Gray, byte> to Mat
                labels.Add(int.Parse(employeeId));
            }

            if (trainingImages.Count > 0)
            {
                using var vectorOfImages = new VectorOfMat(trainingImages.ToArray());
                using var vectorOfLabels = new VectorOfInt(labels.ToArray());
                _recognizer.Train(vectorOfImages, vectorOfLabels);
                _recognizer.Write("recognizer/trainingData.yml");
            }

            // Dispose of Mat objects
            foreach (var mat in trainingImages)
            {
                mat.Dispose();
            }
        }
    }
}
 