using Microsoft.AspNetCore.Mvc;
using AnprDashboardShared;
using System.Drawing.Imaging;
using Tesseract;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using OpenCvSharp;
using Microsoft.AspNetCore.SignalR;
using AnprDashboardServer.Hubs;

namespace AnprDashboardServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DetectionController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly IHubContext<DetectionHub> _hubContext;

        public DetectionController(AppDbContext db, IWebHostEnvironment env, IHubContext<DetectionHub> hubContext)
        {
            _db = db;
            _env = env;
            _hubContext = hubContext;
        }

        [HttpPost("detect")]
        public async Task<IActionResult> DetectPlate([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            string? plateText = null;
            string publicFilePath = string.Empty;

            try
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                byte[] imageBytes = ms.ToArray();

                // Save original uploaded file
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "DetectedPlates");
                Directory.CreateDirectory(uploadsFolder);

                var originalFileName = $"{Guid.NewGuid()}.png";
                var originalFullPath = Path.Combine(uploadsFolder, originalFileName);
                await System.IO.File.WriteAllBytesAsync(originalFullPath, imageBytes);

                // Detect plate region
                Mat plateMat = DetectPlateRegion(imageBytes);

                // Save cropped plate to wwwroot
                publicFilePath = SavePlateImage(plateMat);

                // Preprocess plate for OCR
                byte[] plateBytes = plateMat.ImEncode(".png");
                byte[] processedBytes = PreprocessImage(plateBytes);

                // OCR
                var tessDataPath = Path.Combine(Directory.GetCurrentDirectory(), "TessData");
                using var engine = new TesseractEngine(tessDataPath, "eng", EngineMode.Default);
                engine.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789");
                plateText = TryMultipleAngles(processedBytes, engine);

                // Save record to DB
                var record = new DetectionRecord
                {
                    Plate = string.IsNullOrWhiteSpace(plateText) ? "No plate detected" : plateText,
                    Timestamp = DateTime.Now,
                    FilePath = publicFilePath
                };

                _db.Detections.Add(record);
                await _db.SaveChangesAsync();

                // broadcast to all connected clients
                await _hubContext.Clients.All.SendAsync("ReceiveDetection", record);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Plate = "", Timestamp = DateTime.Now, Error = ex.Message });
            }

            return Ok(new DetectionResult
            {
                Plate = string.IsNullOrWhiteSpace(plateText) ? "No plate detected" : plateText,
                Timestamp = DateTime.Now,
                FilePath = publicFilePath
            });
        }

        private string SavePlateImage(Mat plate)
        {
            var uploadsFolder = Path.Combine(_env.WebRootPath, "DetectedPlates");
            Directory.CreateDirectory(uploadsFolder);

            var fileName = $"{Guid.NewGuid()}.png";
            var fullPath = Path.Combine(uploadsFolder, fileName);

            plate.SaveImage(fullPath); // Save as PNG
            return $"{Request.Scheme}://{Request.Host}/DetectedPlates/{fileName}"; // URL for browser
        }

        private byte[] PreprocessImage(byte[] imageBytes)
        {
            using var image = Image.Load<Rgba32>(imageBytes);

            image.Mutate(x =>
            {
                // Resize if too small
                float scaleFactor = 1.0f;
                if (image.Width < 800 || image.Height < 200)
                {
                    float scaleX = 800f / image.Width;
                    float scaleY = 200f / image.Height;
                    scaleFactor = Math.Min(scaleX, scaleY);
                }
                int newWidth = (int)(image.Width * scaleFactor);
                int newHeight = (int)(image.Height * scaleFactor);
                x.Resize(newWidth, newHeight);

                x.Contrast(1.5f);
                x.Grayscale();
                x.BinaryThreshold(0.4f);
                x.GaussianSharpen(2f);
            });

            using var ms = new MemoryStream();
            image.SaveAsPng(ms);
            return ms.ToArray();
        }

        private string TryMultipleAngles(byte[] imageBytes, TesseractEngine engine)
        {
            string bestText = "No plate detected";
            float bestConfidence = 0;
            string firstNonEmpty = null;

            float[] angles = { -10f, -5f, 0f, 5f, 10f };
            byte[] processed = PreprocessImage(imageBytes);

            foreach (var angle in angles)
            {
                using var image = Image.Load<Rgba32>(processed);
                image.Mutate(x => x.Rotate(angle));

                using var ms = new MemoryStream();
                image.SaveAsPng(ms);
                ms.Position = 0;

                using var pix = Pix.LoadFromMemory(ms.ToArray());
                using var page = engine.Process(pix);
                var text = page.GetText()?.Trim();
                var confidence = page.GetMeanConfidence();

                if (!string.IsNullOrWhiteSpace(text) && firstNonEmpty == null)
                    firstNonEmpty = text;

                if (!string.IsNullOrWhiteSpace(text) && confidence > bestConfidence)
                {
                    bestConfidence = confidence;
                    bestText = text;
                }
            }

            if (bestText == "No plate detected" && firstNonEmpty != null)
                bestText = firstNonEmpty;

            return bestText;
        }

        private Mat DetectPlateRegion(byte[] imageBytes)
        {
            Mat src = Cv2.ImDecode(imageBytes, ImreadModes.Color);
            Mat gray = new Mat();
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.GaussianBlur(gray, gray, new OpenCvSharp.Size(5, 5), 0);

            Mat edges = new Mat();
            Cv2.Canny(gray, edges, 100, 200);

            var contours = Cv2.FindContoursAsArray(edges, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
            OpenCvSharp.Rect plateRect = new OpenCvSharp.Rect();

            foreach (var contour in contours)
            {
                var approx = Cv2.ApproxPolyDP(contour, 0.02 * Cv2.ArcLength(contour, true), true);
                if (approx.Length == 4)
                {
                    var rect = Cv2.BoundingRect(approx);
                    float aspectRatio = rect.Width / (float)rect.Height;
                    if (aspectRatio > 2 && aspectRatio < 6 && rect.Width > 40)
                    {
                        plateRect = rect;
                        break;
                    }
                }
            }

            return plateRect.Width == 0 ? src : new Mat(src, plateRect);
        }
    }
}
